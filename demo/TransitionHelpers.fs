module TransitionHelpers

open Elmish
open Feliz

// Apply after withReactSynchronous (or a custom synchronous setState).
// Elmish can queue UrlChanged during Cmd execution, outside the router's transition.
// Starting here includes the actual React render, while unrelated updates stay urgent.
let withRouteTransitions isEnabled getRoute program =
    let setState = Program.setState program
    let mutable previousRoute = None

    program
    |> Program.withSetState (fun model dispatch ->
        let route = getRoute model
        let changed = previousRoute |> Option.exists ((<>) route)
        previousRoute <- Some route

        if isEnabled model && changed then
            React.startTransition (fun () -> setState model dispatch)
        else
            setState model dispatch)
