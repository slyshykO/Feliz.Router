module RouteHook

open Browser.Dom
open Feliz
open Feliz.Router

[<ReactComponent>]
let App () =
    let route = Router.useRoute ()

    let title, color =
        match route with
        | []
        | [ "home" ] -> "Home", "home"
        | [ "users"; Route.Int id ] -> sprintf "User %d" id, "gallery"
        | [ "about" ] -> "About", "about"
        | _ -> "Not found", "home"

    Html.main
        [
            prop.className "transition-demo"
            prop.children
                [
                    Html.p [ prop.className "eyebrow"; prop.text "FELIZ.ROUTER / ROUTE HOOK" ]
                    Html.h1 "Read the current route"
                    Html.p "Choose a page or use Back and Forward. The page follows the URL."
                    Html.nav
                        [
                            prop.className "page-controls"
                            prop.children
                                [
                                    Html.a [ prop.href (Router.format "home"); prop.text "Home link" ]
                                    Html.button
                                        [
                                            prop.text "Open user 42"
                                            prop.onClick (fun _ -> Router.navigate ("users", 42))
                                        ]
                                    Html.button
                                        [
                                            prop.text "Replace with About"
                                            prop.onClick (fun _ -> Router.navigate ("about", HistoryMode.ReplaceState))
                                        ]
                                    Html.button [ prop.text "Back"; prop.onClick (fun _ -> window.history.back ()) ]
                                    Html.button
                                        [ prop.text "Forward"; prop.onClick (fun _ -> window.history.forward ()) ]
                                ]
                        ]
                    Html.section
                        [
                            prop.className ("route-card " + color)
                            prop.children
                                [
                                    Html.h2 [ prop.testId "hook-page"; prop.text title ]
                                    Html.pre [ prop.testId "hook-segments"; prop.text (sprintf "%A" route) ]
                                ]
                        ]
                    Html.nav
                        [
                            prop.className "example-links"
                            prop.children
                                [
                                    Html.a
                                        [
                                            prop.href "/?example=transitions#/home"
                                            prop.text "Animated navigation example"
                                        ]
                                    Html.a [ prop.href "/#/"; prop.text "Original demo" ]
                                ]
                        ]
                ]
        ]
