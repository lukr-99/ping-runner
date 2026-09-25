# Ping Runner architecture

## Context

One person at one Windows PC wants to know why their connection misbehaves. Ping Runner measures it
from that PC: ICMP pings to a host they choose, and HTTP transfers to Cloudflare for throughput. It has
no server, no account and no telemetry. It keeps `settings.json` and the measurement history,
`history.db`, on that PC.

## Modules and dependencies

```text
PingRunner.App (WPF, composition root) ---> PingRunner.Core <--- PingRunner.Infrastructure
PingRunner.Cli (console)               ---> PingRunner.Core
        \------------------------------------> PingRunner.Infrastructure
```

`PingRunner.Core` (`net10.0`) references nothing else in the solution and does no I/O. Every outside
call goes through an interface that Infrastructure implements and tests replace.

| Folder in Core | Holds | Seam |
| --- | --- | --- |
| `Pinging` | `PingRunSettings` (validated form), `PingLoop` (fixed cadence on a `TimeProvider`), `PingAttempt` | `IPingSender` |
| `Statistics` | `PingStatistics`, `LatencyDistribution`, `Outage`, `CallQuality` | none, pure |
| `Sessions` | `PingSession` (the live, bounded buffer), `PingAttemptCsv` (writes the file format) | none |
| `Importing` | `PingRowReader` (columns by header, values as people write them), `PingCsvReader`, `PingFileImporter` (reader by extension), `PingImport` | `IPingFileReader` |
| `Reports` | `ReportBuilder` (statistics, clock-aligned slices, findings, method), `ConnectionReport`, `ReportText` | `IReportWriter` |
| `Formatting` | `Units`: how every measurement is written, shared by the app and reports | none |
| `History` | `RunRecorder` (saves each run as it goes), `RunRecord` (recorded or imported), `RunSummary`, `SpeedTestRecord` | `IPingRunHistory`, `ISpeedTestHistory`, `IHistoryMaintenance` |
| `Throughput` | `ThroughputMeter`, `ThroughputTest` (parallel streams, sampling) | `IThroughputEndpoint` |
| `SpeedTest` | `SpeedTestRunner` (idle, download, upload, with loaded pings), `Bufferbloat` | uses both seams above |
| `Graphing` | `GraphRange`, `GraphZoom` (zoom and pan maths), `LatencyDownsampler` | none |
| `Settings` | `AppSettings` (versioned, normalized on load) | `ISettingsStore` |
| `Network` | `ConnectionSnapshot` | `IConnectionInfoSource`, `IPublicIpSource` |
| `Updates` | `ReleaseVersion`, `UpdateCheck` | `IReleaseSource` |

`PingRunner.Infrastructure` (`net10.0`) has one adapter per seam: `IcmpPingSender`,
`CloudflareSpeedEndpoint`, `SystemConnectionInfoSource`, `PublicIpService`, `JsonSettingsStore`,
`GitHubReleaseSource`, the SQLite history (`SqliteHistoryDatabase`, `SqlitePingRunHistory`,
`SqliteSpeedTestHistory`, `SqliteMigrator`), `ExcelPingReader` (ClosedXML), the report writers
`PdfReportWriter` (PDFsharp and MigraDoc, with `WindowsFontResolver`) and `ExcelReportWriter`
(ClosedXML), and `AppDataPaths` for where files go. The file readers and report writers only touch the
stream they are handed, so `AppAdapters` sets the real ones by default and tests use them too.

`PingRunner.App` (`net10.0-windows`, WPF UI 4.3, CommunityToolkit.Mvvm) holds the views, view models,
charts and theming. Its composition root is `Composition/AppGraph`, built from an `AppAdapters` set:
`AppAdapters.ForUser` creates the real adapters and owns their HTTP clients, while tests pass fakes and
a fake clock. `App.xaml.cs` builds the graph and shows `Shell/MainWindow`.

The shell follows GoalMaker's Windows app. A `FluentWindow` hosts a left `NavigationView`; `PageProvider`
hands it pages built from the graph and keeps them, so a zoomed graph or a finished speed test is still
there when you come back. View models ask for a page change through `ShellNavigation`.

## Data flow

A run starts on the Monitor page. `MonitorViewModel` validates the form into `PingRunSettings`, saves
the form to settings, and awaits `PingSession.RunAsync` on the UI thread. `PingLoop` sends one ping per
interval; each attempt lands in the session's buffer (50,000 by default, oldest dropped first) and the
session raises `AttemptRecorded` on the UI thread. View models only mark themselves dirty. A
`DispatcherTimer` in `AppGraph` calls `Refresh` four times a second, which recomputes
`PingStatistics` from a snapshot. Even at the 100 ms minimum interval the UI does a bounded amount of
work.

