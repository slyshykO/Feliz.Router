module Tests

open Browser.Dom
open Fable.Core
open Feliz
open Feliz.Router
open Fable.Mocha

[<Import("act", "react")>]
let private reactAct (_callback: unit -> JS.Promise<unit>) : obj = jsNative

// React returns a thenable; Fable's async adapter needs a native Promise.
[<Emit("Promise.resolve($0)")>]
let private resolveAct (_thenable: obj) : JS.Promise<unit> = jsNative

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

// Each row calls a distinct public overload; expected URLs do not use router helpers.
let private formatOverloadCases number numberText =
    let textQuery = [ "q", "Jane & Doe"; "tag", "a/b" ]
    let intQuery = [ "page size", number; "zero", 0 ]

    [
        ("team%20space/details"),
        (fun () -> Router.format ([| "team space"; "details" |])),
        (fun () -> Router.formatPath ([| "team space"; "details" |]))

        ("team%20space/details?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ([ "team space"; "details" ], textQuery)),
        (fun () -> Router.formatPath ([ "team space"; "details" ], textQuery))

        ("team?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team", textQuery)),
        (fun () -> Router.formatPath ("team", textQuery))

        ("team?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team", intQuery)),
        (fun () -> Router.formatPath ("team", intQuery))

        ("team%20space/part2?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", "part2", textQuery)),
        (fun () -> Router.formatPath ("team space", "part2", textQuery))

        ("team%20space/part2?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", "part2", intQuery)),
        (fun () -> Router.formatPath ("team space", "part2", intQuery))

        ("team%20space/part2/" + numberText + "?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", "part2", number, textQuery)),
        (fun () -> Router.formatPath ("team space", "part2", number, textQuery))

        ("team%20space/part2/" + numberText + "?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", "part2", number, intQuery)),
        (fun () -> Router.formatPath ("team space", "part2", number, intQuery))

        ("team%20space/part2/part3?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", "part2", "part3", textQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", textQuery))

        ("team%20space/part2/part3?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", "part2", "part3", intQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", intQuery))

        ("team%20space/" + numberText + "/part3?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, "part3", textQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", textQuery))

        ("team%20space/" + numberText + "/part3?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", number, "part3", intQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", intQuery))

        ("team%20space/part2/part3/part4?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", "part2", "part3", "part4", textQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", "part4", textQuery))

        ("team%20space/part2/part3/part4?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", "part2", "part3", "part4", intQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", "part4", intQuery))

        ("team%20space/part2/part3/part4/part5?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", "part2", "part3", "part4", "part5", textQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", "part4", "part5", textQuery))

        ("team%20space/part2/part3/part4/part5?page%20size=" + numberText + "&zero=0"),
        (fun () -> Router.format ("team space", "part2", "part3", "part4", "part5", intQuery)),
        (fun () -> Router.formatPath ("team space", "part2", "part3", "part4", "part5", intQuery))

        ("team%20space/" + numberText + "/part3/part4?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, "part3", "part4", textQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", "part4", textQuery))

        ("team%20space/"
         + numberText
         + "/part3/part4?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, "part3", "part4", intQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", "part4", intQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/part4?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, number, "part4", textQuery)),
        (fun () -> Router.formatPath ("team space", number, number, "part4", textQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/part4?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, number, "part4", intQuery)),
        (fun () -> Router.formatPath ("team space", number, number, "part4", intQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/part4/part5/part6?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, number, "part4", "part5", "part6", textQuery)),
        (fun () -> Router.formatPath ("team space", number, number, "part4", "part5", "part6", textQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/part4/part5/part6?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, number, "part4", "part5", "part6", intQuery)),
        (fun () -> Router.formatPath ("team space", number, number, "part4", "part5", "part6", intQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/"
         + numberText
         + "/part5?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, number, number, "part5", textQuery)),
        (fun () -> Router.formatPath ("team space", number, number, number, "part5", textQuery))

        ("team%20space/"
         + numberText
         + "/"
         + numberText
         + "/"
         + numberText
         + "/part5?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, number, number, "part5", intQuery)),
        (fun () -> Router.formatPath ("team space", number, number, number, "part5", intQuery))

        ("team%20space/"
         + numberText
         + "/part3/"
         + numberText
         + "/part5?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, "part3", number, "part5", textQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", number, "part5", textQuery))

        ("team%20space/"
         + numberText
         + "/part3/"
         + numberText
         + "/part5?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, "part3", number, "part5", intQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", number, "part5", intQuery))

        ("team%20space/" + numberText + "/part3/part4/part5?q=Jane%20%26%20Doe&tag=a%2Fb"),
        (fun () -> Router.format ("team space", number, "part3", "part4", "part5", textQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", "part4", "part5", textQuery))

        ("team%20space/"
         + numberText
         + "/part3/part4/part5?page%20size="
         + numberText
         + "&zero=0"),
        (fun () -> Router.format ("team space", number, "part3", "part4", "part5", intQuery)),
        (fun () -> Router.formatPath ("team space", number, "part3", "part4", "part5", intQuery))

        ("team%20space/details?raw=a%2Fb"),
        (fun () -> Router.format ("/team%20space/details?raw=a%2Fb")),
        (fun () -> Router.formatPath ("/team%20space/details?raw=a%2Fb"))

        ("team%20space/details"),
        (fun () -> Router.format ([ "team space"; "details" ])),
        (fun () -> Router.formatPath ([ "team space"; "details" ]))

        ("team%20space/" + numberText + ""),
        (fun () -> Router.format ("team space", number)),
        (fun () -> Router.formatPath ("team space", number))

        ("team%20space/" + numberText + "/" + numberText + ""),
        (fun () -> Router.format ("team space", number, number)),
        (fun () -> Router.formatPath ("team space", number, number))

        ("team%20space/part2/" + numberText + ""),
        (fun () -> Router.format ("team space", "part2", number)),
        (fun () -> Router.formatPath ("team space", "part2", number))

        ("team%20space/" + numberText + "/part3"),
        (fun () -> Router.format ("team space", number, "part3")),
        (fun () -> Router.formatPath ("team space", number, "part3"))

        ("team%20space/" + numberText + "/part3/" + numberText + ""),
        (fun () -> Router.format ("team space", number, "part3", number)),
        (fun () -> Router.formatPath ("team space", number, "part3", number))

        ("team%20space/" + numberText + "/part3/" + numberText + "/part5"),
        (fun () -> Router.format ("team space", number, "part3", number, "part5")),
        (fun () -> Router.formatPath ("team space", number, "part3", number, "part5"))

        ("team%20space/" + numberText + "/part3/" + numberText + "/part5/part6"),
        (fun () -> Router.format ("team space", number, "part3", number, "part5", "part6")),
        (fun () -> Router.formatPath ("team space", number, "part3", number, "part5", "part6"))

        ("team%20space/"
         + numberText
         + "/part3/"
         + numberText
         + "/part5/"
         + numberText
         + ""),
        (fun () -> Router.format ("team space", number, "part3", number, "part5", number)),
        (fun () -> Router.formatPath ("team space", number, "part3", number, "part5", number))

        ("team%20space/" + numberText + "/" + numberText + "/" + numberText + ""),
        (fun () -> Router.format ("team space", number, number, number)),
        (fun () -> Router.formatPath ("team space", number, number, number))

        ("team%20space/" + numberText + "/" + numberText + "/part4"),
        (fun () -> Router.format ("team space", number, number, "part4")),
        (fun () -> Router.formatPath ("team space", number, number, "part4"))

    ]

let private navigationOverloadCases mode =
    let textQuery = [ "q", "Jane & Doe"; "tag", "a/b" ]
    let intQuery = [ "page size", -42; "zero", 0 ]

    [
        "team?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team", textQuery, mode)),
        (fun () -> Router.navigatePath ("team", textQuery, mode)),
        (fun () -> Cmd.navigate ("team", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team", textQuery, mode))

        "team?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team", intQuery, mode)),
        (fun () -> Router.navigatePath ("team", intQuery, mode)),
        (fun () -> Cmd.navigate ("team", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team", intQuery, mode))

        "team%20space/part2?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", "part2", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", textQuery, mode))

        "team%20space/part2?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", "part2", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", intQuery, mode))

        "team%20space/part2/-42?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", "part2", -42, textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", -42, textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", -42, textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", -42, textQuery, mode))

        "team%20space/part2/-42?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", "part2", -42, intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", -42, intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", -42, intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", -42, intQuery, mode))

        "team%20space/part2/part3?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", "part2", "part3", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", textQuery, mode))

        "team%20space/part2/part3?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", "part2", "part3", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", intQuery, mode))

        "team%20space/-42/part3?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, "part3", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", textQuery, mode))

        "team%20space/-42/part3?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, "part3", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", intQuery, mode))

        "team%20space/part2/part3/part4?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", "part2", "part3", "part4", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", "part4", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", "part4", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", "part4", textQuery, mode))

        "team%20space/part2/part3/part4?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", "part2", "part3", "part4", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", "part4", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", "part4", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", "part4", intQuery, mode))

        "team%20space/part2/part3/part4/part5?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", "part2", "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", "part4", "part5", textQuery, mode))

        "team%20space/part2/part3/part4/part5?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", "part2", "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", "part3", "part4", "part5", intQuery, mode))

        "team%20space/-42/part3/part4?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, "part3", "part4", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", "part4", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", "part4", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", "part4", textQuery, mode))

        "team%20space/-42/part3/part4?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, "part3", "part4", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", "part4", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", "part4", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", "part4", intQuery, mode))

        "team%20space/-42/-42/part4?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, -42, "part4", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, "part4", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, "part4", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, "part4", textQuery, mode))

        "team%20space/-42/-42/part4?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, -42, "part4", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, "part4", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, "part4", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, "part4", intQuery, mode))

        "team%20space/-42/-42/part4/part5/part6?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, -42, "part4", "part5", "part6", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, "part4", "part5", "part6", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, "part4", "part5", "part6", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, "part4", "part5", "part6", textQuery, mode))

        "team%20space/-42/-42/part4/part5/part6?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, -42, "part4", "part5", "part6", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, "part4", "part5", "part6", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, "part4", "part5", "part6", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, "part4", "part5", "part6", intQuery, mode))

        "team%20space/-42/-42/-42/part5?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, -42, -42, "part5", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, -42, "part5", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, -42, "part5", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, -42, "part5", textQuery, mode))

        "team%20space/-42/-42/-42/part5?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, -42, -42, "part5", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, -42, "part5", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, -42, "part5", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, -42, "part5", intQuery, mode))

        "team%20space/-42/part3/-42/part5?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, "part5", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, "part5", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, "part5", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, "part5", textQuery, mode))

        "team%20space/-42/part3/-42/part5?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, "part5", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, "part5", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, "part5", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, "part5", intQuery, mode))

        "team%20space/-42/part3/part4/part5?q=Jane%20%26%20Doe&tag=a%2Fb",
        (fun () -> Router.navigate ("team space", -42, "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", "part4", "part5", textQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", "part4", "part5", textQuery, mode))

        "team%20space/-42/part3/part4/part5?page%20size=-42&zero=0",
        (fun () -> Router.navigate ("team space", -42, "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", "part4", "part5", intQuery, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", "part4", "part5", intQuery, mode))

        "team%20space/details?raw=a%2Fb",
        (fun () -> Router.navigate ("/team%20space/details?raw=a%2Fb", mode)),
        (fun () -> Router.navigatePath ("/team%20space/details?raw=a%2Fb", mode)),
        (fun () -> Cmd.navigate ("/team%20space/details?raw=a%2Fb", mode)),
        (fun () -> Cmd.navigatePath ("/team%20space/details?raw=a%2Fb", mode))

        "team%20space/-42",
        (fun () -> Router.navigate ("team space", -42, mode)),
        (fun () -> Router.navigatePath ("team space", -42, mode)),
        (fun () -> Cmd.navigate ("team space", -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, mode))

        "team%20space/-42/-42",
        (fun () -> Router.navigate ("team space", -42, -42, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, mode))

        "team%20space/part2/-42",
        (fun () -> Router.navigate ("team space", "part2", -42, mode)),
        (fun () -> Router.navigatePath ("team space", "part2", -42, mode)),
        (fun () -> Cmd.navigate ("team space", "part2", -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", "part2", -42, mode))

        "team%20space/-42/part3",
        (fun () -> Router.navigate ("team space", -42, "part3", mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", mode))

        "team%20space/-42/part3/-42",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, mode))

        "team%20space/-42/part3/-42/part5",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, "part5", mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, "part5", mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, "part5", mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, "part5", mode))

        "team%20space/-42/part3/-42/part5/part6",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, "part5", "part6", mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, "part5", "part6", mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, "part5", "part6", mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, "part5", "part6", mode))

        "team%20space/-42/part3/-42/part5/-42",
        (fun () -> Router.navigate ("team space", -42, "part3", -42, "part5", -42, mode)),
        (fun () -> Router.navigatePath ("team space", -42, "part3", -42, "part5", -42, mode)),
        (fun () -> Cmd.navigate ("team space", -42, "part3", -42, "part5", -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, "part3", -42, "part5", -42, mode))

        "team%20space/-42/-42/-42",
        (fun () -> Router.navigate ("team space", -42, -42, -42, mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, -42, mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, -42, mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, -42, mode))

        "team%20space/-42/-42/part4",
        (fun () -> Router.navigate ("team space", -42, -42, "part4", mode)),
        (fun () -> Router.navigatePath ("team space", -42, -42, "part4", mode)),
        (fun () -> Cmd.navigate ("team space", -42, -42, "part4", mode)),
        (fun () -> Cmd.navigatePath ("team space", -42, -42, "part4", mode))

    ]

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

            for pathMode in [ false; true ] do
                let modeName = if pathMode then "Path" else "Hash"

                testCase (sprintf "All format overloads convert numeric segments in %s mode" modeName)
                <| fun _ ->
                    for number, numberText in
                        [
                            System.Int32.MinValue, "-2147483648"
                            -1, "-1"
                            0, "0"
                            42, "42"
                            System.Int32.MaxValue, "2147483647"
                        ] do
                        for expected, hashFormat, pathFormat in formatOverloadCases number numberText do
                            let actual = if pathMode then pathFormat () else hashFormat ()
                            let prefix = if pathMode then "/" else "#/"

                            Expect.equal
                                actual
                                (prefix + expected)
                                (sprintf "Formatting %s with integer %d" expected number)

                testCase (sprintf "Empty queries and preformatted paths keep their behavior in %s mode" modeName)
                <| fun _ ->
                    let textQuery: (string * string) list = []
                    let intQuery: (string * int) list = []
                    let query = [ "city", "Київ"; "token", "a&b=c/#" ]

                    let cases =
                        [
                            "", Router.format (), Router.formatPath ()
                            "", Router.format ([]: string list), Router.formatPath ([]: string list)
                            "", Router.format ([], textQuery), Router.formatPath ([], textQuery)
                            "?city=%D0%9A%D0%B8%D1%97%D0%B2&token=a%26b%3Dc%2F%23",
                            Router.format ([], query),
                            Router.formatPath ([], query)
                            "users/0/edit",
                            Router.format ("users", 0, "edit", textQuery),
                            Router.formatPath ("users", 0, "edit", textQuery)
                            "users/-1/0/edit",
                            Router.format ("users", -1, 0, "edit", intQuery),
                            Router.formatPath ("users", -1, 0, "edit", intQuery)
                            "users/42/details?q=a%2Fb",
                            Router.format ("/users/42/details?q=a%2Fb", textQuery),
                            Router.formatPath ("/users/42/details?q=a%2Fb", textQuery)
                        ]

                    for expected, hashUrl, pathUrl in cases do
                        let actual, prefix = if pathMode then pathUrl, "/" else hashUrl, "#/"

                        Expect.equal
                            actual
                            (prefix + expected)
                            "Empty queries must not add separators or change path handling"

                for useElmish in [ false; true ] do
                    let apiName = if useElmish then "Cmd" else "Router"

                    testCaseAsync (sprintf "%s numeric overloads replace and notify once in %s mode" apiName modeName)
                    <| async {
                        let notifications = ResizeArray<string list>()
                        let prefix = if pathMode then "/" else "#/"

                        do!
                            withRoot "/#/initial" (fun root ->
                                async {
                                    do!
                                        act (fun () ->
                                            root.render (
                                                React.router
                                                    [
                                                        if pathMode then router.pathMode else router.hashMode
                                                        router.onUrlChanged notifications.Add
                                                    ]
                                            ))

                                    for expected, navigateHash, navigatePath, commandHash, commandPath in
                                        navigationOverloadCases HistoryMode.ReplaceState do
                                        notifications.Clear()
                                        let originalUrl = window.location.href
                                        let originalLength = window.history.length

                                        do!
                                            act (fun () ->
                                                if useElmish then
                                                    let command: Elmish.Cmd<unit> =
                                                        if pathMode then commandPath () else commandHash ()

                                                    Expect.equal
                                                        window.location.href
                                                        originalUrl
                                                        "Constructing a command must not navigate"

                                                    Expect.equal
                                                        notifications.Count
                                                        0
                                                        "Constructing a command must not notify"

                                                    command |> List.iter (fun effect -> effect ignore)
                                                elif pathMode then
                                                    navigatePath ()
                                                else
                                                    navigateHash ())

                                        let actual =
                                            if pathMode then
                                                window.location.pathname + window.location.search
                                            else
                                                window.location.hash

                                        Expect.equal
                                            actual
                                            (prefix + expected)
                                            (sprintf "%s should navigate to %s" apiName expected)

                                        Expect.equal
                                            window.history.length
                                            originalLength
                                            "ReplaceState must keep the history length"

                                        Expect.equal
                                            notifications.Count
                                            1
                                            "Each explicit navigation must notify exactly once"
                                })
                    }

                    testCaseAsync (
                        sprintf "%s default and explicit PushState preserve history in %s mode" apiName modeName
                    )
                    <| async {
                        let initialUrl = if pathMode then "/initial" else "/#/initial"

                        let currentUrl () =
                            if pathMode then
                                Router.currentPath ()
                            else
                                Router.currentUrl ()

                        let runCommand (command: Elmish.Cmd<unit>) =
                            command |> List.iter (fun effect -> effect ignore)

                        do!
                            withRoot initialUrl (fun root ->
                                async {
                                    do!
                                        act (fun () ->
                                            root.render (
                                                React.router [ if pathMode then router.pathMode else router.hashMode ]
                                            ))

                                    do!
                                        act (fun () ->
                                            match useElmish, pathMode with
                                            | true, true ->
                                                runCommand (Cmd.navigatePath ("users", -42, "edit", [ "page", 0 ]))
                                            | true, false ->
                                                runCommand (Cmd.navigate ("users", -42, "edit", [ "page", 0 ]))
                                            | false, true -> Router.navigatePath ("users", -42, "edit", [ "page", 0 ])
                                            | false, false -> Router.navigate ("users", -42, "edit", [ "page", 0 ]))

                                    Expect.equal
                                        (currentUrl ())
                                        [ "users"; "-42"; "edit"; "?page=0" ]
                                        "Default navigation must format numeric segments"

                                    do!
                                        act (fun () ->
                                            match useElmish, pathMode with
                                            | true, true ->
                                                runCommand (Cmd.navigatePath ("users", 7, HistoryMode.PushState))
                                            | true, false ->
                                                runCommand (Cmd.navigate ("users", 7, HistoryMode.PushState))
                                            | false, true -> Router.navigatePath ("users", 7, HistoryMode.PushState)
                                            | false, false -> Router.navigate ("users", 7, HistoryMode.PushState))

                                    Expect.equal
                                        (currentUrl ())
                                        [ "users"; "7" ]
                                        "Explicit PushState must format numeric segments"

                                    do! actOnWindowEvent "popstate" (fun () -> window.history.back ())

                                    Expect.equal
                                        (currentUrl ())
                                        [ "users"; "-42"; "edit"; "?page=0" ]
                                        "Explicit PushState must preserve the preceding entry"

                                    do! actOnWindowEvent "popstate" (fun () -> window.history.back ())

                                    Expect.equal
                                        (currentUrl ())
                                        [ "initial" ]
                                        "Default navigation must preserve the initial entry"
                                })
                    }

            testCase "Integer query values use numeric formatting and shared escaping"
            <| fun _ ->
                Expect.equal (Router.encodeQueryStringInts []) "" "An empty query has no separator"

                Expect.equal
                    (Router.encodeQueryStringInts
                        [
                            "min value", System.Int32.MinValue
                            "max&value", System.Int32.MaxValue
                            "zero", 0
                        ])
                    "?min%20value=-2147483648&max%26value=2147483647&zero=0"
                    "Integer values must be decimal text, with query keys escaped"

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
            for useElmish in [ false; true ] do
                for pathMode in [ false; true ] do
                    for strictMode in [ false; true ] do
                        for enabled in [ None; Some false; Some true ] do
                            let name =
                                sprintf
                                    "%s %s transitions=%A strict=%b"
                                    (if useElmish then "Elmish" else "React")
                                    (if pathMode then "Path" else "Hash")
                                    enabled
                                    strictMode

                            testCaseAsync (name + " schedules the actual route render")
                            <| async {
                                let controls = TransitionApp.createControls ()
                                let initialUrl = if pathMode then "/home" else "/#/home"

                                try
                                    do!
                                        withContainer initialUrl (fun container root ->
                                            async {
                                                do!
                                                    act (fun () ->
                                                        if useElmish then
                                                            TransitionApp.startElmish
                                                                root
                                                                pathMode
                                                                strictMode
                                                                enabled
                                                                true
                                                                controls
                                                        else
                                                            TransitionApp.mountReact
                                                                root
                                                                pathMode
                                                                strictMode
                                                                enabled
                                                                controls)

                                                Expect.equal
                                                    (elementByTestId container "transition-page").textContent
                                                    "Home"
                                                    "The initial page should render"

                                                controls.Notifications.Clear()

                                                do!
                                                    act (fun () ->
                                                        controls.Navigate "slow" HistoryMode.PushState

                                                        Expect.equal
                                                            controls.Notifications.Count
                                                            1
                                                            "The callback must still run synchronously"

                                                        let url =
                                                            if pathMode then
                                                                Router.currentPath ()
                                                            else
                                                                Router.currentUrl ()

                                                        Expect.equal
                                                            url
                                                            [ "slow" ]
                                                            "The URL must update immediately, even while rendering suspends")

                                                let fallback =
                                                    container.querySelector "[data-testid='transition-fallback']"

                                                if enabled = Some true then
                                                    Expect.isNull
                                                        fallback
                                                        "A transition should retain the already visible page"

                                                    Expect.equal
                                                        (elementByTestId container "transition-page").textContent
                                                        "Home"
                                                        "The old page stays until the new page is ready"
                                                else
                                                    Expect.isNotNull
                                                        fallback
                                                        "Default/disabled routing should retain ordinary urgent Suspense behavior"

                                                do! act (fun () -> (elementByTestId container "urgent-count").click())

                                                Expect.equal
                                                    (elementByTestId container "urgent-count").textContent
                                                    "1"
                                                    "Urgent interactions must work while the route is suspended"

                                                do! act controls.Complete

                                                Expect.equal
                                                    (elementByTestId container "transition-page").textContent
                                                    "Slow page"
                                                    "Resolving data must commit the new page"

                                                Expect.equal
                                                    controls.Notifications.Count
                                                    1
                                                    "Rendering must not repeat the navigation callback"

                                                do! actOnWindowEvent "popstate" window.history.back

                                                Expect.equal
                                                    (elementByTestId container "transition-page").textContent
                                                    "Home"
                                                    "Back should render the previous route"

                                                do! actOnWindowEvent "popstate" window.history.forward

                                                Expect.equal
                                                    (elementByTestId container "transition-page").textContent
                                                    "Slow page"
                                                    "Forward should render the resolved route"

                                                Expect.equal
                                                    controls.Notifications.Count
                                                    3
                                                    "Native history notifications should remain deduplicated"

                                                // Return before the pushed entry so subsequent cases can replace
                                                // the forward stack instead of hitting browser history limits.
                                                do! actOnWindowEvent "popstate" window.history.back
                                            })

                                    let count = controls.Notifications.Count

                                    do!
                                        act (fun () ->
                                            if pathMode then
                                                Router.navigatePath ("after-unmount", HistoryMode.ReplaceState)
                                            else
                                                Router.navigate ("after-unmount", HistoryMode.ReplaceState))

                                    Expect.equal
                                        controls.Notifications.Count
                                        count
                                        "Transition routing must clean up its subscriptions"
                                finally
                                    controls.Stop()
                            }

            for pathMode in [ false; true ] do
                testCaseAsync (sprintf "Transition settings and callbacks stay fresh (path=%b)" pathMode)
                <| async {
                    let controls = TransitionApp.createControls ()
                    let initialUrl = if pathMode then "/home" else "/#/home"

                    do!
                        withContainer initialUrl (fun container root ->
                            async {
                                do! act (fun () -> TransitionApp.mountReact root pathMode true None controls)
                                controls.Notifications.Clear()

                                for enabled in [ Some true; Some false; None; Some true ] do
                                    let label = sprintf "callback-%A" enabled
                                    let count = controls.Notifications.Count

                                    do!
                                        act (fun () ->
                                            controls.Configure enabled
                                            controls.RenameCallback label)

                                    Expect.equal
                                        controls.Notifications.Count
                                        count
                                        "Changing props must not trigger navigation"

                                    do! act (fun () -> controls.Navigate "slow" HistoryMode.ReplaceState)

                                    Expect.equal
                                        controls.Notifications.[count]
                                        (label, [ "slow" ])
                                        "The latest committed callback must run"

                                    let fallback = container.querySelector "[data-testid='transition-fallback']"

                                    Expect.equal
                                        (isNull fallback)
                                        (enabled = Some true)
                                        "The latest transition setting must determine scheduling"

                                    do! act (fun () -> controls.Navigate "home" HistoryMode.ReplaceState)

                                    Expect.equal
                                        (elementByTestId container "transition-page").textContent
                                        "Home"
                                        "A newer navigation must supersede the suspended route"

                                do! act controls.Complete

                                Expect.equal
                                    (elementByTestId container "transition-page").textContent
                                    "Home"
                                    "A superseded route must not reappear when its data resolves"
                            })
                }

            testCaseAsync "Queued Elmish navigation needs a transition at the render boundary"
            <| async {
                let controls = TransitionApp.createControls ()

                try
                    do!
                        withContainer "/#/home" (fun container root ->
                            async {
                                do!
                                    act (fun () ->
                                        TransitionApp.startElmish root false false (Some true) false controls)
                                // Idle dispatch runs immediately, so the router's transition is sufficient.
                                do! act (fun () -> Router.navigate ("slow", HistoryMode.ReplaceState))

                                Expect.isNull
                                    (container.querySelector "[data-testid='transition-fallback']")
                                    "Idle Elmish dispatch should inherit the router transition"

                                do! act (fun () -> Router.navigate ("home", HistoryMode.ReplaceState))
                                // Cmd.navigate runs while Elmish is processing another message; UrlChanged is queued.
                                do! act (fun () -> controls.Navigate "slow" HistoryMode.ReplaceState)

                                Expect.isNotNull
                                    (container.querySelector "[data-testid='transition-fallback']")
                                    "Queued dispatch does not inherit the completed callback's transition"

                                do! act controls.Complete
                            })
                finally
                    controls.Stop()
            }

        ]

[<EntryPoint>]
let main args =
    // DOM tests share window.history, including when using Fable.Mocha's browser runner.
    Mocha.runTests (testSequenced routerTests)
