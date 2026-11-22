const { JSDOM } = require("jsdom");

const dom = new JSDOM("<!doctype html><html><body></body></html>", {
    url: "http://localhost/",
    pretendToBeVisual: true
});

// Set up the DOM before importing the Fable output and ReactDOM.
globalThis.window = dom.window;
globalThis.document = dom.window.document;
globalThis.history = dom.window.history;
globalThis.IS_REACT_ACT_ENVIRONMENT = true;

exports.mochaHooks = {
    afterAll() {
        dom.window.close();
        delete globalThis.window;
        delete globalThis.document;
        delete globalThis.history;
        delete globalThis.IS_REACT_ACT_ENVIRONMENT;
    }
};
