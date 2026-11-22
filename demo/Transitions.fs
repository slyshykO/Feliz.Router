module Transitions

open Browser.Dom
open Elmish
open Elmish.React
open Feliz
open Feliz.Router

[<ReactComponent>]
let private Layout
    isElmish
    currentUrl
    enabled
    (onToggle: bool -> unit)
    onUrlChanged
    (navigate: string -> HistoryMode -> unit)
    =
    let draft, setDraft = React.useState ""

    let title, description, color =
        match currentUrl with
        | []
        | [ "home" ] -> "Home", "Choose a page to see its content change with a view transition.", "home"
        | [ "gallery" ] ->
            "Gallery",
            "This page arrived through ordinary router navigation. React coordinates its animation.",
            "gallery"
        | [ "about" ] ->
            "About",
            "Replace changes the current history entry. Back and Forward still follow browser history.",
            "about"
        | _ -> "Not found", "Choose Home to return to the example.", "home"

    Html.main
        [
            prop.className "transition-demo"
            prop.children
                [
                    Html.p [ prop.className "eyebrow"; prop.text "FELIZ.ROUTER / VIEW TRANSITIONS" ]
                    Html.h1 (
                        if isElmish then
                            "Elmish navigation"
                        else
                            "React state navigation"
                    )
                    Html.p
                        "Page animations are optional. Navigation, history, and local interactions keep working without them."
                    Html.nav
                        [
                            prop.className "example-links"
                            prop.children
                                [
                                    Html.a [ prop.href "/?example=transitions#/home"; prop.text "React state example" ]
                                    Html.a
                                        [ prop.href "/?example=transitions-elmish#/home"; prop.text "Elmish example" ]
                                    Html.a [ prop.href "/#/"; prop.text "Original demo" ]
                                ]
                        ]
                    Html.div
                        [
                            prop.className "transition-controls"
                            prop.children
                                [
                                    Html.label
                                        [
                                            prop.children
                                                [
                                                    Html.input
                                                        [
                                                            prop.type'.checkbox
                                                            prop.isChecked enabled
                                                            prop.onChange onToggle
                                                            prop.testId "enable-transitions"
                                                        ]
                                                    Html.span "Enable transitions"
                                                ]
                                        ]
                                    Html.label
                                        [
                                            prop.children
                                                [
                                                    Html.span "Try typing while changing pages"
                                                    Html.input
                                                        [
                                                            prop.type'.text
                                                            prop.value draft
                                                            prop.onChange setDraft
                                                            prop.testId "draft"
                                                        ]
                                                ]
                                        ]
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
                                            prop.text "Open gallery"
                                            prop.onClick (fun _ -> navigate "gallery" HistoryMode.PushState)
                                        ]
                                    Html.button
                                        [
                                            prop.text "Replace with About"
                                            prop.onClick (fun _ -> navigate "about" HistoryMode.ReplaceState)
                                        ]
                                    Html.button [ prop.text "Back"; prop.onClick (fun _ -> window.history.back ()) ]
                                    Html.button
                                        [ prop.text "Forward"; prop.onClick (fun _ -> window.history.forward ()) ]
                                ]
                        ]
                    React.router
                        [
                            router.useTransitions enabled
                            router.onUrlChanged onUrlChanged
                            router.children (
                                React.ViewTransition
                                    [
                                        viewTransition.update "route-page"
                                        viewTransition.children
                                            [
                                                Html.section
                                                    [
                                                        prop.className ("route-card " + color)
                                                        prop.children
                                                            [
                                                                Html.p
                                                                    [
                                                                        prop.className "eyebrow"
                                                                        prop.text "CURRENT PAGE"
                                                                    ]
                                                                Html.h2 [ prop.testId "page"; prop.text title ]
                                                                Html.p description
                                                            ]
                                                    ]
                                            ]
                                    ]
                            )
                        ]
                    Html.p
                        [
                            prop.className "transition-note"
                            prop.text
                                "Reduced-motion preferences are respected. Browsers without View Transitions show each page without animation."
                        ]
                ]
        ]

[<ReactComponent>]
let ReactExample () =
    let currentUrl, setCurrentUrl = React.useState (Router.currentUrl ())
    let enabled, setEnabled = React.useState true
    Layout false currentUrl enabled setEnabled setCurrentUrl (fun page mode -> Router.navigate (page, mode))

type private Model =
    {
        CurrentUrl: string list
        UseTransitions: bool
    }

type private Message =
    | UrlChanged of string list
    | SetTransitions of bool
    | Navigate of string * HistoryMode

let startElmish rootId =
    let init () =
        {
            CurrentUrl = Router.currentUrl ()
            UseTransitions = true
        },
        Cmd.none

    let update message model =
        match message with
        | UrlChanged url -> { model with CurrentUrl = url }, Cmd.none
        | SetTransitions enabled -> { model with UseTransitions = enabled }, Cmd.none
        | Navigate(page, mode) -> model, Cmd.navigate (page, mode)

    let view model dispatch =
        Layout
            true
            model.CurrentUrl
            model.UseTransitions
            (SetTransitions >> dispatch)
            (UrlChanged >> dispatch)
            (fun page mode -> dispatch (Navigate(page, mode)))

    Program.mkProgram init update view
    |> Program.withReactSynchronous rootId
    |> TransitionHelpers.withRouteTransitions (fun model -> model.UseTransitions) (fun model -> model.CurrentUrl)
    |> Program.run
