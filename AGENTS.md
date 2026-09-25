# AGENTS

Ping Runner follows the CodePrint repository rules (`lukr-99/CodePrint`, `RULES.md`).

## Read first

1. [ARCHITECTURE.md](ARCHITECTURE.md) before changing a seam, a project reference or the composition
   root.
2. [CONTEXT.md](CONTEXT.md) before naming a measurement. Attempt, run, session, outage and loaded
   latency have fixed meanings.
3. [docs/releasing.md](docs/releasing.md) before touching `Version.props`, the installer or the
   workflows.

## Baseline

- One top-level type per file, named like the type. XAML view and code-behind pairs are the exception.
- `PingRunner.Core` does no I/O and references no other project. New outside calls get an interface in
  Core and an adapter in Infrastructure, wired in `Composition/AppAdapters`.
- View models get everything through their constructors from `Composition/AppGraph`. Dialogs,
  clipboard and opening links go through `IDesktopServices`.
- Views use the `PR.*` brushes only, through `DynamicResource`. No literal colors in XAML or in the
  charts.
- Tests stay deterministic: `FakeTimeProvider` for time, stub HTTP handlers, temporary folders, and no
  network beyond loopback. WPF tests join the `WPF` collection and run through `WpfHost`.
- Conventional Commits, one coherent change each, with its tests and docs. Add user-visible changes to
  `CHANGELOG.md` under `[Unreleased]` in the same commit.
- Never open a visible window while verifying. `PageRenderTests` renders the real window off screen.

## Verification

```powershell
dotnet format PingRunner.slnx --verify-no-changes
dotnet build PingRunner.slnx -c Release
dotnet test --solution PingRunner.slnx -c Release
python tools/validate_repository.py --root .
pwsh ./tools/test-powershell-syntax.ps1
```
