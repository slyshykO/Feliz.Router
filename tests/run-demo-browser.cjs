const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const http = require("node:http");
const path = require("node:path");
const { chromium } = require("playwright-core");

const repository = path.resolve(__dirname, "..");

async function checkExample(browser, origin, example, mode) {
    const context = await browser.newContext({ reducedMotion: mode === "reduced" ? "reduce" : "no-preference" });
    try {
        const page = await context.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
        // The demo's optional Bootstrap stylesheet is unrelated to navigation; keep this test offline.
        await page.route("https://maxcdn.bootstrapcdn.com/**", route => route.fulfill({ contentType: "text/css", body: "" }));
        await page.addInitScript(disable => {
            window.__viewTransitions = [];
            if (disable) {
                Object.defineProperty(document, "startViewTransition", { configurable: true, value: undefined });
                return;
            }
            const start = document.startViewTransition?.bind(document);
            if (!start) return;
            // Observe the real browser API, including real animations; do not mock its implementation.
            document.startViewTransition = (...args) => {
                const entry = { finished: false, animations: [], error: null };
                window.__viewTransitions.push(entry);
                const transition = start(...args);
                transition.ready.then(() => {
                    entry.animations = document.getAnimations().map(animation => animation.effect.getTiming().duration);
                    entry.animationNames = document.getAnimations().map(animation => animation.animationName);
                }, error => { entry.error = String(error); });
                transition.finished.then(() => { entry.finished = true; }, error => { entry.error = String(error); entry.finished = true; });
                return transition;
            };
        }, mode === "unsupported");
        await page.goto(`${origin}/?example=${example}#/home`);
        await page.getByTestId("page").filter({ hasText: "Home" }).waitFor();
        if (mode !== "unsupported") {
            assert.equal(await page.evaluate(() => typeof document.startViewTransition), "function", "This test needs a browser with View Transitions");
        }
        const settle = () => page.waitForFunction(() => window.__viewTransitions.every(entry => entry.finished));
        await settle();

        async function navigate(title, action, animate) {
            const before = await page.evaluate(() => window.__viewTransitions.length);
            await action();
            await page.waitForFunction(expected => document.querySelector('[data-testid="page"]')?.textContent === expected, title);
            if (animate) {
                await page.waitForFunction(count => window.__viewTransitions.length > count, before);
            }
            await settle();
            const entries = await page.evaluate(count => window.__viewTransitions.slice(count), before);
            if (animate) {
                assert(entries.length > 0, "Navigation must activate React ViewTransition");
                assert(entries.every(entry => entry.error === null), JSON.stringify(entries));
                // React may retain zero-duration bookkeeping animations even when CSS disables motion.
                if (mode === "reduced") assert(entries.every(entry => entry.animations.every(duration => duration === 0)), "Reduced motion must disable animated durations: " + JSON.stringify(entries));
                else assert(entries.some(entry => entry.animations.some(duration => duration > 0)), "The browser must create an actual animation");
            } else assert.equal(entries.length, 0, "This navigation should complete without starting a View Transition");
        }

        await page.getByTestId("draft").fill("Keep this draft");
        const historyLength = await page.evaluate(() => history.length);
        await navigate("Gallery", () => page.getByRole("button", { name: "Open gallery", exact: true }).click(), mode !== "unsupported");
        assert.equal(await page.evaluate(() => history.length), historyLength + 1, "Push navigation adds an entry");
        if (mode === "normal") {
            await fs.mkdir(path.join(__dirname, "bin", "demo-browser"), { recursive: true });
            await page.screenshot({ path: path.join(__dirname, "bin", "demo-browser", `${example}.png`), fullPage: true });
        }
        await navigate("About", () => page.getByRole("button", { name: "Replace with About", exact: true }).click(), mode !== "unsupported");
        assert.equal(await page.evaluate(() => history.length), historyLength + 1, "Replace navigation keeps the history length");
        // React deliberately skips animations for legacy popstate navigation to preserve
        // scroll/form restoration. Chromium also emits popstate for native hash links.
        await navigate("Home", () => page.getByRole("button", { name: "Back", exact: true }).click(), false);
        await navigate("About", () => page.getByRole("button", { name: "Forward", exact: true }).click(), false);
        await navigate("Home", () => page.getByRole("link", { name: "Home link", exact: true }).click(), false);
        await page.getByTestId("enable-transitions").uncheck();
        await navigate("Gallery", () => page.getByRole("button", { name: "Open gallery", exact: true }).click(), false);
        assert.equal(await page.getByTestId("draft").inputValue(), "Keep this draft", "Navigation should preserve local state outside the animated page");
        assert.deepEqual(errors, []);
        console.log(`${example}: ${mode} passed`);
    } finally {
        await context.close();
    }
}

