# Feliz.Router modernization plan

**Repository:** [slyshykO/Feliz.Router — `update-deps`](https://github.com/slyshykO/Feliz.Router/tree/update-deps)  
**Date:** November 22, 2026  
**Scope:** Summary of the discussed improvements, compatibility updates, and maintenance items for Feliz.Router, informed by Feliz 3.4.0 and React 19.3.

## Executive summary

Keep Feliz.Router small, straightforward, and compatible with both plain React/Feliz and Elmish. Prioritize correcting navigation event handling, modernizing dependency versions, and adding browser-level regression tests. Treat transitions, route hooks, and animation as **optional APIs**, rather than forcing new behavior on existing consumers.

The highest-value change is fixing potentially stale callbacks in the router's existing one-time event subscription. The next is handling hash navigation correctly in modern browsers. The newly available React APIs enable cleaner solutions and optional routing enhancements, but most of the features in Feliz 3.4.0 do not belong in the router core.

## Baseline observed in `update-deps`

The following values record the initial review, before the dependency update. Completed implementation notes below supersede this baseline.

| Component | Current state in branch |
| --- | --- |
| Feliz NuGet | `>= 3.2.0` in `paket.dependencies`; **3.2.0** resolved in `paket.lock` |
| React / ReactDOM | `^19.2.0` declared; **19.2.4** in `package-lock.json` |
| Fable.Elmish.React | 5.0.x |
| Feliz.Router package version | 4.100.0 |
| Routing modes | Hash and Path |
| Navigation notification | `onUrlChanged` and custom `CUSTOM_NAVIGATION_EVENT` |
| Elmish integration | `Cmd.navigate` and `Cmd.navigatePath` |
| Core implementation | `src/Router.fs` (routing helpers and public navigation overloads) |

Key files: [Router.fs](https://github.com/slyshykO/Feliz.Router/blob/update-deps/src/Router.fs), [package.json](https://github.com/slyshykO/Feliz.Router/blob/update-deps/package.json), [paket.dependencies](https://github.com/slyshykO/Feliz.Router/blob/update-deps/paket.dependencies), [Feliz.Router.fsproj](https://github.com/slyshykO/Feliz.Router/blob/update-deps/src/Feliz.Router.fsproj), and [Tests.fs](https://github.com/slyshykO/Feliz.Router/blob/update-deps/tests/Tests.fs).

## Recommended improvements

### P0 — Fix stale navigation callbacks using `React.useEffectEvent`

**Status: Implemented.** `src/Router.fs` now creates `onChange` with `React.useEffectEvent`, so the existing listeners read the latest committed `onUrlChanged` callback and routing mode after a re-render.

**Previous bug:** `React.useCallback(...)` had no dependencies array, while `React.useEffectOnce(...)` installed its first-render callback once. Subsequent navigation retained stale `input.onUrlChanged` and `input.hashMode` values.

The subscription and cleanup effects remain one-time effects; each cleanup removes the same listener that its effect installed. Initial route notification and public APIs are preserved. Runtime requirements are React/ReactDOM 19.2+ and Feliz 3.4.0+; this fork uses React 19.3.1.

**Regression coverage:** Six mounted-router cases in `tests/Tests.fs` run through the existing Fable.Mocha runner with jsdom and awaited React `act`:

- Repeated callback replacement in Hash and Path modes, with and without Strict Mode.
- Switching routing modes in both directions while retaining the same callback.
- Adding, removing, and restoring an optional callback after mount.
- Custom navigation and `popstate`, initial notification, and cleanup after prop changes and unmount.

All six cases reproduced the bug before the hook change, establishing a passing 19-test suite at that step. The generated Fable JavaScript imports `useEffectEvent` directly and preserves listener identity during cleanup. The native-event work below extends that coverage.

### P0 — Listen to both `hashchange` and `popstate`

**Status: Implemented.** The router now subscribes to both native events and the custom navigation event, with matching cleanup on unmount. The obsolete Trident/MSIE user-agent branch has been removed.

- `hashchange`: fragment (`#...`) navigation, including normal hash links.
- `popstate`: navigation through browser history entries.
- `CUSTOM_NAVIGATION_EVENT`: the library's own notification after `Router.navigate(...)` / `Router.navigatePath(...)`.

**Notification policy:** Each router remembers its last reported `(routing mode, route URL)` in a React ref. Hash mode compares the fragment; Path mode compares pathname plus query string. Repeated native events for that route are ignored, including a `popstate`/`hashchange` pair. Changes outside the selected route are ignored too, such as fragment-only navigation in Path mode.

The ref is updated before calling `onUrlChanged`, so callback-triggered navigation remains safe. It stores only the last route: visits such as A → B → A still notify at each step, and router instances have independent tracking.

Explicit `Router.navigate(...)` / `Router.navigatePath(...)` calls retain their custom event and notify even for the current URL, with both PushState and ReplaceState. Initial mount notifications, including Strict Mode effect replay, are preserved.

**Regression coverage:** This step expanded the suite to 31 tests. It covers hashchange-only delivery, both native-event orders, duplicate events, query changes, normal hash links, `location.hash`, native Back/Forward, explicit same-URL navigation, redirects from callbacks, and multiple routers. Callback freshness and cleanup exercise all three listeners. Twelve assertions failed against the previous implementation before this fix.

### P0 — Add React/DOM integration tests

**Status: Implemented.** This step expanded `tests/Tests.fs` to 39 tests: 13 parsing/formatting tests and 26 mounted-router/application cases. `tests/IntegrationApp.fs` supplies a React `useState` application and a real Elmish program with a test-owned React root, dispatch queue, update function, and navigation commands.

Coverage includes:

- Callback replacement and routing-mode changes after re-render.
- Hash links, `location.hash`, native history events, and notification deduplication.
- Initial deep links with encoded query values and actual rendered page updates.
- Button-driven PushState/ReplaceState through `Router.navigate`/`Router.navigatePath` and Elmish `Cmd.navigate`/`Cmd.navigatePath`, with history-length assertions.
- Back/Forward restoring the expected page after replacement navigation.
- Both application styles in Hash and Path modes, with and without Strict Mode.
- Listener cleanup after unmount; Elmish remains active during the cleanup assertion, then its dispatch loop is terminated.

Run `npm test` for Mocha/jsdom or `npm run test:browser` for the same F# suite in an installed Chrome browser. The checked-in Playwright Core runner also supports Edge through `BROWSER_CHANNEL=msedge` and custom Chromium executables through `BROWSER_EXECUTABLE_PATH`. It discovers the test count, waits for sequential completion, reports failures and browser errors, and closes the browser and local server.

Generated files remain isolated in `tests/bin/fable/` and `tests/bin/browser/`. Both runners execute the same suite, subsequently expanded to 52 tests by the numeric-overload work below. Transition-specific coverage is tracked with the P1 transition feature below.

### P1 — Update Feliz and React dependencies

**Status: Implemented** in commit `65b6972` (`Update dependencies and build tooling for .NET 10 LTS`).

The dependency declarations and committed lockfiles now agree:

| Dependency | Declared constraint | Locked version |
| --- | --- | --- |
| Feliz | `~> 3.4` in `paket.dependencies` | 3.4.0 |
| React | `^19.3.1` in `package.json` | 19.3.1 |
| ReactDOM | `^19.3.1` in `package.json` | 19.3.1 |

Both `paket.lock` and `package-lock.json` were updated. The upgraded dependencies have passed the Release solution build, production demo build, and the current 52-test suite under Mocha/jsdom and headless Chrome. The router's `useEffectEvent` runtime requirement is documented in the README.

### P1 — Offer opt-in React transitions for route updates

**Opportunity:** `React.startTransition` is exposed in Feliz 3.4.0. Consider an opt-in property such as:

```fsharp
React.router [
    router.useTransitions true // proposed API, not implemented
    router.onUrlChanged updateUrl
    router.children currentPage
]
```

The intent is to mark React route-state updates as non-urgent, helping keep interactions responsive when changing to a rendering-heavy page. This also provides a basis for `React.ViewTransition` animations.

**Important caveats:** `startTransition` only marks React updates made synchronously inside its callback. Wrapping an Elmish `dispatch` does not necessarily mark a state update that happens later, and state updates after `let!` / `await` need their own transition handling. The API should remain optional and must be verified with both plain Feliz and Fable.Elmish.React. It is not a Web Worker and does not remove synchronous computation costs.

### P1 — Document integration with React `ViewTransition`

Prefer **documentation/examples rather than an always-on router animation layer**. The application should own animation styling; the router should only expose or enable navigation semantics when useful.

Example composition (illustrative):

```fsharp
React.router [
    router.onUrlChanged updateUrl
    router.children [
        React.ViewTransition [
            viewTransition.update "page-transition"
            viewTransition.children [ currentPage ]
        ]
    ]
]
```

The state update causing the page change must participate in a supported React Transition, deferred update, or Suspense reveal; wrapping pages in `ViewTransition` alone does not animate ordinary updates.

Consider directional types (`forward`, `backward`) using `React.addTransitionType` only if they can be attached to the **same transition as the actual React state update**. Browser Back/Forward and scrolling/restoration require extra consideration; modern Navigation API integration may be a separate later effort. Browsers without View Transitions should continue to navigate normally.

### P2 — Add an optional route-reading hook

For plain React/Feliz apps, a `Router.useLocation()` or `Router.useRoute()` convenience hook could replace manual `useState` + `router.onUrlChanged` wiring:

```fsharp
[<ReactComponent>]
let App () =
    let location = Router.useLocation() // proposed API
    // match location with ...
```

**Candidate implementation:** `React.useSyncExternalStore`, which was already exposed before Feliz 3.4.0. Browser URL/history is an external source of state, making this a natural conceptual fit.

**Caveats:** `getSnapshot` must return a stable value when the URL has not changed; do not allocate a new `string list` every time. Prefer a primitive URL string or a cached parsed result. Also, `useSyncExternalStore` updates are synchronous and cannot simply replace the transition-aware React route state if the goal is animated navigation. Keep this hook additive, not a mandatory replacement for `onUrlChanged`.

### P2 — Provide examples for `React.Activity`, Suspense, and ErrorBoundary

These can stay in userland rather than core router code:

- **`React.Activity`** (React 19.2): hide an inactive page while preserving its local component state. Useful for tab-like route views. Avoid a global `router.keepAlive true` until cache size, eviction, URL parameters, memory use, and lifecycle semantics are designed.
- **`React.Suspense` + `React.use'`**: represent loading for cached or externally created Promises during route rendering. Avoid creating a fresh Promise every render.
- **`React.ErrorBoundary`**: isolate rendering failures of one page and optionally retry or reset when route parameters change (`resetKeys`). Does not catch arbitrary event-handler or async errors.

### P2/P3 — Review unsafe casts and simplify internal overload handling

**Status: completed.** Integer route segments and query values now use `string` conversion. The remaining `unbox`/`!!` expressions are intentional React property/object interop; there are no numeric-to-string casts left.

Query assembly is centralized in `encodePartsWithQuery`, and integer queries reuse the string query encoder. `navigate`/`navigatePath` reuse `format`/`formatPath` and a single history/event helper; Elmish wrappers forward typed arguments and defer navigation until command execution. All 384 formatting/navigation overload declarations are preserved, including parameter names, `ParamArray`, and `inline` modifiers. Existing full-path and query handling is retained.

Regression coverage exercises all 80 formatting overloads with negative, zero, positive, and `Int32` boundary values; all 148 explicit-history Router/Elmish navigation overloads with `ReplaceState`; and representative default/explicit push navigation with Back traversal. Tests also cover empty queries, escaping, one notification per navigation, and deferred Elmish effects. Before the fix, the new formatting tests reproduced `part.indexOf is not a function` in both route modes.

## Package metadata and stale dependency discussion

### `Elmish.Toastr` in `package.json` is **not** a dependency

The branch's `package.json` includes:

```json
"repository": {
  "type": "git",
  "url": "https://github.com/Zaid-Ajaj/Elmish.Toastr.git"
}
```

This is a repository **metadata URL**, not an npm dependency. It does not install Elmish.Toastr, increase the bundle size, or create compatibility/security exposure to that package. Its update frequency is irrelevant to Feliz.Router. It appears to be an incorrect copy-pasted URL.

Correct the metadata, for example:

```json
"repository": {
  "type": "git",
  "url": "https://github.com/slyshykO/Feliz.Router.git"
}
```

Other metadata cleanup:

- `src/Feliz.Router.fsproj` still points `RepositoryUrl` and `PackageLicenseUrl` at the original `Zaid-Ajaj/Feliz.Router` repository. Decide on attribution/publishing intent, then update the relevant package links (while preserving required license and original author attribution).
- `"remotedev": "^0"` **is** an npm development dependency. Check whether the project still imports or uses it; if unused, consider removing it. Its maintenance status was not verified in this review.
- Review build tooling/CI configuration for current Fable 5, .NET, Node, and React compatibility.

## Suggested implementation order

| Phase | Deliverable | Priority |
| --- | --- | --- |
| 1 | Correct stale callbacks and subscription lifecycle — **completed** | P0 |
| 1 | Handle `hashchange`, `popstate`, and custom navigation consistently — **completed** | P0 |
| 1 | Add mounted-router tests and Strict Mode coverage — **completed** | P0 |
| 2 | Upgrade Feliz/React, regenerate lockfiles, verify builds — **completed** | P1 |
| 2 | Add **opt-in** transition behavior; test both React and Elmish | P1 |
| 2 | Publish a `ViewTransition` integration example | P1 |
| 3 | Explore additive `Router.useLocation()` / `useRoute()` | P2 |
| 3 | Add examples for Activity, Suspense, ErrorBoundary | P2 |
| 3 | Fix numeric-to-string `unbox` and reduce internal duplication — **completed** | P2/P3 |
| 3 | Correct npm/NuGet metadata; review `remotedev` and CI | P2 |

## Design principles to preserve

1. **Backwards compatibility:** retain the current `React.router`, `router.onUrlChanged`, `Router.navigate`, `Router.navigatePath`, and Elmish `Cmd` ergonomics.
2. **Small core:** avoid making the router an all-in-one data fetching, caching, animation, or state management framework.
3. **Optional modern features:** React Transition and animation capabilities should be opt-in or demonstrated through composition.
4. **Correct browser semantics:** history entries, hash links, listener cleanup, and initial route notification matter more than visual polish.
5. **Explicit React requirements:** clearly distinguish which features require React 19.2 versus 19.3.

## References

- [Feliz 3.4.0 changelog](https://github.com/fable-hub/Feliz/blob/main/src/Feliz/CHANGELOG.md)
- [Feliz React bindings](https://github.com/fable-hub/Feliz/blob/main/src/Feliz/React/React.fs)
- [React docs: `useEffectEvent`](https://react.dev/reference/react/useEffectEvent)
- [React docs: `useSyncExternalStore`](https://react.dev/reference/react/useSyncExternalStore)
- [React docs: `startTransition`](https://react.dev/reference/react/startTransition)
- [React docs: `ViewTransition`](https://react.dev/reference/react/ViewTransition)
- [Feliz.Router `update-deps` branch](https://github.com/slyshykO/Feliz.Router/tree/update-deps)

---

**Verification status:** The dependency-update P1, all three P0 items, and the unsafe-cast/overload P2/P3 item are implemented. `npm test` and `npm run test:browser` (Chrome 158) each pass all 52 tests with no skipped cases or browser errors. `dotnet build Feliz.Router.sln --configuration Release` passes with zero warnings/errors. `npm run build:demo` and a production-demo smoke check of numeric query navigation, push/replace, Back, and native hash links pass; webpack's existing bundle-size warnings remain. Remaining snippets marked as proposed/illustrative are API designs or sketches, not validated patches.
