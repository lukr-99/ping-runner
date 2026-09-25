# Security

## Supported versions

Only the latest release gets fixes. 2.0.0 is the first release published on GitHub.

## Reporting

Contact the owner privately through their GitHub profile (`lukr-99`) rather than opening a public
issue for something that could put users at risk. GitHub's private vulnerability reporting is not
switched on for this repository yet.

## Trust boundaries

- **Imported CSV files.** Treated as untrusted. Every row is parsed and checked (timestamp, host
  length, boolean, non-negative round-trip time), lines over 4,096 characters and files over 2,000,000
  rows are refused, and a bad row fails the whole import with its line number.
- **`settings.json`.** Read as untrusted too. Unknown fields are ignored, every value is clamped to its
  range, and a file that does not parse is set aside and replaced by defaults.
- **Network answers.** The public IP must parse as an IP address. The GitHub release must have a
  version tag and a `https://github.com` page. The Cloudflare transfers are only counted, never
  parsed.
- **Opening links.** The app hands the browser only three kinds of address, and only when you click:
  the release page from the update check, the repository, and `http://<gateway>/`, which it builds from
  the gateway's parsed IP address and nothing else.
- **Updates.** The app never downloads or runs an update. The update check reads the latest published
  release and opens its page in the browser when you ask it to.
- **Installer.** Per-user, no administrator rights. Releases publish a SHA-256 checksum beside the
  installer. The installer is not Authenticode-signed yet, so the checksum is the integrity check.
- **Privacy.** No telemetry and no account. The app contacts only the hosts listed in
  [ARCHITECTURE.md](ARCHITECTURE.md), each when a feature needs it. The public IP stays hidden on
  screen until you show it, and adapter MAC addresses are never read into the app.

## Recovery

- A broken settings file: the app keeps a copy as `settings.unreadable.json` and starts on defaults.
  Deleting `%LOCALAPPDATA%\PingRunner` resets everything.
- A crash writes `crash.log` to the same folder and names it in the error message.
- A bad release: install the previous release's installer over it. Settings stay where they are.
