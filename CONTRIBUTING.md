# Contributing to WardenId

Thanks for your interest in WardenId. Contributions of every size are welcome: bug reports, docs fixes, tests, and new features from the roadmap.

## Ways to contribute

- **Report a bug.** Open an issue with steps to reproduce, what you expected, and what happened. Include logs or the failing request where you can.
- **Suggest a feature.** Open an issue describing the use case before writing code, so we can agree on the approach.
- **Pick up an issue.** Issues labelled `good first issue` or `help wanted` are good starting points. Comment on the issue so others know you're on it.
- **Improve the docs.** README, code comments, and examples all count.

## Security issues

Please **do not** open a public issue for a security vulnerability. Report it privately through [GitHub Security Advisories](https://github.com/Youssef-Abdullahx09/WardenId/security/advisories/new) instead, and give us a chance to fix it before disclosure.

## Getting set up

Follow [Getting started](README.md#getting-started) in the README. In short:

```bash
dotnet build WardenId.slnx
dotnet run --project WardenId.Gateway
dotnet test WardenId.slnx
```

You need the .NET 10 SDK, PostgreSQL, RabbitMQ, and an SMTP catcher such as Mailpit. Docker is needed for the Testcontainers-based tests.

## Conventions

The codebase follows a strict set of conventions, documented in full in [`CLAUDE.md`](CLAUDE.md). Please read it before making non-trivial changes. The ones that come up most often:

- **Module isolation.** A module never references another module's `Domain` or `Application` project. Cross-module calls go through `Integration.Command` contracts; cross-module reactions go through CAP subscribers.
- **Vertical slices.** Features live under `Features/<Aggregate>/<UseCase>/`, with `Endpoint.cs` + `Request.cs` in Presentation and `Command.cs`/`Query.cs` + `Handler.cs` in Application.
- **Results, not exceptions.** Domain and Application code returns `FluentResults.Result`/`Result<T>` for expected failures. New error codes need a matching case in `IResultExtensions.HandleApiFailure()`.
- **Write/read split.** Writes go through EF Core aggregates; untracked reads go through the Dapper `<Module>.Query` projects.
- **No magic strings.** Routes, claim types, config keys, topics, and similar identifiers come from library constants, enums, or a constants class in the project's `Utilities/` folder.
- **Enums** give every member an explicit value starting at 1, never 0.
- **No `Dto` suffix** on type names.
- **Packages** are versioned centrally in `Directory.Packages.props`. After changing references, run `dotnet restore --force-evaluate` and commit the updated `packages.lock.json` files.
- **Migrations** are added per module with `dotnet ef migrations add` (see the README's Development section). Don't edit migrations that are already on `master`.

## Pull requests

1. Fork the repository and create a branch from `master` (e.g. `feat/passkeys`, `fix/token-lifetime`).
2. Keep each PR focused on one change. Large features are easier to review when split up.
3. Add or update tests. Handler tests use hand-written fakes in the test project's `Fakes/` folder and EF Core InMemory.
4. Make sure `dotnet build WardenId.slnx` and `dotnet test WardenId.slnx` pass with no new warnings.
5. Write commit messages in [Conventional Commits](https://www.conventionalcommits.org/) style, matching the history: `feat: ...`, `fix: ...`, `refactor: ...`, `docs: ...`, `build: ...`, `test: ...`.
6. Open the PR with a short description of what changed and why, and link the issue it addresses.

A maintainer will review your PR and may ask for changes. Once it's approved, it will be merged into `master`.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
