module RouteHookApp

open Browser.Dom
open Fable.Core
open Feliz
open Feliz.Router

type Observer =
    {
        mutable Renders: int
        mutable Route: string list
        mutable Commits: int
    }

let observe () =
    { Renders = 0; Route = []; Commits = 0 }

[<ReactComponent>]
let Reader (mode: RouteMode option) revision testId (observer: Observer) =
    let route = Router.useRoute (?mode = mode)
    observer.Renders <- observer.Renders + 1
    observer.Route <- route
    React.useEffect ((fun () -> observer.Commits <- observer.Commits + 1), [| box route |])

    Html.div
        [
            Html.pre [ prop.testId testId; prop.text (String.concat " | " route) ]
            Html.span (string revision)
        ]

[<ReactComponent>]
let NavigateBeforeSubscription (mode: RouteMode) (observer: Observer) =
    let route = Router.useRoute mode
    observer.Route <- route
    // Layout effects run before useSyncExternalStore's passive subscription.
    React.useLayoutEffectOnce (fun () ->
        if mode = RouteMode.Path then
            Router.navigatePath ("mounted", HistoryMode.ReplaceState)
        else
            Router.navigate ("mounted", HistoryMode.ReplaceState))

    Html.pre (String.concat " | " route)

[<ReactComponent>]
let SuspendedReader (mode: RouteMode) (pending: JS.Promise<string>) =
    let route = Router.useRoute mode
    let title = if route = [ "slow" ] then React.use' pending else "Home"
    Html.h1 [ prop.testId "hook-page"; prop.text title ]

type ListenerMonitor =
    abstract Added: int
    abstract Removed: int
    abstract Active: int
    abstract Restore: unit -> unit

// Observe actual listener identities, so an unmounted root silently ignoring an
// update cannot conceal a leaked subscription. Every test restores the window.
[<Emit("""(() => {
    const types = new Set(['hashchange', 'popstate', 'CUSTOM_NAVIGATION_EVENT']);
    const add = window.addEventListener, remove = window.removeEventListener;
    const active = new Map();
    let added = 0, removed = 0;
    window.addEventListener = function(type, listener, options) {
        if (types.has(type)) {
            added++;
            if (!active.has(type)) active.set(type, new Set());
            active.get(type).add(listener);
        }
        return add.call(this, type, listener, options);
    };
    window.removeEventListener = function(type, listener, options) {
        if (types.has(type)) {
            removed++;
            active.get(type)?.delete(listener);
        }
        return remove.call(this, type, listener, options);
    };
    return {
        get Added() { return added; },
        get Removed() { return removed; },
        get Active() { return [...active.values()].reduce((total, listeners) => total + listeners.size, 0); },
        Restore() {
            for (const [type, listeners] of active)
                for (const listener of listeners) remove.call(window, type, listener);
            window.addEventListener = add;
            window.removeEventListener = remove;
        }
    };
})()""")>]
let monitorListeners () : ListenerMonitor = jsNative
