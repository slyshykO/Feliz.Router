module ExampleLayout

open Browser.Dom
open Feliz

let layout (title: string) (description: string) content =
    Html.main
        [
            prop.className "transition-demo application-example"
            prop.children
                [
                    Html.p [ prop.className "eyebrow"; prop.text "FELIZ.ROUTER / APPLICATION EXAMPLES" ]
                    Html.h1 title
                    Html.p description
                    Html.nav
                        [
                            prop.className "example-links"
                            prop.children
                                [
                                    Html.a [ prop.href "/?example=activity#/work"; prop.text "Preserved tabs" ]
                                    Html.a [ prop.href "/?example=suspense#/home"; prop.text "Loading pages" ]
                                    Html.a [ prop.href "/?example=errors#/home"; prop.text "Recovering from errors" ]
                                    Html.a [ prop.href "/#/"; prop.text "Original demo" ]
                                ]
                        ]
                    yield! content
                ]
        ]

let historyButtons =
    [
        Html.button [ prop.text "Back"; prop.onClick (fun _ -> window.history.back ()) ]
        Html.button [ prop.text "Forward"; prop.onClick (fun _ -> window.history.forward ()) ]
    ]

[<ReactComponent>]
let Draft () =
    let draft, setDraft = React.useState ""

    Html.label
        [
            prop.className "example-draft"
            prop.children
                [
                    Html.span "A draft that stays while pages load or recover"
                    Html.input
                        [
                            prop.type'.text
                            prop.value draft
                            prop.onChange setDraft
                            prop.testId "shell-draft"
                        ]
                ]
        ]
