const fs = require("node:fs/promises");
const http = require("node:http");
const path = require("node:path");
const webpack = require("webpack");
const { chromium } = require("playwright-core");

const repository = path.resolve(__dirname, "..");
const outputDirectory = path.join(__dirname, "bin", "browser");

async function buildTests() {
    await new Promise((resolve, reject) => {
        const compiler = webpack({
            mode: "development", // React act and Strict Mode checks require the development build.
            context: repository,
            entry: "./tests/bin/fable/Tests.js",
            output: { path: outputDirectory, filename: "bundle.js" },
            devtool: false
        });
        compiler.run((error, stats) => compiler.close(closeError => {
            if (error || closeError) reject(error || closeError);
            else if (stats.hasErrors()) reject(new Error(stats.toString({ all: false, errors: true })));
            else resolve();
        }));
    });
    return fs.readFile(path.join(outputDirectory, "bundle.js"));
}

async function main() {
    const bundle = await buildTests();
    const html = '<!doctype html><html><body><script>globalThis.IS_REACT_ACT_ENVIRONMENT=true;</script><script src="/bundle.js"></script></body></html>';
    const server = http.createServer((request, response) => {
        if (request.url === "/favicon.ico") {
            response.writeHead(204).end();
        } else if (request.url === "/bundle.js") {
            response.writeHead(200, { "Content-Type": "text/javascript" }).end(bundle);
        } else {
            // History API routes must remain on this same origin.
            response.writeHead(200, { "Content-Type": "text/html; charset=utf-8" }).end(html);
        }
    });
    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(0, "127.0.0.1", resolve);
    });

    let browser;
    try {
        const executablePath = process.env.BROWSER_EXECUTABLE_PATH;
        browser = await chromium.launch({
            headless: true,
            ...(executablePath ? { executablePath } : { channel: process.env.BROWSER_CHANNEL || "chrome" })
        });
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("console", message => {
            if (message.type() === "error") errors.push(message.text());
        });
        await page.goto(`http://127.0.0.1:${server.address().port}/`);
        await page.waitForSelector("[data-test]");
        const total = await page.locator("[data-test]").count();
        console.log(`Running ${total} integration/unit tests in ${browser.browserType().name()} ${browser.version()}...`);
        // Fable.Mocha's sequential browser runner waits one second before every test.
        await page.waitForFunction(() => document.querySelectorAll(".executing").length === 0,
            undefined, { timeout: total * 2000 + 10000 });
        const result = await page.evaluate(() => ({
            passed: document.querySelectorAll(".passed").length,
            failed: Array.from(document.querySelectorAll(".failed"), element => element.textContent),
            skipped: document.querySelectorAll(".pending").length
        }));
        console.log(JSON.stringify({ ...result, errors }, null, 2));
        if (result.failed.length || errors.length || result.passed === 0 ||
            result.passed + result.skipped !== total) {
            throw new Error("Browser test suite failed; see the results above.");
        }
    } finally {
        try {
            if (browser) await browser.close();
        } finally {
            server.closeAllConnections();
            await new Promise(resolve => server.close(resolve));
        }
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
