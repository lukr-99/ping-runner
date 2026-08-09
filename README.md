# Ping Runner

Ping Runner is a small .NET ping utility with:

- a WPF desktop app for long-running ping sessions
- a live graph window with zoom, pan, CSV import/export, and PNG export
- a console try app for quick terminal-based runs

## Projects

- `PingerTool` - the desktop app
- `PingerTool.Core` - shared ping models and ping loop service
- `PingerTool.TryApp` - the console entry point

## Run

Build the solution:

```powershell
dotnet build Playground.sln -c Release
```

Run the desktop app:

```powershell
dotnet run --project PingerTool\PingerTool.csproj
```

Run the console app:

```powershell
dotnet run --project PingerTool.TryApp\PingerTool.TryApp.csproj
```

## Install

The desktop app includes an installer script:

```powershell
powershell -ExecutionPolicy Bypass -File .\PingerTool\install.ps1
```

That publishes a self-contained Windows build to `%LOCALAPPDATA%\Programs\PingRunner\<version>`, records the active version in `%LOCALAPPDATA%\Programs\PingRunner\current-version.txt`, and creates a Start Menu shortcut.

Reinstall the currently selected version in place:

```powershell
powershell -ExecutionPolicy Bypass -File .\PingerTool\install.ps1 -Reinstall
```

Install a new version while keeping older version folders, then optionally remove older versions:

```powershell
powershell -ExecutionPolicy Bypass -File .\PingerTool\install.ps1
powershell -ExecutionPolicy Bypass -File .\PingerTool\install.ps1 -Reinstall -PruneOldVersions
```

## UI notes

- The desktop app now shows your current public IP in the header area.
- The IP is hidden by default and can be revealed or hidden again with the adjacent button.


## License

[PolyForm Noncommercial 1.0.0](LICENSE.md) — free for personal and non-commercial use; selling or other commercial use requires permission.
