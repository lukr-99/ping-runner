# Ping Runner

Ping Runner is a Windows app for finding out what is wrong with an internet connection. It pings a
host for as long as you like and shows latency, jitter, packet loss and outages as they happen, and it
runs a speed test that also measures how much a full line slows everything else down.

![The Monitor page during a 16-minute run against 8.8.8.8](docs/assets/monitor-light.png)

## Status

Version 2.0.0 is the current release and the first one published on GitHub. It replaces the 1.x window
and pop-up graph with one window and five pages. Local builds report themselves as `2.0.0-dev`.
Version 1.1.0 was installed with the old `install.ps1`; the 2.0 installer removes that copy.

## What it shows

**Monitor.** Enter a host or IP, pick an interval, a timeout and how long to run, and start. Eight
figures update a few times a second:

| Figure | How it is worked out |
| --- | --- |
| Average latency | Mean round-trip time of the replies, with the fastest and slowest. |
| Median | Middle reply time, with the 95th and 99th percentiles (linear interpolation). |
| Jitter | Mean change in round-trip time between consecutive replies; lost pings in between are skipped. |
| Packet loss | Lost pings as a share of pings sent. Over 1 % turns amber, over 5 % red. |
| Call quality | A mean opinion score from 1 to 4.5, estimated with the simplified ITU-T G.107 E-model most network monitors use. A guide, not a measured call. |
| Outages | Runs of two or more lost pings in a row, with the longest. One lost ping is loss, not an outage. |
| Pings sent | Sent and received, and the time the session covers. |
| Worst spikes | Mean of the 50 slowest replies. |

The page also draws the last 120 pings and lists the newest 200. The copy button puts a text summary of
the session on the clipboard, ready for a support ticket.

**Graph.** The whole session, or a CSV you import, narrowed to the last N pings or a time span. Scroll
to zoom at the pointer, drag to pan, double-click to reset, and hover for the exact ping. The figures
above the chart describe only what is on screen. Export the visible part or everything as CSV, or the
chart and its figures as PNG.

![The Graph page in dark mode, showing the last 300 pings with one lost ping marked](docs/assets/graph-dark.png)

**Speed test.** Ten pings on an idle line, then ten seconds of download and ten of upload over four
parallel streams against Cloudflare's speed-test service, pinging 1.1.1.1 the whole time. The result
has the average rate and the best rate held for a full second, both leaving out the first second
while TCP ramps up, idle latency, latency under load and a bufferbloat grade from A+ to F. Length and stream count
are in Settings. A test on a fast line moves several hundred megabytes.

![A finished speed test: 443 Mbps down, 38.7 Mbps up, bufferbloat A+](docs/assets/speedtest-light.png)

**Connection.** The adapter that carries the default route: type, link speed, local addresses, gateway
and DNS servers, each with a button that fills in the Monitor. "Open router" opens the gateway's admin
page (`http://<gateway>/`) in your browser. Ping the gateway first. Loss there
points at Wi-Fi or cabling; loss only to public hosts points past the router. The public IP is looked
up when the page opens and stays hidden until you show it.

![The Connection page in dark mode](docs/assets/connection-dark.png)

**Settings.** Light, dark or Windows theme, four accent colors, how many pings the live session keeps
(10,000 to 500,000), speed-test length and streams, and an update check.

There is also a console version: `pingrunner` asks for a host and prints each ping and a summary, and
`pingrunner speed [seconds] [streams]` runs the speed test.

## Requirements

- Windows 10 or 11, x64. The installer brings its own .NET runtime.
- Some networks and hosts drop ICMP. When a host never answers, every ping shows as lost; try another
  target such as your gateway.
- To build: the .NET 10 SDK. To build the installer: Inno Setup 6.

## Install

Download `PingRunner-<version>-setup.exe` and its `.sha256` from the
[Releases](https://github.com/lukr-99/ping-runner/releases) page, check the hash, and run it. It
installs for the current user only, without administrator rights, to
`%LOCALAPPDATA%\Programs\Ping Runner`. The installer is not code-signed yet, so Windows SmartScreen
may warn before it runs.

```powershell
(Get-FileHash .\PingRunner-2.0.0-setup.exe -Algorithm SHA256).Hash.ToLower()
```

To build the installer yourself, see [Delivery](#delivery).

## Build and test

From the repository root:

```powershell
dotnet build PingRunner.slnx -c Release
dotnet test --solution PingRunner.slnx -c Release
dotnet run --project src\PingRunner.App\PingRunner.App.csproj
```

The tests need no network beyond loopback ping. [CONTRIBUTING.md](CONTRIBUTING.md) has the full
check that CI runs, and how to regenerate the screenshots above.

## Architecture

Four projects with dependencies pointing inward: `PingRunner.Core` (statistics, the ping loop, the
speed test, formats; no I/O), `PingRunner.Infrastructure` (ICMP, HTTP, the adapter list, the settings
file), `PingRunner.App` (WPF UI and the composition root) and `PingRunner.Cli`. See
[ARCHITECTURE.md](ARCHITECTURE.md) for the seams, the data flow and the outside services the app talks
to, and [CONTEXT.md](CONTEXT.md) for what each measurement means.

## Data safety

Ping Runner stores one file, `settings.json`, in `%LOCALAPPDATA%\PingRunner` (`PingRunner Dev` for
`-dev` builds). It holds appearance, the last ping form and recent targets, with a `Version` field for
later migrations. A settings file that cannot be read is kept as `settings.unreadable.json` and the app
starts on defaults. Deleting the folder resets the app; uninstalling leaves it.

Ping sessions live in memory only. To keep one, export it from the Graph page as CSV; importing the
file brings it back. The CSV format is the one 1.x wrote, so old exports still open.

## Delivery

The version lives in [Version.props](Version.props). Every build is `-dev` unless it is made with
`-p:PingRunnerReleaseBuild=true`, which only the installer script and the release workflow do.

```powershell
.\installer\build-installer.ps1        # release installer, installer\dist\PingRunner-2.0.0-setup.exe
.\installer\build-installer.ps1 -Dev   # the same, versioned 2.0.0-dev
```

Pushing a `v2.0.0` tag runs the release workflow, which checks the tag against `Version.props`, runs
the tests, builds the installer and its checksum, and drafts a GitHub Release. The steps before and
after are in [docs/releasing.md](docs/releasing.md). The in-app update check only reads the newest
published release and offers to open its page; it never downloads or runs anything.

## License

[PolyForm Noncommercial 1.0.0](LICENSE.md). Free for personal and other noncommercial use; selling it
or using it commercially needs permission.
