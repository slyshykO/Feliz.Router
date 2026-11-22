module AsyncExamples

open System.Collections.Generic
open Browser.Dom
open Fable.Core
open Feliz
open Feliz.Router

type private Report = { Title: string; Description: string }

// A small, session-local cache for three fixed sample reports. In a real app,
// use a Suspense-compatible data layer with an explicit eviction/retry policy.
// Store Promises outside components so retries of a suspended render reuse them.
let private requests = Dictionary<string, JS.Promise<Report>>()

let private requestReport id shouldFail =
    JS.Constructors.Promise.Create(fun resolve reject ->
        window.setTimeout (
            (fun () ->
                if shouldFail then
                    reject (System.Exception "Demo request failure: unavailable report")
                else
                    resolve
                        {
                            Title =
                                match id with
                                | "overview" -> "Overview report"
                                | "details" -> "Detailed report"
                                | _ -> "Recovered report"
                            Description = "Your sample report is ready. Returning to it reuses the loaded result."
                        }),
            1200
        )
        |> ignore)

let private loadReport id =
    match requests.TryGetValue id with
    | true, pending -> pending
    | _ ->
        let pending = requestReport id (id = "unavailable")
        requests.Add(id, pending)
        pending

let private retryReport id =
    // Replace the failed Promise before resetting the boundary. Rereading the
    // same rejected Promise would immediately throw the same error again.
    let pending = requestReport id false
    requests.[id] <- pending
    pending

type private Page =
    | Home
    | ReportPage of string * JS.Promise<Report>
    | NotFound

let private readPage withErrors route =
    match route with
    | []
    | [ "home" ] -> Home
    | [ "reports"; id ] when id = "overview" || id = "details" || (withErrors && id = "unavailable") ->
        ReportPage(id, loadReport id)
    | _ -> NotFound

[<ReactComponent>]
let private ReportContent (pending: JS.Promise<Report>) allowFailure =
    let broken, setBroken = React.useState false

    if broken then
        failwith "Demo render failure: report component"

    let report = React.use' pending

    Html.section
        [
            prop.className "route-card gallery"
            prop.children
                [
                    Html.h2 [ prop.testId "report-page"; prop.text report.Title ]
                    Html.p report.Description
                    if allowFailure then
                        Html.button [ prop.text "Simulate rendering error"; prop.onClick (fun _ -> setBroken true) ]
                ]
        ]

[<ReactComponent>]
let private ReportApp withErrors =
    // The state holds the Promise itself, so an urgent parent render cannot
    // replace the currently displayed resource during a pending transition.
    let page, setPage =
        React.useState (fun () -> readPage withErrors (Router.currentUrl ()))

    let transitions, setTransitions = React.useState false

    let content =
        match page with
        | Home ->
            Html.section
                [
                    prop.className "route-card home"
                    prop.children
                        [
                            Html.h2 [ prop.testId "report-page"; prop.text "Home" ]
                            Html.p "Choose a report. The sample request takes a moment to finish."
                        ]
                ]
        | ReportPage(_, pending) -> ReportContent pending withErrors
        | NotFound -> Html.h2 [ prop.testId "report-page"; prop.text "Not found" ]

    let loading =
        React.Suspense(
            [ content ],
            fallback =
                Html.section
                    [
                        prop.className "route-card about"
                        prop.role "status"
                        prop.testId "report-loading"
                        prop.children
                            [
                                Html.h2 "Loading report…"
                                Html.p "You can keep editing your draft or open another page."
                            ]
                    ]
        )

    let protectedPage =
        if not withErrors then
            loading
        else
            let routeKey, resource =
                match page with
                | Home -> "home", null
                | ReportPage(id, pending) -> id, box pending
                | NotFound -> "not-found", null

            React.ErrorBoundary(
                [ loading ],
                (fun error ->
                    Html.section
                        [
                            prop.className "route-card about"
                            prop.role "alert"
                            prop.testId "report-error"
                            prop.children
                                [
                                    Html.h2 "This report could not be opened"
                                    Html.p
                                        "The sample report failed. Try again, or choose another report. Your draft is safe."
                                    Html.button
                                        [
                                            prop.text "Try again"
                                            prop.onClick (fun _ ->
                                                match page with
                                                | ReportPage(id, _) -> setPage (ReportPage(id, retryReport id))
                                                | _ -> ()

                                                error.reset ())
                                        ]
                                ]
                        ]),
                // Compare primitive route values and the Promise reference, not a freshly allocated list.
                resetKeys = [| box routeKey; resource |]
            )

    ExampleLayout.layout
        (if withErrors then
             "Recover one page at a time"
         else
             "Keep using the app while pages load")
        (if withErrors then
             "Open the unavailable report to try recovery, or simulate an error in a loaded report."
         else
             "Compare a loading placeholder with keeping the previous page visible. Loaded reports are reused.")
        [
            ExampleLayout.Draft()
            if not withErrors then
                Html.label
                    [
                        prop.className "example-option"
                        prop.children
                            [
                                Html.input
                                    [
                                        prop.type'.checkbox
                                        prop.isChecked transitions
                                        prop.onChange setTransitions
                                        prop.testId "keep-page"
                                    ]
                                Html.span "Keep the previous page visible while loading"
                            ]
                    ]
            Html.nav
                [
                    prop.className "page-controls"
                    prop.children
                        [
                            Html.a [ prop.href (Router.format "home"); prop.text "Home link" ]
                            Html.button
                                [
                                    prop.text "Open overview"
                                    prop.onClick (fun _ -> Router.navigate ("reports", "overview"))
                                ]
                            Html.button
                                [
                                    prop.text "Open details"
                                    prop.onClick (fun _ -> Router.navigate ("reports", "details"))
                                ]
                            if withErrors then
                                Html.button
                                    [
                                        prop.text "Open unavailable report"
                                        prop.onClick (fun _ -> Router.navigate ("reports", "unavailable"))
                                    ]
                            yield! ExampleLayout.historyButtons
                        ]
                ]
            React.router
                [
                    router.useTransitions transitions
                    router.onUrlChanged (readPage withErrors >> setPage)
                    router.children protectedPage
                ]
        ]

[<ReactComponent>]
let SuspenseApp () = ReportApp false

[<ReactComponent>]
let ErrorBoundaryApp () = ReportApp true
