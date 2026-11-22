module TransitionApp

open Browser.Dom
open Elmish
open Fable.Core
open Feliz
open Feliz.Router

type Controls =
    {
        Pending: JS.Promise<string>
        Complete: unit -> unit
        Notifications: ResizeArray<string * string list>
        mutable Configure: bool option -> unit
        mutable RenameCallback: string -> unit
        mutable Navigate: string -> HistoryMode -> unit
        mutable Stop: unit -> unit
    }

let createControls () =
    let mutable complete = ignore

    let pending =
        JS.Constructors.Promise.Create(fun resolve _ -> complete <- fun () -> resolve "Slow page")

    {
        Pending = pending
        Complete = fun () -> complete ()
        Notifications = ResizeArray()
        Configure = ignore
        RenameCallback = ignore
        Navigate = fun _ _ -> ()
        Stop = ignore
    }

let private wrapStrict strictMode element =
    if strictMode then React.StrictMode [ element ] else element

[<ReactComponent>]
let private Page (segments: string list) (pending: JS.Promise<string>) =
    let title = if segments = [ "slow" ] then React.use' pending else "Home"
    Html.h1 [ prop.testId "transition-page"; prop.text title ]

[<ReactComponent>]
let private Shell (controls: Controls) pathMode (enabled: bool option) currentUrl onUrlChanged =
    let urgentCount, setUrgentCount = React.useState 0

    React.router
        [
            if pathMode then router.pathMode else router.hashMode
            match enabled with
            | Some value -> router.useTransitions value
            | None -> ()
            router.onUrlChanged onUrlChanged
            router.children
                [
                    Html.button
                        [
                            prop.testId "urgent-count"
                            prop.text (string urgentCount)
                            prop.onClick (fun _ -> setUrgentCount (urgentCount + 1))
                        ]
                    React.Suspense(
                        [ Page currentUrl controls.Pending ],
                        fallback = Html.p [ prop.testId "transition-fallback"; prop.text "Loading" ]
                    )
                ]
        ]

[<ReactComponent>]
let private Stateful (controls: Controls) pathMode (initialTransitions: bool option) =
    let currentUrl, setCurrentUrl = React.useState [ "home" ]
    let enabled, setEnabled = React.useState initialTransitions
    let label, setLabel = React.useState "first"
    controls.Configure <- setEnabled
    controls.RenameCallback <- setLabel

    controls.Navigate <-
        fun page mode ->
            if pathMode then
                Router.navigatePath (page, mode)
            else
                Router.navigate (page, mode)

    Shell controls pathMode enabled currentUrl (fun segments ->
        controls.Notifications.Add(label, segments)
        setCurrentUrl segments)

let mountReact root pathMode strictMode enabled controls =
    (root: IReactRoot).render(wrapStrict strictMode (Stateful controls pathMode enabled))

type private Model =
    {
        Url: string list
        Enabled: bool option
        Label: string
    }

type private Message =
    | UrlChanged of string list
    | Navigate of string * HistoryMode
    | Configure of bool option
    | Rename of string
    | Stop

let startElmish (root: IReactRoot) pathMode strictMode enabled wrapRender (controls: Controls) =
    let mutable dispatchMessage: Dispatch<Message> = ignore

    let init () =
        {
            Url = [ "home" ]
            Enabled = enabled
            Label = "first"
        },
        Cmd.none

    let update message model =
        match message with
        | UrlChanged url -> { model with Url = url }, Cmd.none
        | Configure enabled -> { model with Enabled = enabled }, Cmd.none
        | Rename label -> { model with Label = label }, Cmd.none
        | Navigate(page, mode) ->
            model,
            (if pathMode then
                 Cmd.navigatePath (page, mode)
             else
                 Cmd.navigate (page, mode))
        | Stop -> model, Cmd.none

    let view model dispatch =
        Shell controls pathMode model.Enabled model.Url (fun segments ->
            controls.Notifications.Add(model.Label, segments)
            dispatch (UrlChanged segments))

    Program.mkProgram init update view
    |> Program.withSetState (fun model dispatch ->
        dispatchMessage <- dispatch
        root.render (wrapStrict strictMode (view model dispatch)))
    |> (if wrapRender then
            TransitionHelpers.withRouteTransitions (fun model -> Option.defaultValue false model.Enabled) (fun model ->
                model.Url)
        else
            id)
    |> Program.withTermination ((=) Stop) ignore
    |> Program.withErrorHandler (fun (_, error) -> raise error)
    |> Program.run

    controls.Configure <- Configure >> dispatchMessage
    controls.RenameCallback <- Rename >> dispatchMessage
    controls.Navigate <- fun page mode -> dispatchMessage (Navigate(page, mode))
    controls.Stop <- fun () -> dispatchMessage Stop
