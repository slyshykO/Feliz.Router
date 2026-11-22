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

Generated files remain isolated in `tests/bin/fable/` and `tests/bin/browser/`. Both runners execute the same suite, subsequently expanded to 52 tests by the numeric-overload work, 79 by the transition work, and 93 by the route-reading hook below.

### P1 — Update Feliz and React dependencies

**Status: Implemented** in commit `65b6972` (`Update dependencies and build tooling for .NET 10 LTS`).

The dependency declarations and committed lockfiles now agree:

| Dependency | Declared constraint | Locked version |
| --- | --- | --- |
| Feliz | `~> 3.4` in `paket.dependencies` | 3.4.0 |
| React | `^19.3.1` in `package.json` | 19.3.1 |
| ReactDOM | `^19.3.1` in `package.json` | 19.3.1 |

Both `paket.lock` and `package-lock.json` were updated. The upgraded dependencies passed the Release solution build, production demo build, and the 52-test suite under Mocha/jsdom and headless Chrome before the transition work below. The router's `useEffectEvent` runtime requirement is documented in the README.

### P1 — Offer opt-in React transitions for route updates

**Status: completed.** `router.useTransitions true` wraps `onUrlChanged` in `React.startTransition`. The option defaults to `false` and is read from the latest committed props:

```fsharp
React.router [
    router.useTransitions true
    router.onUrlChanged updateUrl
    router.children currentPage
]
```

URL/history changes and callback invocation remain synchronous. React can defer rendering and retain an already revealed Suspense page while its replacement loads. Initial, programmatic, and native notifications use the same option; deduplication and cleanup are preserved.

**Elmish integration:** `Cmd.navigate` can queue `UrlChanged` until the router's transition has ended. The application-level `demo/TransitionHelpers.fs` helper wraps the actual render when the route changes. Apply it after `Program.withReactSynchronous`; `withReactBatched` schedules a later animation-frame callback and does not preserve this scope. The demo uses the actual Fable.Elmish.React renderer. State updates after `let!` / `await` likewise need a transition around their actual React update. Synchronous callback work is not moved to another thread.

**Regression coverage:** `tests/TransitionApp.fs` supplies controlled Suspense fixtures. Twenty-seven added cases cover default/disabled/enabled behavior in Hash/Path modes with and without Strict Mode, urgent interactions during suspension, resolved navigation and Back/Forward, immediate callbacks/URL updates, option and callback freshness, superseded navigation, listener cleanup, and the queued Elmish dispatch limitation. Both React state and real Elmish programs exercise the supported scheduling paths.

### P1 — Document integration with React `ViewTransition`

**Status: completed.** The README documents composition and runtime requirements, with runnable React-state and Elmish examples in `demo/Transitions.fs`. Open `/?example=transitions#/home` or `/?example=transitions-elmish#/home` on the demo server. The application owns the animation boundary and styling in `public/transitions.css`; the router adds no automatic animation layer.

Example composition (React/ReactDOM 19.3+):

```fsharp
React.router [
    router.useTransitions true
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

`npm run test:demo` builds the production demo and verifies both examples with the real browser View Transition API. It checks actual animated durations for push/replace, disabled transitions, reduced motion, absence of the browser API, native links, Back/Forward, history entries, and preserved local input state. Screenshots are written to ignored `tests/bin/demo-browser/`.

**Browser limitation:** React intentionally skips animations from legacy `popstate` events to preserve scroll/form restoration. Back/Forward and native hash links still navigate correctly; the Chromium tests verify that these paths do not animate. Navigation API integration and directional transition types remain separate future work. Browsers without View Transitions navigate normally, and reduced-motion CSS disables animated durations.

### P2 — Add an optional route-reading hook

**Status: completed.** `Router.useRoute(?mode: RouteMode)` returns parsed route segments and subscribes to browser navigation without requiring `React.router` or manual `useState` / `onUrlChanged` wiring:

```fsharp
[<ReactComponent>]
let App () =
    let route = Router.useRoute() // Hash by default; use RouteMode.Path for paths
    match route with
    | [] | [ "home" ] -> Html.h1 "Home"
    | [ "users"; Route.Int id ] -> Html.h1 (sprintf "User %d" id)
    | _ -> Html.h1 "Not found"
