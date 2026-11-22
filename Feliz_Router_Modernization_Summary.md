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
| Core implementation | `src/Router.fs` (about 1,481 lines, with many navigation overloads) |

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

All six cases reproduced the bug before the hook change; all 19 tests pass afterward. The generated Fable JavaScript imports `useEffectEvent` directly and preserves listener identity during cleanup. The separate `hashchange` and deduplication work below remains pending.

### P0 — Listen to both `hashchange` and `popstate`

**Observed code:** The router chooses `hashchange` only for legacy IE user agents (Trident/MSIE), and `popstate` otherwise. Modern browsers use distinct events:

- `hashchange`: fragment (`#...`) navigation, including normal hash links.
- `popstate`: navigation through browser history entries.
- `CUSTOM_NAVIGATION_EVENT`: the library's own notification after `Router.navigate(...)` / `Router.navigatePath(...)`.

**Proposed change:** Subscribe to both native events where appropriate; preserve the custom event for `history.pushState` and `history.replaceState`, which do not emit `popstate` automatically. Drop obsolete IE-specific branching if legacy IE is no longer supported by the overall React 19 stack.

Deduplicate notifications for the same effective location where necessary. Ensure both Hash and Path modes work under direct links, Back/Forward, and programmatic navigation. Be especially careful not to drop genuine navigation changes when deduplicating.

### P0 — Add React/DOM integration tests

The existing `tests/Tests.fs` includes useful parsing and URL formatting tests; expand coverage to the mounted router behavior. Recommended cases:

1. Re-render with a new `onUrlChanged` callback and verify that the next navigation uses it.
2. Re-render with changed routing mode; verify correct URL interpretation and subscriptions.
3. Navigate through a normal `href="#/..."` link and check `hashchange` handling.
4. Test `popstate`, Back/Forward, `pushState`, `replaceState`, and the custom navigation event.
5. Confirm no duplicate callback for one effective location change (when deduplication is implemented).
6. Verify event-listener cleanup on unmount and under React Strict Mode.
7. Exercise both Hash and Path modes, React-only usage, and Elmish dispatch.
8. Test opt-in transitions separately: synchronous React state changes versus delayed/asynchronous Elmish updates.

The callback-freshness work now uses Fable.Mocha with jsdom for mounted-router tests, including routing-mode changes and Strict Mode cleanup. Real-browser history/hash-link checks, deduplication, and the remaining integration cases are follow-up work.

### P1 — Update Feliz and React dependencies

Align the library/demo with the new APIs:

`paket.dependencies`:

```text
nuget Feliz >= 3.4.0
```

Then re-resolve and commit `paket.lock` so the build actually uses the new version.

`package.json`:

```json
{
  "dependencies": {
    "react": "^19.3.1",
    "react-dom": "^19.3.1"
  }
}
```

Update the npm lockfile and run a build/test pass. React 19.3 is needed for `ViewTransition`, `addTransitionType`, Fragment refs, and the associated new features. If React 19.2 consumers must remain supported, avoid using 19.3-only APIs unconditionally in router core, or clearly declare the raised runtime requirement as a compatibility change.

Review package-release notes and compatibility requirements before publishing a new version.

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

`Router.fs` contains several expressions like `unbox<string> segment2` where `segment2` is an `int`. This is not normal numeric formatting in .NET; replace with `string segment2` where textual conversion is intended, and test generated URLs.

The file has many similar overloads for `format`, `navigate`, `navigatePath`, and Elmish `Cmd` wrappers. Keep public overloads for source compatibility, but centralize encoding and navigation logic in a smaller number of internal helpers. This reduces repetition and copy/paste mistakes without forcing consumers to change code.

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
| 1 | Correct stale callbacks and subscription lifecycle | P0 |
| 1 | Handle `hashchange`, `popstate`, and custom navigation consistently | P0 |
| 1 | Add mounted-router tests and Strict Mode coverage | P0 |
| 2 | Upgrade Feliz/React, regenerate lockfiles, verify builds | P1 |
| 2 | Add **opt-in** transition behavior; test both React and Elmish | P1 |
| 2 | Publish a `ViewTransition` integration example | P1 |
| 3 | Explore additive `Router.useLocation()` / `useRoute()` | P2 |
| 3 | Add examples for Activity, Suspense, ErrorBoundary | P2 |
| 3 | Fix numeric-to-string `unbox` and reduce internal duplication | P2/P3 |
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

**Verification status:** The initial document was a static review. The stale-callback fix is now implemented and validated with `npm test` (19 passing tests), `dotnet build Feliz.Router.sln --configuration Release`, and `npm run build:demo`. A production-demo smoke check also passed initial rendering, Elmish navigation, pushState, popstate, and replaceState. DOM checks use jsdom rather than a real browser. Remaining snippets marked as proposed/illustrative are API designs or sketches, not validated patches.
