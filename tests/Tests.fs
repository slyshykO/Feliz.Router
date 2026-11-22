module Tests

open Browser.Dom
open Fable.Core
open Feliz
open Feliz.Router
open Fable.Mocha

[<Import("act", "react")>]
let private reactAct (callback: unit -> JS.Promise<unit>) : obj = jsNative

// React returns a thenable; Fable's async adapter needs a native Promise.
[<Emit("Promise.resolve($0)")>]
let private resolveAct (thenable: obj) : JS.Promise<unit> = jsNative

let private actAsync action =
    reactAct action |> resolveAct |> Async.AwaitPromise

let private act action =
    actAsync (fun () ->
        action ()
        JS.Constructors.Promise.resolve ())

// Wait for browser-generated events instead of guessing how long navigation takes.
let private actOnWindowEvent eventName action =
    actAsync (fun () ->
        Async.FromContinuations(fun (success, failure, _) ->
            let mutable timeout = None

            let rec onEvent (_: Browser.Types.Event) =
                cleanup ()
                success ()

            and cleanup () =
                window.removeEventListener (eventName, onEvent)
                timeout |> Option.iter window.clearTimeout

            window.addEventListener (eventName, onEvent)

            timeout <-
                Some(
                    window.setTimeout (
                        (fun () ->
                            cleanup ()
                            failure (System.Exception(sprintf "Timed out waiting for %s" eventName))),
                        1000
                    )
                )

            try
                action ()
            with error ->
                cleanup ()
                failure error)
        |> Async.StartAsPromise)

let private withContainer initialUrl (run: Browser.Types.HTMLElement -> IReactRoot -> Async<unit>) =
    async {
        window.history.replaceState (null, "", initialUrl)
        let container = document.createElement "div"
        document.body.appendChild container |> ignore
        let root = ReactDOM.createRoot container
        let! result = Async.Catch(run container root)
        do! act root.unmount
        container.remove ()
        window.history.replaceState (null, "", "/")

        match result with
        | Choice1Of2() -> ()
        | Choice2Of2 error -> return raise error
    }

let private withRoot initialUrl run =
    withContainer initialUrl (fun _ root -> run root)

let private elementByTestId (container: Browser.Types.HTMLElement) testId =
    let element = container.querySelector (sprintf "[data-testid='%s']" testId)
    Expect.isNotNull element (sprintf "The application should render '%s'" testId)
    unbox<Browser.Types.HTMLElement> element

let private dispatchWindowEvent eventName =
    let ev = document.createEvent "Event"
    ev.initEvent (eventName, false, false)
    window.dispatchEvent ev |> ignore

let private dispatchPopstate () = dispatchWindowEvent "popstate"

type test =
    static member pass() = Expect.isTrue true "It must be true"
    static member fail() = Expect.isTrue false "It must be false"

    //static member pass() = Expect.isTrue true
    //static member fail() = Expect.isTrue false
    static member failwith x = failwith x
    static member passWith x = Expect.isTrue true

let itCompiles () = Router.navigate "users"

