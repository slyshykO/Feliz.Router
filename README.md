# Feliz.Router [![Nuget](https://img.shields.io/nuget/v/Feliz.Router.svg?maxAge=0&colorB=brightgreen)](https://www.nuget.org/packages/Feliz.Router) [![Build status](https://ci.appveyor.com/api/projects/status/qwjte2b9vn43j9ff?svg=true)](https://ci.appveyor.com/project/Zaid-Ajaj/feliz-router)

A router component for React and Elmish that is focused, powerful and extremely easy to use.

Here is a full example in [Feliz](https://github.com/Zaid-Ajaj/Feliz)

```fs
module App

open Feliz
open Feliz.Router

[<ReactComponent>]
let Router() =
    let (currentUrl, updateUrl) = React.useState(Router.currentUrl())
    React.router [
        router.onUrlChanged updateUrl
        router.children [
            match currentUrl with
            | [ ] -> Html.h1 "Index"
            | [ "users" ] -> Html.h1 "Users page"
            | [ "users"; Route.Int userId ] -> Html.h1 (sprintf "User ID %d" userId)
            | otherwise -> Html.h1 "Not found"
        ]
    ]

open Browser.Dom

let root = ReactDOM.createRoot(document.getElementById "root")
root.render(Router())
```

Full Elmish example
```fs
module App

open Feliz
open Feliz.Router

type State = { CurrentUrl : string list }
type Msg = UrlChanged of string list

let init() = { CurrentUrl = Router.currentUrl() }
let update (UrlChanged segments) state = { state with CurrentUrl = segments }

let render state dispatch =
    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children [
            match state.CurrentUrl with
            | [ ] -> Html.h1 "Home"
            | [ "users" ] -> Html.h1 "Users page"
            | [ "users"; Route.Int userId ] -> Html.h1 (sprintf "User ID %d" userId)
            | _ -> Html.h1 "Not found"
        ]
    ]

Program.mkSimple init update render
|> Program.withReactSynchronous "root"
|> Program.run
```

### Installation

```
dotnet add package Feliz.Router
```

The package includes a single React component called `router` that you can use at the very top level of your application
```fs
React.router [
    router.onUrlChanged (UrlChanged >> dispatch)
    router.children [
        Html.h1 "App"
    ]
]
```
Where it has two primary properties
 - `router.onUrlChanged : string list -> unit` gets triggered when the url changes where it gives you the *url segments* to work with.
 - `router.children: ReactElement` the element to be rendered as the single child of the `router` component, usually here is where your root application `render` function goes.
 - `router.children: ReactElement list` overload to be rendered as the children of the `router`

```fs
let currentPage = Html.h1 "App"

React.router [
    router.onUrlChanged (UrlChanged >> dispatch)
    router.children [
        Html.div [
            Html.h1 "Using the router"
            currentPage
        ]
    ]
]
```
### `router.onUrlChanged` is everything

Routing in most applications revolves around having your application react to url changes, causing the current page to change and data to reload. Here is where `router.onUrlChanged` comes into play where it triggers when the url changes giving you the *cleaned url segments* as a list of strings. These are sample urls their corresposing url segments that get triggered as input of of `onUrlChanged`:
```fs
segment "#/" => [ ]
segment "#/home" => [ "home" ]
segment "#/home/settings" => [ "home"; "settings" ]
segment "#/users/1" => [ "users"; "1" ]
segment "#/users/1/details" => [ "users"; "1"; "details" ]

// with query string parameters
segment "#/users?id=1" => [ "users"; "?id=1" ]
segment "#/home/users?id=1" => [ "home"; "users"; "?id=1" ]
segment "#/users?id=1&format=json" => [ "users"; "?id=1&format=json" ]
```

The router listens to both `hashchange` and `popstate`, so normal hash links,
`location.hash` changes, and browser Back/Forward navigation are supported.
Native events notify once per changed route: Hash mode compares the fragment,
and Path mode compares the pathname plus query string. Fragment-only changes
therefore do not notify a Path router. Returning to an earlier route still notifies.

Explicit `Router.navigate(...)` and `Router.navigatePath(...)` calls continue to
notify even for the current URL, including `HistoryMode.ReplaceState`. The initial
notification on mount is also preserved.

### Optional route-reading hook

`Router.useRoute()` returns the current parsed `string list` and subscribes to URL
changes. Use it in a React function component or custom hook, called
unconditionally. It works independently of `React.router`:

```fsharp
[<ReactComponent>]
let App () =
    let route = Router.useRoute()
    Html.div [
        Html.button [
            prop.text "Open user 42"
            prop.onClick (fun _ -> Router.navigate("users", 42))
        ]
        match route with
        | [] | [ "home" ] -> Html.h1 "Home"
        | [ "users"; Route.Int id ] -> Html.h1 (sprintf "User %d" id)
        | _ -> Html.h1 "Not found"
    ]
```

Hash mode is the default, with the same parsing as `Router.currentUrl()`. For
pathname plus query string, use `Router.useRoute(RouteMode.Path)` and navigate
with `Router.navigatePath(...)`; its segments match `Router.currentPath()`.
Existing `Route` active patterns and encoded query handling apply to both modes.
The mode can change between renders.

The hook listens to `hashchange`, `popstate`, and the router's custom navigation
event. It uses a stable URL string snapshot and memoizes the parsed list. Events
for an unchanged selected URL—including explicit same-URL navigation—do not
request another render. `router.onUrlChanged` retains its separate same-URL
notification behavior. Each hook cleans up its listeners when unmounted.
Raw `history.pushState` / `replaceState` calls emit no event; use the Router
navigation functions to notify mounted readers.

This hook is **client-only**: it reads the browser location and does not supply a
server snapshot for SSR/hydration. Its external-store updates are synchronous,
even when navigation is wrapped in `React.startTransition`. For deferred page
updates, retaining a visible Suspense page, or `ViewTransition` animations, keep
React route state with `router.onUrlChanged` and the transition integration below.
See React's [`useSyncExternalStore` caveats](https://react.dev/reference/react/useSyncExternalStore#caveats).

Run `npm run start:demo` and open the
[standalone hook example](http://localhost:8080/?example=hook#/home), implemented
in [demo/RouteHook.fs](demo/RouteHook.fs).

### Parsing URL segments into `Page` definitions

Instead of using overly complicated parser combinators to parse a simple structure such as URL segments, the `Route` module includes a handful of convenient active patterns to use against these segments:
```fs
type Page =
    | Home
    | Users
    | User of id:int
    | NotFound

// string list -> Page
let parseUrl = function
    // matches #/ or #
    | [ ] ->  Page.Home
    // matches #/users or #/users/ or #users
    | [ "users" ] -> Page.Users
    // matches #/users/{userId}
    | [ "users"; Route.Int userId ] -> Page.User userId
    // matches #/users?id={userId} where userId is an integer
    | [ "users"; Route.Query [ "id", Route.Int userId ] ] -> Page.User userId
    // matches everything else
    | _ -> NotFound

[<ReactComponent>]
static member Router() =
    let (pageUrl, updateUrl) = React.useState(parseUrl(Router.currentUrl()))
    let currentPage =
        match pageUrl with
        | Home -> Html.h1 "Home"
        | Users -> Html.h1 "Users page"
        | User userId -> Html.h1 (sprintf "User ID %d" userId)
        | NotFound -> Html.h1 "Not Found"

    React.router [
        router.onUrlChanged (parseUrl >> updateUrl)
        router.children currentPage
    ]
```
Of course, you can define your own patterns to match against the route segments, just remember that you are working against simple string.

### Programmatic Navigation

Aside from listening to manual changes made to the URL by hand, the `React.router` element is able to listen to changes made programmatically from your code with `Router.navigate(...)`.
To use this function is as a *command* inside your `update` function the same functionality is exposed as `Cmd.navigate`.

The function `Router.navigate` (and `Cmd.navigate`) has the general syntax:
```
Router.navigate(segment1, segment2, ..., segmentN, [query string parameters], [historyMode])
```
Examples of the generated paths:
```fs
Router.navigate("users") => "#/users"
Router.navigate("users", "about") => "#/users/about"
Router.navigate("users", 1) => "#/users/1"
Router.navigate("users", 1, "details") => "#/users/1/details"
```
Examples of generated paths with query string parameters
```fs
Router.navigate("users", [ "id", 1 ]) => "#/user?id=1"
Router.navigate("users", [ "name", "john"; "married", "false" ]) => "#/users?name=john&married=false"
// paramters are encoded automatically
Router.navigate("search", [ "q", "whats up" ]) => @"#/search?q=whats%20up"
// Pushing a new history entry is the default bevahiour
Router.navigate("users", HistoryMode.PushState)
// to replace current history entry, use HistoryMode.ReplaceState
Router.navigate("users", HistoryMode.ReplaceState)
```

### Generating links

In addition to `Router.navigate(...)` you can also use the `Router.format(...)` if you only need to generate the string that can be used to set the `href` property of a link.

The function `Router.format` has a similar general syntax as `Router.navigate`:
```
Router.format(segment1, segment2, ..., segmentN, [query string parameters])
```
Examples of the generated paths:
```fs
Router.format("users") => "#/users"
Router.format("users", "about") => "#/users/about"
Router.format("users", 1) => "#/users/1"
Router.format("users", 1, "details") => "#/users/1/details"
```
Examples of generated paths with query string parameters
```fs
Router.format("users", [ "id", 1 ]) => "#/user?id=1"
Router.format("users", [ "name", "john"; "married", "false" ]) => "#/users?name=john&married=false"
// paramters are encoded automatically
Router.format("search", [ "q", "whats up" ]) => @"#/search?q=whats%20up"
```

Example of usage:
```fs
Html.a [
    prop.href (Router.format("users", ["id", 10]))
    prop.text "Single User link"
]
```

### Using Path routes without hash sign
The router by default prepends all generated routes with a hash sign (`#`), to omit the hash sign and use plain old paths, use `router.pathMode`
```fs
React.router [
    router.pathMode
    router.onUrlChanged (parseUrl >> PageChanged >> dispatch)
    router.children currentPage
]
```
Then refactor the application to use path-based functions rather than the default functions which are hash-based:
| Hash-based            | Path-based              |
| --------------------- | ----------------------- |
| `Router.currentUrl()` | `Router.currentPath()`  |
| `Router.format()`     | `Router.formatPath()`   |
| `Router.navigate()`   | `Router.navigatePath()` |
| `Cmd.navigate()`      | `Cmd.navigatePath()`    |

Using (anchor) `Html.a` tags using path mode can be problematic because they cause a full-refresh if they are not prefixed with the hash sign.
Still, you can use them with path mode routing by overriding the default behavior using the `prop.onClick` event handler to dispatch a
message which executes a `Cmd.navigatePath` command. It goes like this:
```fs
type Msg =
 | NavigateTo of string

let update msg state =
  match msg with
  | NavigateTo href -> state, Cmd.navigatePath(href)

let goToUrl (dispatch: Msg -> unit) (href: string) (e: MouseEvent) =
    // disable full page refresh
    e.preventDefault()
    // dispatch msg
    dispatch (NavigateTo href)

let render state dispatch =
  let href = Router.format("some-sub-path")
  Html.a [
    prop.text "Click me"
    prop.href href
    prop.onClick (goToUrl dispatch href)
  ]
```

### Demo application

Here is a full example in an Elmish program.

```fs
type State = { CurrentUrl : string list }

type Msg =
    | UrlChanged of string list
    | NavigateToUsers
    | NavigateToUser of int

let init() = { CurrentUrl = Router.currentUrl() }, Cmd.none

let update msg state =
    match msg with
    | UrlChanged segments -> { state with CurrentUrl = segments }, Cmd.none
    // notice here the use of the command Cmd.navigate
    | NavigateToUsers -> state, Cmd.navigate("users")
    // Router.navigate with query string parameters
    | NavigateToUser userId -> state, Cmd.navigate("users", [ "id", userId ])

let render state dispatch =

    let currentPage =
        match state.CurrentUrl with
        | [ ] ->
            Html.div [
                Html.h1 "Home"
                Html.button [
                    prop.text "Navigate to users"
                    prop.onClick (fun _ -> dispatch NavigateToUsers)
                ]
                Html.a [
                    prop.href (Router.format("users"))
                    prop.text "Users link"
                ]
            ]
        | [ "users" ] ->
            Html.div [
                Html.h1 "Users page"
                Html.button [
                    prop.text "Navigate to User(10)"
                    prop.onClick (fun _ -> dispatch (NavigateToUser 10))
                ]
                Html.a [
                    prop.href (Router.format("users", ["id", 10]))
                    prop.text "Single User link"
                ]
            ]

        | [ "users"; Route.Query [ "id", Route.Int userId ] ] ->
            Html.h1 (sprintf "Showing user %d" userId)

        | _ ->
            Html.h1 "Not found"

    React.router [
        router.onUrlChanged (UrlChanged >> dispatch)
        router.children currentPage
    ]
```
### Opt-in route transitions

Add `router.useTransitions true` to run `onUrlChanged` inside
[`React.startTransition`](https://react.dev/reference/react/startTransition):

```fsharp
[<ReactComponent>]
let App () =
    let currentUrl, setCurrentUrl = React.useState (Router.currentUrl ())
    let currentPage =
        match currentUrl with
        | [ "about" ] -> Html.h1 "About"
        | _ -> Html.h1 "Home"

    React.router [
        router.useTransitions true
        router.onUrlChanged setCurrentUrl
        router.children currentPage
    ]
```

The option defaults to `false`. URL/history changes and callback invocation remain
synchronous; React can defer the resulting render and keep an already revealed
Suspense page visible while the next page loads. The option also applies to the
initial notification and native navigation events. Existing deduplication and
listener cleanup remain unchanged. Use `router.pathMode` and `Router.currentPath()`
for the equivalent path-based application.

Only React updates scheduled inside the callback inherit its transition. Updates
after an `await`, timer, or queued dispatch need `React.startTransition` around
their actual state update. Keep controlled-input updates urgent. This option does
not move synchronous callback work to another thread or expose an `isPending` flag;
use `React.useTransition` in the application when you need pending UI.

#### Elmish rendering

`Cmd.navigate` may run while Elmish is already processing a message. In that case,
the router callback queues `UrlChanged`, and Elmish renders it after the callback's
transition has ended. Wrapping dispatch alone cannot cover that render.

The example's [TransitionHelpers.withRouteTransitions](demo/TransitionHelpers.fs)
starts a transition at the actual render boundary when the route changes:

```fsharp
Program.mkProgram init update view
|> Program.withReactSynchronous "root"
|> TransitionHelpers.withRouteTransitions
    (fun model -> model.UseTransitions)
    (fun model -> model.CurrentUrl)
|> Program.run
```

This helper is application/example code, not a new Feliz.Router API. Copy or adapt
it for your model. Apply it **after** `withReactSynchronous`; it preserves urgent
renders for unrelated model changes. `withReactBatched` defers rendering through
`requestAnimationFrame`, so wrapping that adapter's scheduling call is insufficient.
The [Elmish example](demo/Transitions.fs) uses the actual Fable.Elmish.React renderer.

### ViewTransition animations

With React/ReactDOM **19.3+**, wrap the page content in `React.ViewTransition`:

```fsharp
React.router [
    router.useTransitions true
    router.onUrlChanged setCurrentUrl
    router.children (
        React.ViewTransition [
            viewTransition.update "route-page"
            viewTransition.children [ currentPage ]
        ])
]
```

`ViewTransition` animates eligible React updates; an ordinary urgent state update
does not activate it. The router does not add an animation boundary or CSS itself.
The application controls styling and reduced motion, as shown in
[public/transitions.css](public/transitions.css). React manages transition names
and the browser API; do not call `document.startViewTransition` around these updates.
Browsers without that API continue to navigate without animation.

**Back/Forward limitation:** React intentionally skips View Transition animations
for legacy `popstate` updates to preserve browser scroll/form restoration. Back and
Forward still navigate correctly. Native hash links can follow this same path.
Animating those navigations requires separate Navigation API integration; this
change does not alter scroll restoration or add directional transition types.
See [React's router guidance](https://react.dev/reference/react/ViewTransition#building-view-transition-enabled-routers).

Run `npm run start:demo`, then open:

- [React state example](http://localhost:8080/?example=transitions#/home)
- [Elmish example](http://localhost:8080/?example=transitions-elmish#/home)

Both examples include an opt-out checkbox, push/replace navigation, native links,
Back/Forward, and a local text field outside the animated page. Reduced-motion
preferences disable animated durations. `npm run test:demo` builds and checks both
examples in Chrome with real animations, reduced motion, and the browser API disabled.

### More application examples

These examples compose React features in application code. Their boundaries,
resource cache, and retry policies live in `demo/`; they add no router options.
Launch them using the [running examples](#running-examples) instructions below.

#### Preserving tabs with Activity

[ActivityExample.fs](demo/ActivityExample.fs) keeps two `React.Activity` boundaries
in fixed positions and changes their `ActivityMode.Visible` / `ActivityMode.Hidden`
values from `Router.useRoute()`. Write a draft and add points in each tab, then
switch tabs or use Back/Forward: each page retains its own component state.

Hidden pages keep their DOM and state, but React cleans up their effects and
reconnects them when revealed. The example's ArrowUp subscription demonstrates
this: only the visible tab receives points. This requires React 19.2+. State does
not survive a reload, and hidden DOM can still need explicit cleanup for media
or other side effects. Keep the retained set bounded; the example has two fixed
pages, with no global route cache. See React's [Activity reference](https://react.dev/reference/react/Activity).

#### Loading pages with Suspense

[AsyncExamples.SuspenseApp](demo/AsyncExamples.fs) passes a cached Promise to a
child that reads it with `React.use'`, inside `React.Suspense`. Sample requests
take 1.2 seconds and make no network calls. The cache lives outside components;
page state holds the selected Promise so rerenders and suspended retries reuse
it. A loaded report can be revisited without another loading delay.

With the checkbox off, navigation displays the loading fallback. With it on,
`router.useTransitions true` keeps the already visible page until its replacement
is ready. The surrounding draft input stays usable in both cases. An initial
deep link can still show the fallback because there is no previously revealed
page to retain. Leaving a pending page does not navigate back when its request
finishes. See React's [`use` reference](https://react.dev/reference/react/use).

The fixed sample cache is cleared by a full page reload. Real applications should
use a Suspense-compatible data layer with deliberate cache lifetime, invalidation,
and cancellation policies; creating a new Promise on every render is unsuitable.

#### Recovering pages with ErrorBoundary

[AsyncExamples.ErrorBoundaryApp](demo/AsyncExamples.fs) wraps only the page in
Feliz's `React.ErrorBoundary`, leaving navigation and the draft input available.
Open the unavailable report to reject a Promise, or use **Simulate rendering
error** on a loaded report to trigger a child render failure.

**Try again** replaces the cached Promise, updates page state, and calls the
fallback's `reset()` function. Resetting without replacing a rejected Promise
would immediately fail again. `resetKeys` contains the report ID and Promise
reference, so navigating to another report also clears the fallback; an arbitrary
parent rerender does not. Expected demo failures are logged by React in the console.

Error boundaries handle render failures and rejected Promises read through
`React.use'`. They do not handle arbitrary event-handler or asynchronous callback
errors, SSR errors, or errors in their own fallback. The simulation button sets
state; the child throws during its next render. See React's
[error boundary guidance](https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary).

### Migrating from 2.x to 3.x

The 3.x release refactored the API to be more in line with how Feliz libraries are built as well as using latest features from React like functional components to implement the router itself. This release also made it easy to work with the router from a React-only applications and not just from Elmish based apps.

To migrate your router to latest version, here are the required changes:
  - `Router.router` becomes `React.router`
  - `Router.onUrlChanged` becomes `router.onUrlChanged`
  - `Router.application` becomes `router.children`
  - `Router.navigate` becomes `Cmd.navigate` for the Elmish variant and `Router.navigate() : unit` for the React variant

The rest of the implementation and API is kept as is. If you have any questions or run into problems, please let us know!

### Migrating to v4

Starting from Feliz.Router v4, it will only work with Fable compiler v4+ and Feliz 2.x+

If you are using Fable compiler v3, then you should use Feliz.Router v3.10 (latest) which still relies on Feliz v1.68 for Fable v3 compatibility.

### Running examples

Complete the [development setup](#development), then run this command from the
repository root:

```bash
npm run start:demo
```

On Windows PowerShell, use `npm.cmd run start:demo` if execution policy blocks
`npm.ps1`. Keep the terminal running and wait for compilation to finish, then
open an example:

| Example | URL |
| --- | --- |
| Original Elmish hash routing | [localhost:8080/#/](http://localhost:8080/#/) |
| Standalone `Router.useRoute()` hook | [localhost:8080/?example=hook#/home](http://localhost:8080/?example=hook#/home) |
| React state with ViewTransition | [localhost:8080/?example=transitions#/home](http://localhost:8080/?example=transitions#/home) |
| Elmish with ViewTransition | [localhost:8080/?example=transitions-elmish#/home](http://localhost:8080/?example=transitions-elmish#/home) |
| Activity tabs with preserved state | [localhost:8080/?example=activity#/work](http://localhost:8080/?example=activity#/work) |
| Suspense loading and transitions | [localhost:8080/?example=suspense#/home](http://localhost:8080/?example=suspense#/home) |
| ErrorBoundary recovery and retry | [localhost:8080/?example=errors#/home](http://localhost:8080/?example=errors#/home) |

All examples share the same server. The `?example=...` query parameter selects
the application in [demo/Main.fs](demo/Main.fs); the `#/...` fragment selects a
page within that application. Open another example link to switch applications.
The server watches source changes; stop it with **Ctrl+C**.

**Path example limitation:** The older Path example is selected by
[`?example=path`](http://localhost:8080/?example=path), but its route parser also
receives this query parameter and currently renders **Not Found**. Its example
selection needs fixing before it can be launched normally. This is a demo issue;
the library's Path routing is covered by the integration tests.

### Development

The `update-deps` branch of this fork uses Feliz 3.4, React/ReactDOM 19.3,
Fable.Core 5.6, FSharp.Core 10, and Fable.Elmish.React 5.6. The library still
targets `netstandard2.1`; consumers need the updated package dependencies.
The router uses `React.useEffectEvent`, requiring React/ReactDOM 19.2 or newer.

Install the .NET 10 LTS SDK specified in `global.json` (10.0.404, with patch roll-forward)
and a supported Node.js version: 22.x from 22.22.2, 24.x from 24.15.0, or 26+
(see `engines` in `package.json`). CI uses Node.js 26. The local tool manifest pins
Fable, Paket, and FAKE. On Windows PowerShell, use `npm.cmd` if execution policy blocks `npm.ps1`.

```bash
# restore the checked-in dependency versions
dotnet tool restore
dotnet paket restore
npm ci
# run tests with jsdom
npm test

# run the same suite in an installed Chrome browser
npm run test:browser

# build and verify the hook, transition, and application examples in Chrome
npm run test:demo

# run the demo application in watch mode
npm run start:demo

# build the demo application
npm run build:demo
```

Tests use Fable.Mocha 2.17 with Mocha 12.0.3. Fable.Mocha's embedded npm metadata
still declares Mocha 8; this fork validates the newer runner with its test suite.
Check the F# binding and JavaScript runtime together when upgrading either.
Mounted-router tests use React `act` to cover callback and routing-mode updates,
hash links, Back/Forward, native-event deduplication, and listener cleanup,
including Strict Mode. `tests/IntegrationApp.fs` adds rendered React state and
Elmish program fixtures: button clicks exercise `Router.navigate`/`Cmd.navigate`
and their Path variants, with assertions on page content, history entries, and
dispatched URL changes. Tests run sequentially because they share browser history.
Fable compiles them into `tests/bin/fable/` so demo builds cannot overwrite their
generated router or introduce a second copy of the Fable runtime into the tests.
`tests/TransitionApp.fs` adds controlled Suspense fixtures for transition opt-in,
urgent interactions, callback/option changes, and Elmish dispatch/render timing.
The demo browser runner additionally exercises the actual Fable.Elmish.React renderer,
browser View Transition API, and standalone hook. `tests/application-examples.cjs`
checks Activity state/effect lifecycles, Suspense caching and transition behavior,
and ErrorBoundary retries and route-key resets. Playwright's clock controls the
sample request timers; only the two deliberately triggered error messages are
allowed in these tests. Screenshots are saved in `tests/bin/demo-browser/`.

`npm run test:browser` uses Playwright Core and an installed Chrome; it does not
download a browser. Set `BROWSER_CHANNEL=msedge` to use installed Edge, or set
`BROWSER_EXECUTABLE_PATH` to a Chromium-compatible executable. The command bundles
the same F# suite into `tests/bin/browser/`, serves it on a temporary local port,
and fails on test failures or browser runtime/console errors. Fable.Mocha's browser
runner waits one second before each test, so this run is slower than `npm test`.

Use `dotnet build Feliz.Router.sln --configuration Release` to compile all projects,
`dotnet build build/Build.fsproj --configuration Release` to check release tooling,
and `dotnet pack src/Feliz.Router.fsproj --configuration Release --no-build` to
create the NuGet package locally. The demo runs at `http://localhost:8080`.
