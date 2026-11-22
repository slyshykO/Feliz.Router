module Main

open Elmish
open Elmish.React
open Browser.Dom
open Feliz
open Feliz.Router

let example =
    (Router.createUrlSearchParams window.location.search).entries()
    |> Seq.tryPick (fun entry -> if entry.[0] = "example" then Some entry.[1] else None)

match example with
| Some "activity" -> ReactDOM.createRoot(document.getElementById "root").render(ActivityExample.App())
| Some "suspense" -> ReactDOM.createRoot(document.getElementById "root").render(AsyncExamples.SuspenseApp())
| Some "errors" -> ReactDOM.createRoot(document.getElementById "root").render(AsyncExamples.ErrorBoundaryApp())
| Some "hook" -> ReactDOM.createRoot(document.getElementById "root").render(RouteHook.App())
| Some "transitions" -> ReactDOM.createRoot(document.getElementById "root").render(Transitions.ReactExample())
| Some "transitions-elmish" -> Transitions.startElmish "root"
| Some "path" ->
    Program.mkProgram AppPath.init AppPath.update AppPath.render
    |> Program.withReactSynchronous "root"
    |> Program.run
| _ ->
    Program.mkProgram App.init App.update App.render
    |> Program.withReactSynchronous "root"
    |> Program.withConsoleTrace
    |> Program.run
