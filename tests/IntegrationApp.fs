module IntegrationApp

open Elmish
open Feliz
open Feliz.Router

let private wrapStrict strictMode element =
    if strictMode then React.StrictMode [ element ] else element

let private pageView pathMode currentUrl (navigate: string -> HistoryMode -> unit) =
    let title =
        match currentUrl with
        | [] | [ "home" ] -> "Home"
        | [ "users"; Route.Int userId ] -> sprintf "User %d" userId
        | [ "search"; Route.Query [ "q", query ] ] -> "Search: " + query
        | _ -> "Not found"

    Html.div [
        Html.h1 [ prop.testId "page"; prop.text title ]
        Html.button [
            prop.testId "push-user"
            prop.text "Open user"
            prop.onClick (fun _ -> navigate "/users/7" HistoryMode.PushState)
        ]
        Html.button [
            prop.testId "replace-home"
            prop.text "Replace with home"
            prop.onClick (fun _ -> navigate "/home" HistoryMode.ReplaceState)
        ]
        Html.a [
            prop.testId "native-link"
            prop.href (if pathMode then "#details" else Router.format "/users/8")
            prop.text "Follow fragment link"
        ]
    ]

let private routerView pathMode currentUrl onUrlChanged navigate =
    React.router [
        if pathMode then router.pathMode else router.hashMode
        router.onUrlChanged onUrlChanged
        router.children (pageView pathMode currentUrl navigate)
    ]

[<ReactComponent>]
let private StatefulRouter (pathMode: bool) (notifications: ResizeArray<string list>) =
    // Start empty so the initial router notification must render the deep-linked page.
    let currentUrl, setCurrentUrl = React.useState<string list>([])
    let onUrlChanged segments =
        notifications.Add segments
        setCurrentUrl segments
    let navigate (page: string) (mode: HistoryMode) =
        if pathMode then Router.navigatePath(page, mode)
        else Router.navigate(page, mode)
    routerView pathMode currentUrl onUrlChanged navigate

let mountReact (root: IReactRoot) pathMode strictMode notifications =
    root.render (wrapStrict strictMode (StatefulRouter pathMode notifications))

type private Message =
    | UrlChanged of string list
    | Navigate of string * HistoryMode
    | Stop

let startElmish (root: IReactRoot) pathMode strictMode (notifications: ResizeArray<string list>) =
    let mutable dispatchMessage: Dispatch<Message> = ignore
    let init () = [], Cmd.none
    let update message currentUrl =
        match message with
        | UrlChanged segments ->
            notifications.Add segments
            segments, Cmd.none
        | Navigate(page, mode) ->
            let command =
                if pathMode then Cmd.navigatePath(page, mode)
                else Cmd.navigate(page, mode)
            currentUrl, command
        | Stop -> currentUrl, Cmd.none
    let view currentUrl dispatch =
        routerView pathMode currentUrl (UrlChanged >> dispatch)
            (fun page mode -> dispatch (Navigate(page, mode)))

    Program.mkProgram init update view
    // Own the React root in the test harness so it can always be unmounted.
    // Elmish still runs the real dispatch queue, update function, and Cmd effects.
    |> Program.withSetState (fun currentUrl dispatch ->
        dispatchMessage <- dispatch
        root.render (wrapStrict strictMode (view currentUrl dispatch)))
    |> Program.withTermination ((=) Stop) ignore
    |> Program.withErrorHandler (fun (_, error) -> raise error)
    |> Program.run

    fun () -> dispatchMessage Stop