The Graph page reads the same session, or an imported CSV, applies a `GraphRange`, then a `GraphZoom`,
and computes statistics for exactly the visible attempts. `LatencyChart` thins what it draws to about one
point per two pixels but keeps every lost ping and each bucket's fastest and slowest reply.

`RunRecorder` follows the same session. When a run starts it stores the run, then writes attempts every
50 pings or 5 seconds, and when the run ends it loads them back, computes the `RunSummary` and finishes
the run. Store work runs in order on the thread pool, so the UI never waits on the disk. At start-up it
finishes any run still marked running as "interrupted". The History page reads the stored runs; opening
one hands its attempts to the Graph page, the same way an imported CSV does. A finished speed test is
stored whole, series and latency samples included.

A speed test runs `SpeedTestRunner`: up to ten idle pings to 1.1.1.1 (it gives up after three silent
ones), then download and upload through `ThroughputTest`. Each direction starts its streams on the thread
pool, samples the shared byte counter every 200 ms, reports progress to the page, and keeps pinging
underneath for loaded latency.

An import reads a file through `PingFileImporter`, which picks `PingCsvReader` or `ExcelPingReader` by
extension. Both find columns by header through `PingRowReader`, so a column order or a spreadsheet's
re-save does not matter, and both leave unreadable rows out with their line numbers. The Graph shows an
import directly; the History stores it with `IPingRunHistory.ImportRunAsync`, one run per target.

