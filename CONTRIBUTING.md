# Contributing

## Local verification

Run these from the repository root before every commit. CI runs the same checks on every pull request
and on `main`.

```powershell
dotnet format PingRunner.slnx --verify-no-changes
dotnet build PingRunner.slnx -c Release
dotnet test --solution PingRunner.slnx -c Release
python tools/validate_repository.py --root .
pwsh ./tools/test-powershell-syntax.ps1
```

Warnings fail the build, and code style is checked during the build. The tests need the .NET 10 SDK
and nothing else. `tools/validate_repository.py`, `tools/migrations.py` and
`tools/test-powershell-syntax.ps1` are copies from CodePrint; update them from there rather than
editing them here.

## Screenshots

The README screenshots come from `PageRenderTests`, which renders every page off screen with sample
data. Point it at a folder to keep the images:

```powershell
$env:PINGRUNNER_SCREENSHOTS = "$PWD\artifacts\screenshots"
dotnet test --project tests\PingRunner.App.Tests -c Release
```

Copy the ones you need into `docs/assets/` and describe them in the image alt text.

## Change shape

- One coherent behavior or repository change per commit, as a Conventional Commit:
  `type(scope): imperative summary`. Scopes in use are `core`, `infrastructure`, `app`, `cli`,
  `installer`, `ci` and `docs`.
- Tests and documentation that explain a behavior go in the same commit as the behavior.
- Add each user-visible change to `CHANGELOG.md` under `[Unreleased]` in that commit.
- Never commit build output, `installer/publish`, `installer/dist`, signing material or machine paths.

## Pull requests

Say what changed for someone using the app, how you checked it, and attach screenshots for UI work
(both themes if colors changed). Mention any change to `settings.json` or the CSV format, and how an
older file still loads.
