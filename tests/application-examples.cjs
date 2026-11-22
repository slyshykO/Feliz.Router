const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");

async function screenshot(page, name) {
    const directory = path.join(__dirname, "bin", "demo-browser");
    await fs.mkdir(directory, { recursive: true });
    await page.screenshot({ path: path.join(directory, `${name}.png`), fullPage: true });
}

async function withPage(browser, timed, run, expectedErrors = []) {
    const context = await browser.newContext();
    try {
        const page = await context.newPage();
        const unexpected = [], caught = [], consoleChecks = [];
        page.on("pageerror", error => unexpected.push(error.message));
        page.on("console", message => {
            if (message.type() !== "error") return;
            consoleChecks.push((async () => {
                // Fable exceptions are objects, not native Error instances. Chrome's
                // console preview can show only the minified constructor name.
                const details = await Promise.all(message.args().map(arg =>
                    arg.evaluate(value => String(value?.message ?? value))));
                const text = [message.text(), ...details].join("\n");
                // Only the two deliberate demo failures may be logged by React.
                if (expectedErrors.some(marker => text.includes(marker))) caught.push(text);
                else unexpected.push(text);
            })());
        });
        await page.route("https://maxcdn.bootstrapcdn.com/**", route => route.fulfill({ contentType: "text/css", body: "" }));
        if (timed) {
            // Advance actual sample-request timers deterministically, without wall-clock sleeps.
            await page.clock.install({ time: new Date("2026-01-01T00:00:00Z") });
            await page.clock.pauseAt(new Date("2026-01-01T00:01:00Z"));
        }
        await run(page);
        await Promise.all(consoleChecks);
        assert.deepEqual(unexpected, []);
        for (const marker of expectedErrors) assert(caught.some(text => text.includes(marker)), `Expected a caught ${marker}`);
    } finally {
        await context.close();
    }
}

const heading = (page, title) => page.getByRole("heading", { name: title, exact: true }).waitFor();
const click = (page, name) => page.getByRole("button", { name, exact: true }).click();
const home = page => page.getByRole("link", { name: "Home link", exact: true }).click();
const finishRequest = page => page.clock.runFor(2000);

async function checkActivity(browser, origin) {
    await withPage(browser, false, async page => {
        await page.goto(`${origin}/?example=activity#/work`);
        await heading(page, "Work");
        await page.getByTestId("work-draft").fill("Keep the work draft");
        await page.keyboard.press("ArrowUp");
        await click(page, "Add a point");
        assert.equal(await page.getByTestId("work-points").textContent(), "2");
        await page.getByRole("link", { name: "Personal tab", exact: true }).click();
        await heading(page, "Personal");
        assert.equal(await page.getByTestId("work-panel").count(), 1, "The hidden page must remain mounted");
        assert.equal(await page.getByTestId("work-panel").isVisible(), false);
        await page.getByTestId("personal-draft").fill("Keep the personal draft");
        await page.keyboard.press("ArrowUp");
        assert.equal(await page.getByTestId("personal-points").textContent(), "1");
        await click(page, "Back");
        await heading(page, "Work");
        assert.equal(await page.getByTestId("work-draft").inputValue(), "Keep the work draft");
        assert.equal(await page.getByTestId("work-points").textContent(), "2", "Hidden effects must stop receiving keyboard events");
        await page.keyboard.press("ArrowUp");
        assert.equal(await page.getByTestId("work-points").textContent(), "3", "Revealing a page must reconnect exactly one listener");
        await screenshot(page, "activity");
        await click(page, "Forward");
        await heading(page, "Personal");
        assert.equal(await page.getByTestId("personal-draft").inputValue(), "Keep the personal draft");
        assert.equal(await page.getByTestId("personal-points").textContent(), "1");
        await page.goto(`${origin}/?example=activity#/personal`);
        await page.reload();
        await heading(page, "Personal");
        assert.equal(await page.getByTestId("personal-draft").inputValue(), "", "Activity does not persist state across a reload");
    });
    console.log("activity: preserved state, hidden effects, history and deep links passed");
}

async function checkSuspense(browser, origin) {
    await withPage(browser, true, async page => {
        await page.goto(`${origin}/?example=suspense#/home`);
        await heading(page, "Home");
        await click(page, "Open overview");
        await page.getByTestId("report-loading").waitFor();
        await page.getByTestId("shell-draft").fill("Editing while loading");
        await screenshot(page, "suspense-loading");
        await finishRequest(page);
        await heading(page, "Overview report");
        await home(page);
        await heading(page, "Home");
        await click(page, "Back");
        await heading(page, "Overview report");
        assert.equal(await page.getByTestId("report-loading").count(), 0, "A cached report must render without advancing the clock");
        await page.getByTestId("keep-page").check();
        await click(page, "Open details");
        assert(page.url().endsWith("#/reports/details"), "The URL changes while the next page is pending");
        await heading(page, "Overview report");
        assert.equal(await page.getByTestId("report-loading").count(), 0, "A transition must keep the revealed page");
        await page.getByTestId("shell-draft").fill("Editing during a transition");
        await finishRequest(page);
        await heading(page, "Detailed report");
        assert.equal(await page.getByTestId("shell-draft").inputValue(), "Editing during a transition");

        // An initial deep link suspends; leaving before completion must not bring it back later.
        await page.goto(`${origin}/?example=suspense#/reports/overview`);
        await page.reload();
        await page.getByTestId("report-loading").waitFor();
        await home(page);
        await heading(page, "Home");
        await finishRequest(page);
        await heading(page, "Home");
        await click(page, "Open overview");
        await heading(page, "Overview report");
        await page.goto(`${origin}/?example=suspense#/reports/unknown`);
        await heading(page, "Not found");
    });
    console.log("suspense: fallback, cache, transitions, urgent input and superseded navigation passed");
}

async function checkErrors(browser, origin) {
    await withPage(browser, true, async page => {
        await page.goto(`${origin}/?example=errors#/reports/unavailable`);
        await page.getByTestId("report-loading").waitFor();
        await page.getByTestId("shell-draft").fill("Keep this after recovery");
        await finishRequest(page);
        await page.getByTestId("report-error").waitFor();
        await screenshot(page, "errors");
        await home(page);
        await heading(page, "Home");
        await click(page, "Back");
        await page.getByTestId("report-error").waitFor();
        await click(page, "Try again");
        await page.getByTestId("report-loading").waitFor();
        await finishRequest(page);
        await heading(page, "Recovered report");
        assert.equal(await page.getByTestId("shell-draft").inputValue(), "Keep this after recovery");
        await click(page, "Simulate rendering error");
        await page.getByTestId("report-error").waitFor();
        await click(page, "Open overview");
        await page.getByTestId("report-loading").waitFor();
        await finishRequest(page);
        await heading(page, "Overview report");
        await click(page, "Simulate rendering error");
        await page.getByTestId("report-error").waitFor();
        await click(page, "Try again");
        await page.getByTestId("report-loading").waitFor();
        await finishRequest(page);
        await heading(page, "Overview report");
        assert.equal(await page.getByTestId("shell-draft").inputValue(), "Keep this after recovery");
    }, ["Demo request failure: unavailable report", "Demo render failure: report component"]);
    console.log("errors: rejected requests, render failures, retries and route-key resets passed");
}

module.exports = async function checkApplicationExamples(browser, origin) {
    await checkActivity(browser, origin);
    await checkSuspense(browser, origin);
    await checkErrors(browser, origin);
};