async function checkRouteHook(browser, origin) {
    const context = await browser.newContext();
    try {
        const page = await context.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
        await page.route("https://maxcdn.bootstrapcdn.com/**", route => route.fulfill({ contentType: "text/css", body: "" }));
        await page.goto(`${origin}/?example=hook#/home`);
        const expectPage = title => page.waitForFunction(expected => document.querySelector('[data-testid="hook-page"]')?.textContent === expected, title);
        await expectPage("Home");
        const historyLength = await page.evaluate(() => history.length);
        await page.getByRole("button", { name: "Open user 42", exact: true }).click();
        await expectPage("User 42");
        assert.equal(await page.evaluate(() => history.length), historyLength + 1);
        await fs.mkdir(path.join(__dirname, "bin", "demo-browser"), { recursive: true });
        await page.screenshot({ path: path.join(__dirname, "bin", "demo-browser", "hook.png"), fullPage: true });
        await page.getByRole("button", { name: "Replace with About", exact: true }).click();
        await expectPage("About");
        assert.equal(await page.evaluate(() => history.length), historyLength + 1);
        await page.reload();
        await expectPage("About");
        await page.getByRole("button", { name: "Back", exact: true }).click();
        await expectPage("Home");
        await page.getByRole("button", { name: "Forward", exact: true }).click();
        await expectPage("About");
        await page.getByRole("link", { name: "Home link", exact: true }).click();
        await expectPage("Home");
        assert.deepEqual(errors, []);
        console.log("hook: standalone navigation passed");
    } finally {
        await context.close();
    }
}

async function main() {
    const assets = new Map(await Promise.all([
        ["/", "index.html", "text/html"],
        ["/bundle.js", "bundle.js", "text/javascript"],
        ["/transitions.css", "transitions.css", "text/css"]
    ].map(async ([url, file, type]) => [url, { body: await fs.readFile(path.join(repository, "public", file)), type }])));
    const server = http.createServer((request, response) => {
        const pathname = new URL(request.url, "http://localhost").pathname;
        if (pathname === "/favicon.ico") return response.writeHead(204).end();
        const asset = assets.get(pathname) || assets.get("/");
        response.writeHead(200, { "Content-Type": asset.type }).end(asset.body);
    });
    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(0, "127.0.0.1", resolve);
    });
    let browser;
    try {
        const executablePath = process.env.BROWSER_EXECUTABLE_PATH;
        browser = await chromium.launch({ headless: true,
            ...(executablePath ? { executablePath } : { channel: process.env.BROWSER_CHANNEL || "chrome" }) });
        const origin = `http://127.0.0.1:${server.address().port}`;
        for (const example of ["transitions", "transitions-elmish"]) {
            for (const mode of ["normal", "reduced", "unsupported"]) await checkExample(browser, origin, example, mode);
        }
        await checkRouteHook(browser, origin);
    } finally {
        try { if (browser) await browser.close(); }
        finally {
            server.closeAllConnections();
            await new Promise(resolve => server.close(resolve));
        }
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
