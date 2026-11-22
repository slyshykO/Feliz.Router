module ActivityExample

open Browser.Dom
open Browser.Types
open Feliz
open Feliz.Router

[<ReactComponent>]
let private Workspace id (title: string) color =
    let draft, setDraft = React.useState ""
    let points, updatePoints = React.useStateWithUpdater 0

    // Activity removes this subscription while hidden and reconnects it on reveal.
    // State remains local to this component; no page-state cache is needed.
    React.useEffectOnce (fun () ->
        let onKey (event: Event) =
            // The DOM binding exposes Event; this listener only receives keydown events.
            let keyboard = event :?> KeyboardEvent

            if keyboard.key = "ArrowUp" then
                updatePoints ((+) 1)

        window.addEventListener ("keydown", onKey)
        fun () -> window.removeEventListener ("keydown", onKey))

    Html.section
        [
            prop.className ("route-card " + color)
            prop.testId (id + "-panel")
            prop.children
                [
                    Html.h2 title
                    Html.label
                        [
                            prop.className "workspace-draft"
                            prop.children
                                [
                                    Html.span (title + " notes")
                                    Html.textarea
                                        [ prop.value draft; prop.onChange setDraft; prop.testId (id + "-draft") ]
                                ]
                        ]
                    Html.p
                        [
                            Html.span "Points: "
                            Html.strong [ prop.testId (id + "-points"); prop.text (string points) ]
                        ]
                    Html.button [ prop.text "Add a point"; prop.onClick (fun _ -> updatePoints ((+) 1)) ]
                    Html.p "ArrowUp also adds a point, only while this tab is open."
                ]
        ]

[<ReactComponent>]
let App () =
    let route = Router.useRoute ()
    let isWork = route = [] || route = [ "work" ]
    let isPersonal = route = [ "personal" ]

    let mode visible =
        if visible then
            ActivityMode.Visible
        else
            ActivityMode.Hidden

    ExampleLayout.layout
        "Keep your place between tabs"
        "Write notes and add points in each tab. Switch tabs or use Back and Forward; your work stays where you left it."
        [
            Html.nav
                [
                    prop.className "page-controls"
                    prop.children
                        [
                            Html.a [ prop.href (Router.format "work"); prop.text "Work tab" ]
                            Html.a [ prop.href (Router.format "personal"); prop.text "Personal tab" ]
                            yield! ExampleLayout.historyButtons
                        ]
                ]
            // Keep both boundaries in fixed positions. Only their modes change.
            // This is an intentional, bounded set of two pages, not an unlimited route cache.
            React.Activity(mode isWork, Workspace "work" "Work" "home")
            React.Activity(mode isPersonal, Workspace "personal" "Personal" "gallery")
            if not isWork && not isPersonal then
                Html.h2 "Not found"
        ]