let routerTests =
    testList
        "Router tests"
        [
            for useElmish in [ false; true ] do
                for pathMode in [ false; true ] do
                    for strictMode in [ false; true ] do
                        let appName = if useElmish then "Elmish" else "React state"
                        let modeName = if pathMode then "Path" else "Hash"
                        let strictName = if strictMode then " with Strict Mode" else ""

                        testCaseAsync (sprintf "%s renders navigation in %s mode%s" appName modeName strictName)
                        <| async {
                            let notifications = ResizeArray<string list>()
                            let mutable stop = ignore
                            let initialRoute = "search?q=Jane%20Doe"

                            let initialUrl =
                                if pathMode then
                                    "/" + initialRoute
                                else
                                    "/#/" + initialRoute

                            try
                                do!
                                    withContainer initialUrl (fun container root ->
                                        async {
                                            do!
                                                act (fun () ->
                                                    if useElmish then
                                                        stop <-
                                                            IntegrationApp.startElmish
                                                                root
                                                                pathMode
                                                                strictMode
                                                                notifications
                                                    else
                                                        IntegrationApp.mountReact
                                                            root
                                                            pathMode
                                                            strictMode
                                                            notifications)

                                            let expectPage title count =
                                                Expect.equal
                                                    (elementByTestId container "page").textContent
                                                    title
                                                    "The rendered page should follow the current URL"

                                                Expect.equal
                                                    notifications.Count
                                                    count
                                                    "Navigation should update application state exactly once per route change"

                                            let click testId =
                                                (elementByTestId container testId).click()

                                            let mountCount = if strictMode then 2 else 1
                                            let initialHistoryLength = window.history.length
                                            expectPage "Search: Jane Doe" mountCount

                                            Expect.equal
                                                notifications.[0]
                                                [ "search"; "?q=Jane%20Doe" ]
                                                "The initial notification should preserve the query for the route parser"

                                            do! act (fun () -> click "push-user")
                                            expectPage "User 7" (mountCount + 1)

                                            Expect.equal
                                                window.history.length
                                                (initialHistoryLength + 1)
                                                "PushState should add a history entry"

                                            do! act (fun () -> click "replace-home")
                                            expectPage "Home" (mountCount + 2)

                                            Expect.equal
                                                window.history.length
                                                (initialHistoryLength + 1)
                                                "ReplaceState should replace the current entry without adding one"

                                            let historyEvent = if pathMode then "popstate" else "hashchange"
                                            do! actOnWindowEvent historyEvent window.history.back
                                            expectPage "Search: Jane Doe" (mountCount + 3)
                                            do! actOnWindowEvent historyEvent window.history.forward
                                            expectPage "Home" (mountCount + 4)

                                            do! actOnWindowEvent "hashchange" (fun () -> click "native-link")

                                            if pathMode then
                                                expectPage "Home" (mountCount + 4)
                                            else
                                                expectPage "User 8" (mountCount + 5)

                                            do!
                                                act (fun () ->
                                                    dispatchWindowEvent "popstate"
                                                    dispatchWindowEvent "hashchange")

                                            let finalCount = mountCount + (if pathMode then 4 else 5)

                                            Expect.equal
                                                notifications.Count
                                                finalCount
                                                "Duplicate native events must not dispatch another application update"
                                        })

                                // Keep the Elmish program alive until after this check so a leaked
                                // listener cannot be hidden by terminating its dispatch loop early.
                                let countBeforeUnmountedNavigation = notifications.Count

                                do!
                                    act (fun () ->
                                        if pathMode then
                                            Router.navigatePath "after-unmount"
                                        else
                                            Router.navigate "after-unmount")

                                Expect.equal
                                    notifications.Count
                                    countBeforeUnmountedNavigation
                                    "The application should stop receiving navigation after its root unmounts"
                            finally
                                stop ()
                        }

            for pathMode in [ false; true ] do
                for strictMode in [ false; true ] do
                    let modeName = if pathMode then "Path" else "Hash"
                    let strictName = if strictMode then " with Strict Mode" else ""

                    testCaseAsync (sprintf "%s router uses the latest callback%s and cleans up" modeName strictName)
                    <| async {
                        let notifications = ResizeArray<int * string list>()

                        let url page =
                            if pathMode then "/" + page else "/#/" + page

                        let navigate (page: string) =
                            if pathMode then
                                Router.navigatePath page
                            else
                                Router.navigate page

                        do!
                            withRoot (url "initial") (fun root ->
                                async {
                                    let render version =
                                        let element =
                                            React.router
                                                [
                                                    if pathMode then router.pathMode else router.hashMode
                                                    router.onUrlChanged (fun segments ->
                                                        notifications.Add(version, segments))
                                                ]

                                        root.render (if strictMode then React.StrictMode [ element ] else element)

                                    do! act (fun () -> render 1)
                                    let mountCount = if strictMode then 2 else 1

                                    Expect.equal
                                        (List.ofSeq notifications)
                                        (List.replicate mountCount (1, [ "initial" ]))
                                        "Mount should report the initial URL, including Strict Mode effect replay"

                                    notifications.Clear()

                                    for version in [ 2; 3 ] do
                                        do! act (fun () -> render version)

                                        Expect.isEmpty
                                            notifications
                                            "Changing callback props should not notify on its own"

                                        do! act (fun () -> navigate "programmatic")

                                        do!
                                            act (fun () ->
                                                window.history.replaceState (null, "", url "history")
                                                dispatchPopstate ())

                                        do!
                                            act (fun () ->
                                                window.history.replaceState (null, "", url "hash-event")
                                                dispatchWindowEvent "hashchange")

                                        Expect.equal
                                            (List.ofSeq notifications)
                                            [
                                                (version, [ "programmatic" ])
                                                (version, [ "history" ])
                                                (version, [ "hash-event" ])
                                            ]
                                            "All three navigation listeners should call only the latest callback once"

                                        notifications.Clear()
                                })

                        do!
                            act (fun () ->
                                navigate "after-unmount"
                                window.history.replaceState (null, "", url "popstate-after-unmount")
                                dispatchPopstate ()
                                window.history.replaceState (null, "", url "hashchange-after-unmount")
                                dispatchWindowEvent "hashchange")

                        Expect.isEmpty
                            notifications
                            "Unmount should remove all three listeners, including after prop changes"
                    }

            testCaseAsync "Router uses the latest routing mode in both navigation listeners"
            <| async {
                let notifications = ResizeArray<string list>()
                // Keep the callback identity unchanged to isolate routing-mode prop updates.
                let onUrlChanged segments = notifications.Add segments

                do!
                    withRoot "/path?source=path#/hash?source=hash" (fun root ->
                        async {
                            let render pathMode =
                                root.render (
                                    React.router
                                        [
                                            if pathMode then router.pathMode else router.hashMode
                                            router.onUrlChanged onUrlChanged
                                        ]
                                )

                            do! act (fun () -> render false)

                            Expect.equal
                                (List.ofSeq notifications)
                                [ [ "hash"; "?source=hash" ] ]
                                "The initial notification should read the hash route"

                            notifications.Clear()

                            for pathMode in [ true; false ] do
                                do! act (fun () -> render pathMode)
                                Expect.isEmpty notifications "Changing the routing mode should not notify on its own"

                                do!
                                    act (fun () ->
                                        window.history.replaceState (null, "", "/path?source=path#/hash?source=hash")
                                        dispatchPopstate ())

                                do! act (fun () -> Router.navigate "custom")

                                let expected =
                                    if pathMode then
                                        [ [ "path"; "?source=path" ]; [ "path"; "?source=path" ] ]
                                    else
                                        [ [ "hash"; "?source=hash" ]; [ "custom" ] ]

                                Expect.equal
                                    (List.ofSeq notifications)
                                    expected
                                    "Both listeners should interpret the URL using the current routing mode"

                                notifications.Clear()
                        })
            }

            testCaseAsync "Router supports adding, removing, and restoring the callback after mount"
            <| async {
                let notifications = ResizeArray<string list>()
                let onUrlChanged segments = notifications.Add segments

                do!
                    withRoot "/#/initial" (fun root ->
                        async {
                            let render withCallback =
                                root.render (
                                    React.router
                                        [
                                            if withCallback then
                                                router.onUrlChanged onUrlChanged
                                        ]
                                )

                            do! act (fun () -> render false)

                            for withCallback in [ true; false; true ] do
                                do! act (fun () -> render withCallback)

                                Expect.isEmpty
                                    notifications
                                    "Updating the optional callback should not notify on its own"

                                do! act (fun () -> Router.navigate "next")
                                do! act dispatchPopstate
                                let expected = if withCallback then [ [ "next" ] ] else []

                                Expect.equal
                                    (List.ofSeq notifications)
                                    expected
                                    "Use the current callback, ignore absent callbacks, and suppress the redundant native event"

                                notifications.Clear()
                        })
            }

            testCaseAsync "Hash router handles hashchange without popstate"
            <| async {
                let notifications = ResizeArray<string list>()

                do!
                    withRoot "/#/initial" (fun root ->
                        async {
                            do! act (fun () -> root.render (React.router [ router.onUrlChanged notifications.Add ]))
                            notifications.Clear()

                            do!
                                act (fun () ->
                                    window.history.replaceState (null, "", "/#/next?query=1")
                                    dispatchWindowEvent "hashchange")

                            Expect.equal
                                (List.ofSeq notifications)
                                [ [ "next"; "?query=1" ] ]
                                "A hashchange alone must notify the router in modern browsers"
                        })
            }

            for pathMode in [ false; true ] do
                let modeName = if pathMode then "Path" else "Hash"

                let url page =
                    if pathMode then "/" + page else "/#/" + page

                let navigate (page: string) (mode: HistoryMode) =
                    if pathMode then
                        Router.navigatePath (page, mode)
                    else
                        Router.navigate (page, mode)

                let render (root: IReactRoot) callback =
                    root.render (
                        React.router
                            [
                                if pathMode then router.pathMode else router.hashMode
                                router.onUrlChanged callback
                            ]
                    )

                testCaseAsync (sprintf "%s router deduplicates native events without losing return visits" modeName)
                <| async {
                    let notifications = ResizeArray<string list>()

                    do!
                        withRoot (url "initial") (fun root ->
                            async {
                                do! act (fun () -> render root notifications.Add)
                                notifications.Clear()

                                for page, events in
                                    [
                                        "first", [ "popstate"; "hashchange"; "popstate" ]
                                        "second", [ "hashchange"; "popstate"; "hashchange" ]
                                        "first", [ "popstate"; "hashchange" ]
                                    ] do
                                    do!
                                        act (fun () ->
                                            window.history.replaceState (null, "", url page)
                                            events |> List.iter dispatchWindowEvent)

                                Expect.equal
                                    (List.ofSeq notifications)
                                    [ [ "first" ]; [ "second" ]; [ "first" ] ]
                                    "Notify each changed route once in either event order, including a return to a previous URL"
                            })
                }

                testCaseAsync (sprintf "%s router preserves explicit navigation to the current URL" modeName)
                <| async {
                    let notifications = ResizeArray<string list>()

                    do!
                        withRoot (url "same") (fun root ->
                            async {
                                do! act (fun () -> render root notifications.Add)
                                notifications.Clear()

                                for mode in [ HistoryMode.PushState; HistoryMode.ReplaceState ] do
                                    do! act (fun () -> navigate "same" mode)

                                    Expect.equal
                                        (List.ofSeq notifications)
                                        [ [ "same" ] ]
                                        "Each explicit navigate call should still notify, including for an unchanged URL"

                                    do!
                                        act (fun () ->
                                            dispatchPopstate ()
                                            dispatchWindowEvent "hashchange")

                                    Expect.equal
                                        (List.ofSeq notifications)
                                        [ [ "same" ] ]
                                        "Native events for the route just reported by custom navigation should be ignored"

                                    notifications.Clear()
                            })
                }

                testCaseAsync (sprintf "%s router handles native Back and Forward once per route" modeName)
                <| async {
                    let notifications = ResizeArray<string list>()

                    do!
                        withRoot (url "initial") (fun root ->
                            async {
                                do! act (fun () -> render root notifications.Add)
                                notifications.Clear()

                                do!
                                    act (fun () ->
                                        navigate "first" HistoryMode.PushState
                                        navigate "second" HistoryMode.PushState)

                                let eventName = if pathMode then "popstate" else "hashchange"
                                do! actOnWindowEvent eventName window.history.back
                                do! actOnWindowEvent eventName window.history.forward

                                Expect.equal
                                    (List.ofSeq notifications)
                                    [ [ "first" ]; [ "second" ]; [ "first" ]; [ "second" ] ]
                                    "Programmatic navigation and browser-generated history events should each report one route"
                            })
                }

            testCaseAsync "Hash router handles normal hash links and location.hash assignments"
            <| async {
                let notifications = ResizeArray<string list>()

                do!
                    withRoot "/#/initial" (fun root ->
                        async {
                            do! act (fun () -> root.render (React.router [ router.onUrlChanged notifications.Add ]))
                            notifications.Clear()
                            let link = document.createElement "a"
                            link.setAttribute ("href", "#/linked")
                            document.body.appendChild link |> ignore

                            try
                                do! actOnWindowEvent "hashchange" link.click

                                do!
                                    actOnWindowEvent "hashchange" (fun () ->
                                        window.location.hash <- "#/assigned?query=1")

                                Expect.equal
                                    (List.ofSeq notifications)
                                    [ [ "linked" ]; [ "assigned"; "?query=1" ] ]
                                    "Native fragment navigation should notify once per changed hash"
                            finally
                                link.remove ()
                        })
            }

            testCaseAsync "Path router ignores fragment-only changes but observes query changes"
            <| async {
                let notifications = ResizeArray<string list>()

                do!
                    withRoot "/page?query=1#first" (fun root ->
                        async {
                            do!
                                act (fun () ->
                                    root.render (
                                        React.router [ router.pathMode; router.onUrlChanged notifications.Add ]
                                    ))

                            notifications.Clear()
                            do! actOnWindowEvent "hashchange" (fun () -> window.location.hash <- "#second")
                            Expect.isEmpty notifications "A fragment-only navigation does not change a path route"

                            do!
                                act (fun () ->
                                    window.history.replaceState (null, "", "/page?query=2#second")
                                    dispatchPopstate ()
                                    dispatchWindowEvent "hashchange")

                            Expect.equal
                                (List.ofSeq notifications)
                                [ [ "page"; "?query=2" ] ]
                                "The query string is part of a path route and must not be deduplicated away"
                        })
            }

            testCaseAsync "Hash router deduplicates by fragment including its query string"
            <| async {
                let notifications = ResizeArray<string list>()

                do!
                    withRoot "/page?outer=1#/route?inner=1" (fun root ->
                        async {
                            do! act (fun () -> root.render (React.router [ router.onUrlChanged notifications.Add ]))
                            notifications.Clear()

                            do!
                                act (fun () ->
                                    window.history.replaceState (null, "", "/other?outer=2#/route?inner=1")
                                    dispatchPopstate ())

                            Expect.isEmpty notifications "Path and outer query changes do not change a hash route"
                            do! actOnWindowEvent "hashchange" (fun () -> window.location.hash <- "#/route?inner=2")

                            Expect.equal
                                (List.ofSeq notifications)
                                [ [ "route"; "?inner=2" ] ]
                                "A query change inside the fragment is a genuine hash route change"
                        })
            }

            testCaseAsync "Native event deduplication preserves navigation triggered by a callback"
            <| async {
                let notifications = ResizeArray<string list>()

                do!
                    withRoot "/#/initial" (fun root ->
                        async {
                            do!
                                act (fun () ->
                                    root.render (
                                        React.router
                                            [
                                                router.onUrlChanged (fun segments ->
                                                    notifications.Add segments

                                                    if segments = [ "redirect" ] then
                                                        Router.navigate "destination")
                                            ]
                                    ))

                            notifications.Clear()

                            do!
                                act (fun () ->
                                    window.history.replaceState (null, "", "/#/redirect")
                                    dispatchPopstate ()
                                    dispatchWindowEvent "hashchange")

                            Expect.equal
                                (List.ofSeq notifications)
                                [ [ "redirect" ]; [ "destination" ] ]
                                "Remember a route before notifying so a callback-triggered navigation is not overwritten"
                        })
            }

            testCaseAsync "Native event deduplication is local to each mounted router"
            <| async {
                let first = ResizeArray<string list>()
                let second = ResizeArray<string list>()

                do!
                    withRoot "/#/initial" (fun root ->
                        async {
                            do!
                                act (fun () ->
                                    root.render (
                                        React.Fragment
                                            [
                                                React.router [ router.onUrlChanged first.Add ]
                                                React.router [ router.onUrlChanged second.Add ]
                                            ]
                                    ))

                            first.Clear()
                            second.Clear()

                            do!
                                act (fun () ->
                                    window.history.replaceState (null, "", "/#/next")
                                    dispatchPopstate ()
                                    dispatchWindowEvent "hashchange")

                            Expect.equal
                                (List.ofSeq first)
                                [ [ "next" ] ]
                                "The first router should receive the changed route once"

                            Expect.equal
                                (List.ofSeq second)
                                [ [ "next" ] ]
                                "The second router should independently receive it once"
                        })
            }

            testCase "Route.Int works"
            <| fun _ ->
                match [ "5"; "-1"; "" ] with
                | [ Route.Int 5; Route.Int -1; "" ] -> test.pass ()
                | [ Route.Int 5; Route.Int -1; Route.Int value ] -> test.failwith (string value)
                | _ -> test.fail ()

            testCase "Router.urlSegments works"
            <| fun _ ->
                [
                    "", []
                    "#", []
                    "#/", []
                    "#/?", []
                    "#?", []
                    "#home", [ "home" ]
                    "#home/", [ "home" ]
                    "#/home", [ "home" ]
                    "#/home/", [ "home" ]
                    "#/home/users", [ "home"; "users" ]
                    "#/home/users/", [ "home"; "users" ]
                    "#/home/users/settings", [ "home"; "users"; "settings" ]
                    "#/home/users/1", [ "home"; "users"; "1" ]
                    "#/users?id=1", [ "users"; "?id=1" ]
                    @"#/search?q=whats%20up", [ "search"; @"?q=whats%20up" ]
                    "#/?token=jwt", [ "?token=jwt" ]
                    "#?token=jwt", [ "?token=jwt" ]
                    "#?pretty", [ "?pretty" ]
                    "/", []
                    "/?", []
                    "?", []
                    "home", [ "home" ]
                    "home/", [ "home" ]
                    "/home", [ "home" ]
                    "/home/", [ "home" ]
                    "/home/users", [ "home"; "users" ]
                    "/home/users/", [ "home"; "users" ]
                    "/home/users/settings", [ "home"; "users"; "settings" ]
                    "/home/users/1", [ "home"; "users"; "1" ]
                    "/users?id=1", [ "users"; "?id=1" ]
                    @"/search?q=whats%20up", [ "search"; @"?q=whats%20up" ]
                    "/?token=jwt", [ "?token=jwt" ]
                    "?token=jwt", [ "?token=jwt" ]
                    "?pretty", [ "?pretty" ]
                ]
                |> List.iter (fun (input, output) ->
                    Expect.equal
                        (Router.urlSegments input RouteMode.Hash)
                        output
                        (sprintf "Input of %s should output %A" input output))

            testCase "RouteMode affects how the URL segments are cleaned up"
            <| fun _ ->
                ("/some/path#", RouteMode.Hash)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [] "Hash at the end means route starts there"

                ("/Feliz.MaterialUI/#", RouteMode.Hash)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [] "Hash at the end means route starts there"

                ("/Feliz.MaterialUI#", RouteMode.Hash)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [] "Hash at the end means route starts there"

                ("/some/path#/", RouteMode.Hash)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [] "Hash at the end means route starts there"

                ("/some/path#", RouteMode.Path)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [ "some"; "path" ] "Path segments are read correctly"

                ("/Feliz.MaterialUI#", RouteMode.Path)
                ||> Router.urlSegments
                |> fun output -> Expect.equal output [ "Feliz.MaterialUI" ] "Path segments are read correctly"

            testCase "Router.urlSegments decodes URL segments"
            <| fun _ ->
                let hashInput = "#/Hello%20World"
                let pathInput = "/Hello%20World"
                let expected = [ "Hello World" ]
                Expect.equal expected (Router.urlSegments hashInput RouteMode.Hash) "They are equal"
                Expect.equal expected (Router.urlSegments pathInput RouteMode.Path) "They are equal"

            testCase "Route.Query works"
            <| fun _ ->
                match [ "users"; "?id=1" ] with
                | [ "users"; Route.Query [ "id", "1" ] ] -> test.pass ()
                | otherwise -> test.fail ()

                match [ "users"; "?id=1" ] with
                | [ "users"; Route.Query [ "id", Route.Int userId ] ] -> Expect.equal userId 1 "They are equal"
                | otherwise -> test.fail ()

                match [ "users"; "?id=1&limit=5" ] with
                | [ "users"; Route.Query [ "id", Route.Int userId; "limit", Route.Int limit ] ] ->
                    Expect.equal userId 1 "They are equal"
                    Expect.equal limit 5 "They are equal"
                | otherwise -> test.fail ()

            testCase "Boolean query string parameters"
            <| fun _ ->
                match "?value=true" with
                | Route.Query [ "value", Route.Bool true ] -> test.pass ()
                | otherwise -> test.fail ()

                match "?value=1" with
                | Route.Query [ "value", Route.Bool true ] -> test.pass ()
                | otherwise -> test.fail ()

                match "?value=0" with
                | Route.Query [ "value", Route.Bool false ] -> test.pass ()
                | otherwise -> test.fail ()

                match "?value=false" with
                | Route.Query [ "value", Route.Bool false ] -> test.pass ()
                | otherwise -> test.fail ()

                match "?users=all&pretty" with
                | Route.Query [ "users", "all"; "pretty", "" ] -> test.pass ()
                | otherwise -> test.fail ()

            testCase "Double and decimal Route"
            <| fun _ ->
                match "?lng=12.12411241&lat=2.3451241" with
                | Route.Query [ "lng", Route.Decimal 12.12411241M; "lat", Route.Decimal 2.3451241M ] -> test.pass ()
                | otherwise -> test.fail ()

                match "?lng=12.12&lat=2.345" with
                | Route.Query [ "lng", Route.Number 12.12; "lat", Route.Number 2.345 ] -> test.pass ()
                | otherwise -> test.fail ()

            testCase "encodeQueryString for a single argument"
            <| fun _ ->
                let input = [ ("id", "1") ]
                let expected = "?id=1"
                let actual = Router.encodeQueryString input
                Expect.equal actual expected "They are equal"

            testCase "encodeQueryString for an empty map"
            <| fun _ ->
                let input = []
                let expected = ""
                let actual = Router.encodeQueryString input
                Expect.equal actual expected "They are equal"

            testCase "encodeQueryString for multiple arguments"
            <| fun _ ->
                let input = [ "id", "1"; "limit", "5" ]
                let expected = "?id=1&limit=5"
                let actual = Router.encodeQueryString input
                Expect.equal actual expected "They are equal"

            testCase "encode segments works"
            <| fun _ ->
                [
                    [ "users" ], "#/users"
                    [ "#/home" ], "#/home"
                    [ "#/hello?value=1" ], "#/hello?value=1"
                    [ "#/hello/home" ], "#/hello/home"
                    [ "#about" ], "#/about"
                    [ "users"; "home" ], "#/users/home"
                    [ "#one"; "two" ], "#/one/two"
                    [ "users"; "1" ], "#/users/1"
                    [ "users" + Router.encodeQueryString [ ("id", "1") ] ], "#/users?id=1"
                    [ "search" + Router.encodeQueryString [ ("q", "whats up") ] ], @"#/search?q=whats%20up"
                    [ "products" + Router.encodeQueryStringInts [ ("id", 1) ] ], "#/products?id=1"
                    [ "users" + Router.encodeQueryString [] ], "#/users"
                ]
                |> List.iter (fun (input, output) ->
                    Expect.equal (Router.encodeParts input RouteMode.Hash) output "They are equal")

                [
                    [ "users" ], "/users"
                    [ "/home" ], "/home"
                    [ "/hello?value=1" ], "/hello?value=1"
                    [ "/hello/home" ], "/hello/home"
                    [ "about" ], "/about"
                    [ "users"; "home" ], "/users/home"
                    [ "/one"; "two" ], "/one/two"
                    [ "users"; "1" ], "/users/1"
                    [ "users" + Router.encodeQueryString [ ("id", "1") ] ], "/users?id=1"
                    [ "search" + Router.encodeQueryString [ ("q", "whats up") ] ], @"/search?q=whats%20up"
                    [ "products" + Router.encodeQueryStringInts [ ("id", 1) ] ], "/products?id=1"
                    [ "users" + Router.encodeQueryString [] ], "/users"
                ]
                |> List.iter (fun (input, output) ->
                    Expect.equal (Router.encodeParts input RouteMode.Path) output "They are equal")

            testCase "Router.formatPath overload with segment list and query works" (fun _ ->
                [
                    [], [ ("foo", "bar") ], "/?foo=bar"
                    [ "hello" ], [], "/hello"
                    [ "hello" ], [ ("foo", "bar") ], "/hello?foo=bar"
                    [ "hello"; "friend" ], [ ("foo", "bar") ], "/hello/friend?foo=bar"
                ]
                |> List.iter (fun (sgs, qry, res) -> Expect.equal (Router.formatPath (sgs, qry)) res "They are equal"))

            testCase "Router.format overload with segment list and query works" (fun _ ->
                [
                    [], [ ("foo", "bar") ], "#/?foo=bar"
                    [ "hello" ], [], "#/hello"
                    [ "hello" ], [ ("foo", "bar") ], "#/hello?foo=bar"
                    [ "hello"; "friend" ], [ ("foo", "bar") ], "#/hello/friend?foo=bar"
                ]
                |> List.iter (fun (sgs, qry, res) -> Expect.equal (Router.format (sgs, qry)) res "They are equal"))
        ]

[<EntryPoint>]
let main args =
    // DOM tests share window.history, including when using Fable.Mocha's browser runner.
    Mocha.runTests (testSequenced routerTests)
