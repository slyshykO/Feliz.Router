# Repository Guidelines

## Project Structure & Module Organization

- `src/Router.fs` contains the routing implementation and public API; `src/Feliz.Router.fsproj` defines the NuGet library.
- `tests/Tests.fs` contains the Fable.Mocha suite.
- `demo/App.fs` and `demo/AppPath.fs` demonstrate hash and path routing; `demo/Main.fs` selects the active example.
- `public/index.html` hosts the demo; webpack writes its generated bundle into `public/`.
- `build/` contains release tooling. `.config/dotnet-tools.json` pins local tools; Paket files manage .NET dependencies.

## Build, Test, and Development Commands

Run commands from the repository root with a .NET SDK and Node.js/npm installed.

- `npm install`: install JavaScript dependencies.
- `dotnet tool restore`: restore the pinned Fable, Paket, and FAKE tools.
- `dotnet build src/Feliz.Router.fsproj`: compile the library.
- `npm test`: automatically run `pretest` to restore tools and compile F# tests with Fable, then execute Mocha.
- `npm run start:demo`: watch F# changes and serve the demo at `http://localhost:8080`.
- `npm run build:demo`: generate the production demo bundle.

## Coding Style & Naming Conventions

Follow surrounding F# code: four-space indentation, camelCase functions and values, and PascalCase modules, types, and union cases. Preserve existing DSL names such as `router`. Use pattern matching and pipelines consistently with nearby code, and add `///` documentation for public APIs. Keep F# files in dependency order in each `.fsproj`.

No repository-wide formatter or lint command is configured. Avoid unrelated formatting changes and do not commit generated `*.fs.js`, bundles, or `bin/` and `obj/` output.

## Testing Guidelines

Add regression cases to `routerTests` in `tests/Tests.fs`, using descriptive names such as `Router.urlSegments decodes URL segments`. Use Fable.Mocha assertions with useful failure messages. Cover relevant hash/path behavior, encoding, query parameters, and empty inputs. No numeric coverage threshold is configured. Run `npm test`; also build and manually check the demo for navigation or rendering changes.

## Commit & Pull Request Guidelines

The `update-deps` branch is the main branch for this fork. Base new work on it and target pull requests to it.

History favors short, plain-language subjects such as `Update dependencies` and `Fix ParamArray + adding tests for each Format* overload`; no uniform Conventional Commits prefix is used. Write focused, action-oriented subjects.

In pull requests, explain the behavior change, link relevant issues, and report validation commands and results. Update README examples for API changes; include screenshots for visible demo changes.