A report starts on the Reports page. `ReportsViewModel` gathers the pings through `ReportSources` (the
session, the Graph's source, one stored run, or a target's stored runs cut to a date range), and
`ReportBuilder` turns them into a `ConnectionReport`: the statistics, at most 48 clock-aligned slices,
the speed tests inside the period, findings and method notes, all in the offset the pings were measured
in. The preview shows its figures and findings. Saving adds the connection snapshot and, only if ticked,
the public IP, draws the charts with `Reports/ReportChartRenderer` (off screen, light palette, the
app's accent, twice the layout size) and hands the report to the chosen `IReportWriter`.

## Outside services

| Service | Used for | When | Limits |
| --- | --- | --- | --- |
| ICMP to the chosen host | Every ping | While a run is going | Per-ping timeout from the form (10 ms to 60 s) |
| `speed.cloudflare.com` `__down` / `__up` | Throughput | Only when a speed test starts | Test length, then cancelled |
| ICMP to `1.1.1.1` | Idle and loaded latency | During a speed test | 1 s timeout |
| `api.ipify.org`, then `checkip.amazonaws.com` | Public IP | When the Connection page opens or refreshes | 15 s client timeout, 10 s per lookup |
| `api.github.com` releases/latest | Update check | Only when you press the button | 15 s |

Every request carries the user agent `PingRunner/<version>`. No request sends anything about the user.
Responses are untrusted: the IP must parse as an address, and a release must have a version tag and a
`https://github.com` page, or the check reports a failure.

## Data

`settings.json`, format version 1, owned by `JsonSettingsStore`, in `%LOCALAPPDATA%\PingRunner` (or
`PingRunner Dev`). Saves write a temporary file and move it over the old one. Loading clamps every
value to its range, so a hand-edited file cannot start the app in a broken state. A future format change
bumps `AppSettings.CurrentVersion` and migrates in `Normalized`.

`history.db`, SQLite, owned by `SqliteHistoryDatabase` in the same folder. Tables: `ping_runs` (one row
per run with its summary), `ping_attempts` (every attempt, keyed by run and sequence, deleted with its
run), `speed_tests` (series and latency samples as JSON arrays), and `schema_migrations`. Times are
stored as Unix milliseconds plus the local offset in minutes, so records read back exactly as written.

- **Migrations** are immutable files in `src/PingRunner.Infrastructure/History/Migrations`, numbered
  from `0001` with no gaps, embedded in the assembly and applied one transaction each by
  `SqliteMigrator`. `schema_migrations` records each file's number, name and SHA-256, following
  CodePrint's `tools/migrations.py`, which CI runs on the same folder. A database that lists a migration
  this build does not know (a newer Ping Runner) or whose applied file changed is refused, never
  repaired; the app then runs without history and says why.
- **Access** is serialized: every operation opens an unpooled connection on the thread pool under one
  lock, so a restore can swap the file with nothing holding it open.
- **Backup** uses SQLite's online backup to a file the user picks. **Restore** copies the chosen file,
  checks it has the Ping Runner tables, runs `integrity_check` and `foreign_key_check`, migrates the copy,
  checks again, keeps the current file as `history.before-restore.db`, and only then moves the copy
  into place. Any failure leaves the current history untouched.
- **Retention**: nothing is deleted automatically. Runs and speed tests go when the user deletes them
  or clears the history.

`ping_runs.source` (`recorded` or `imported`) and `source_name` came with migration `0002`; runs from
before it read as recorded. CI tests `0002` from `0001` with representative rows
(`tests/PingRunner.Infrastructure.Tests/History/Fixtures`), and so does `SqliteMigratorTests`.

The session CSV (`Timestamp,TargetHost,IsSuccess,RoundtripTimeMilliseconds,Details`) is the exchange
format, unchanged from 1.x. Import is looser than export: it takes the columns by name and in any order,
caps line length, row count and workbook size, and leaves bad rows out with their line numbers. The
Excel report's Pings sheet uses the same column names, so a report imports back.

## Theming

`Theming/ThemeApplier` sets semantic brushes (`PR.BackgroundBrush`, `PR.SurfaceBrush`,
`PR.TextPrimaryBrush`, `PR.AccentBrush`, `PR.DangerBrush` and the rest) from `NeutralPalette` (light
or dark) and `AccentPalette` (teal, blue, violet, amber), and applies WPF UI's theme to match. Views read
only the `PR.*` keys through `DynamicResource`, so a switch shows at once. The accent reaches every page
through shared pieces: `Controls/PageHeader` (the page icon on `PR.AccentSoftBrush`), the icon badge
in `Controls/StatTile`, `PR.SectionLabel`, the `PR.StatusPill` while a run is going, and
`PR.TonalButton` (in `App.xaml`) for every secondary action. WPF UI builds some accent
brushes once per theme, so a window open across a switch kept the old accent; `ThemeApplier` writes
fresh brushes under those keys on every apply, the same fix GoalMaker uses.

## Delivery

`Version.props` holds the version. `Directory.Build.props` appends `-dev` unless the build passes
`-p:PingRunnerReleaseBuild=true`, and stamps the result into the assembly, where `BuildInfo` reads it.
Dev builds show their version in the title bar and keep their settings in a separate folder.

`installer/build-installer.ps1` publishes a self-contained win-x64 build and compiles
`installer/PingRunner.iss` into a per-user Inno Setup installer with a SHA-256 file beside it. The
installer's `AppId` never changes. It removes the 1.x copy under `%LOCALAPPDATA%\Programs\PingRunner`,
which held only program files. The release workflow drafts a GitHub Release from a `v*` tag; see
[docs/releasing.md](docs/releasing.md).

The update check follows CodePrint's split (release source, version policy, manual install), minus the
download and launch steps: it tells you a newer release exists and opens its page.

## Tests

Core and Infrastructure tests are plain xUnit v3 on Microsoft.Testing.Platform. Timing tests drive a
`FakeTimeProvider`, HTTP tests use a stub handler, file tests use temporary folders, and the only real
network use is a loopback ping. App tests run on one STA thread with the app's resources loaded
(`Hosting/WpfHost`), build the whole graph on fakes, and include `PageRenderTests`, which shows the real
window off screen, visits every page in both themes and fails on any binding error.

## Decisions

- [0001: WPF UI instead of dotnetlib](docs/adr/0001-wpf-ui-instead-of-dotnetlib.md)
- [0002: Cloudflare for the speed test](docs/adr/0002-cloudflare-for-the-speed-test.md)
- [0003: SQLite for the history](docs/adr/0003-sqlite-for-the-history.md)
- [0004: PDFsharp and ClosedXML for reports](docs/adr/0004-pdfsharp-and-closedxml-for-reports.md)

## Known constraints

- Windows limits ICMP in ways the app cannot see. Some hosts and networks drop pings, and some
  firewalls rate-limit them, which shows up as loss that ordinary traffic would not have.
- Round-trip times come from Windows in whole milliseconds, so a LAN host often reads 0 or 1 ms.
- Upload bytes are counted as they are written to the socket, a little ahead of what the server has
  received. Leaving the first second out of the average absorbs most of that.
- The speed test measures the path to Cloudflare's nearest data centre, not to any other server.
- A report's connection details and public IP are read when the report is made, which may be long
  after the pings; the report says so.
- Excel stores times without an offset, so the workbook writes clock times in the measured offset and
  puts the offset in its own column or header.