```

**Implementation:** `React.useSyncExternalStore` observes a primitive URL string: the fragment in Hash mode, or pathname plus query in Path mode. Stable snapshot/subscription functions avoid subscription churn; parsed lists are memoized by URL and mode. Each consumer subscribes to `hashchange`, `popstate`, and `CUSTOM_NAVIGATION_EVENT`, with independent cleanup. Changing mode reads the new location immediately. React rechecks the snapshot after subscribing, covering navigation between render and subscription.

**Semantics:** Same-URL events do not request a hook render; existing callback routing still receives explicit same-URL navigation notifications. Raw history writes emit no event, so use the Router navigation functions. The hook is client-only and supplies no SSR snapshot. External-store updates remain synchronous even inside `startTransition`; keep callback-based React state for deferred rendering, Suspense page retention, and ViewTransition integration.

**Documentation and coverage:** The README covers Hash/Path usage and limitations. `demo/RouteHook.fs`, available at `/?example=hook#/home`, demonstrates standalone navigation. Fourteen cases in `tests/Tests.fs` with fixtures in `tests/RouteHookApp.fs` cover deep links, queries, PushState/ReplaceState, native links/hash assignment, Back/Forward, selected-URL filtering, duplicate events, callback coexistence, mode changes, memoized segments, subscription identity/cleanup under Strict Mode, multiple consumers, the subscription race, and synchronous Suspense behavior. The production browser smoke test also verifies navigation and reloads in the hook demo.

### P2 — More application examples

**Status: completed.** Three runnable examples compose React features in application code, with shared presentation in `demo/ExampleLayout.fs`. The README explains the patterns and lists every launch URL under **Running examples**. No new router options or dependencies are required.

- **Activity** — `demo/ActivityExample.fs`, `/?example=activity#/work`: two fixed tab pages preserve local drafts and counters through `React.Activity` (React 19.2+). Native links and Back/Forward select the visible page. A keyboard subscription is removed while a page is hidden and restored on reveal. The retained set is intentionally bounded; reloads reset local state.
- **Suspense** — `demo/AsyncExamples.fs`, `/?example=suspense#/home`: simulated requests use a module-level Promise cache; page state holds the selected Promise and a child reads it with `React.use'`. A checkbox compares the ordinary loading fallback with `router.useTransitions true` retaining an already revealed page. Draft input stays usable, cached reports can be revisited, and completing an abandoned request does not replace the newer route.
- **ErrorBoundary** — `demo/AsyncExamples.fs`, `/?example=errors#/home`: both rejected requests and child render failures are isolated to the page. Retry replaces the resource and calls the fallback's `reset`; `resetKeys` uses the report ID and Promise reference to recover on route changes. Navigation and draft state remain available. Arbitrary event-handler/async errors are outside this boundary's scope.

The sample cache covers three fixed reports and resets on reload; production apps need their own data-layer lifetime, invalidation, and cancellation policies. The sample requests make no network calls. `tests/application-examples.cjs` exercises the real production demos with Playwright-controlled timers, allowing only the two deliberate error messages in React's console output. It runs through `npm run test:demo` alongside the existing transition and hook scenarios. Screenshot review also prompted an explicit UTF-8 declaration in the demo HTML.

### P2/P3 — Review unsafe casts and simplify internal overload handling

**Status: completed.** Integer route segments and query values now use `string` conversion. The remaining `unbox`/`!!` expressions are intentional React property/object interop; there are no numeric-to-string casts left.

Query assembly is centralized in `encodePartsWithQuery`, and integer queries reuse the string query encoder. `navigate`/`navigatePath` reuse `format`/`formatPath` and a single history/event helper; Elmish wrappers forward typed arguments and defer navigation until command execution. All 384 formatting/navigation overload declarations are preserved, including parameter names, `ParamArray`, and `inline` modifiers. Existing full-path and query handling is retained.

Regression coverage exercises all 80 formatting overloads with negative, zero, positive, and `Int32` boundary values; all 148 explicit-history Router/Elmish navigation overloads with `ReplaceState`; and representative default/explicit push navigation with Back traversal. Tests also cover empty queries, escaping, one notification per navigation, and deferred Elmish effects. Before the fix, the new formatting tests reproduced `part.indexOf is not a function` in both route modes.

## Package metadata and continuous integration

**Metadata cleanup: completed.** The earlier `Elmish.Toastr` URL was copied repository metadata, not a dependency. `package.json` and the NuGet repository/license links now point to this fork, while the original author remains credited. The unused `remotedev` dependency has been removed.

**CI migration: implemented.** `.github/workflows/ci.yml` replaces `.travis.yml` and `appveyor.yml`. The Ubuntu 24.04 job runs on pushes, pull requests, and manual dispatches with .NET from `global.json` and Node.js 26. It restores tools and dependencies, checks tracked F# files with Fantomas, builds the solution and release tooling, compiles the production demo, runs the console Mocha/jsdom suite, and verifies NuGet packaging. Browser-dependent suites remain local commands.

Fantomas 8.1.3 is pinned in `.config/dotnet-tools.json`; `dotnet tool restore` makes the same formatter available locally and in CI. The README documents local formatting commands and links to the new workflow badge. The solution's references to the retired CI files have also been replaced.

**CI validation:** Actionlint 1.7.12 accepts the workflow. A clean source copy passed every workflow command in WLinux (Pengwin/Debian 13), using .NET 10.0.404 and Linux Node.js 26.13.1: tool/dependency restoration, formatting, both .NET builds, the production demo build, all 93 console tests, and NuGet packaging. The GitHub-hosted Ubuntu run will occur after the workflow is pushed. Existing webpack size warnings and NuGet license/readme notices remain.

## Suggested implementation order

| Phase | Deliverable | Priority |
| --- | --- | --- |
| 1 | Correct stale callbacks and subscription lifecycle — **completed** | P0 |
| 1 | Handle `hashchange`, `popstate`, and custom navigation consistently — **completed** | P0 |
| 1 | Add mounted-router tests and Strict Mode coverage — **completed** | P0 |
| 2 | Upgrade Feliz/React, regenerate lockfiles, verify builds — **completed** | P1 |
| 2 | Add **opt-in** transition behavior; test both React and Elmish — **completed** | P1 |
| 2 | Document a `ViewTransition` integration example — **completed** | P1 |
| 3 | Add optional `Router.useRoute()` — **completed** | P2 |
| 3 | Add examples for Activity, Suspense, ErrorBoundary — **completed** | P2 |
| 3 | Fix numeric-to-string `unbox` and reduce internal duplication — **completed** | P2/P3 |
| 3 | Correct metadata, remove unused `remotedev`, and migrate CI to GitHub Actions — **completed** | P2 |

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
- [React docs: `Activity`](https://react.dev/reference/react/Activity)
- [React docs: `use` and Promise caching](https://react.dev/reference/react/use)
- [React docs: error boundaries](https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary)
- [Feliz.Router `update-deps` branch](https://github.com/slyshykO/Feliz.Router/tree/update-deps)

---

**Verification status:** All listed P0/P1 items, the optional route-reading hook, the application examples, and the unsafe-cast/overload P2/P3 item are implemented. `npm test` passes all 93 tests; the unchanged library suite was also verified with `npm run test:browser` in Chrome 158 during the hook work. The production demo build and `tests/run-demo-browser.cjs` pass ten scenarios: six React/Elmish transition scenarios, standalone hook navigation, Activity tabs, Suspense loading, and ErrorBoundary recovery. There are no unexpected browser errors; the error demo deliberately exercises two logged failure types. Screenshots of all three new examples were reviewed. `dotnet build Feliz.Router.slnx --configuration Release` passes with zero warnings/errors. Fantomas 8.1.3 checks pass for all 17 repository F# source files. Webpack's existing bundle-size warnings remain. Remaining snippets marked as proposed/illustrative are API designs or sketches, not validated patches.
