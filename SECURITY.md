# Security

## Supported versions

Only the latest release gets fixes.

## Reporting

Contact the owner privately through their GitHub profile (`lukr-99`) rather than opening a public
issue for something that could put users at risk. GitHub's private vulnerability reporting is not
switched on for this repository yet.

## Trust boundaries

- **Imported files.** CSV files and Excel workbooks are untrusted. Every row is parsed and checked
  (time, host length, result, a round-trip time from 0 to 10 minutes); a row that fails is left out and
  listed with its line number, never half-read. Lines over 4,096 characters are skipped, files over
  2,000,000 pings and workbooks over 200 MB are refused, and a file with no readable pings is refused
  with the reason. Workbook formulas are read by their saved value and never calculated, and macros in
  `.xlsm` files are never run.
- **Reports.** A report holds what you chose to put in it. The public IP address stays out unless you
  tick it; adapter MAC addresses are never included. Reports are written only to the file you pick.
- **History backups.** A file chosen for restore is untrusted: a copy of it must have Ping Runner's
  tables, pass SQLite's integrity and foreign-key checks before and after migrating, and list no
  migration this build does not know. Only then does it replace the history; the old one is kept.
- **`settings.json`.** Read as untrusted too. Unknown fields are ignored, every value is clamped to its
  range, and a file that does not parse is set aside and replaced by defaults.
- **Network answers.** The public IP must parse as an IP address. The GitHub release must have a
  version tag and a `https://github.com` page. The Cloudflare transfers are only counted, never
  parsed.
- **Opening links.** The app hands the browser only three kinds of address, and only when you click:
  the release page from the update check, the repository, and `http://<gateway>/`, which it builds from
  the gateway's parsed IP address and nothing else. It also opens a report you just saved, or its
  folder, when you press Open or Show in folder.
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
- A broken history file is set aside as `history.unreadable-<time>.db` and a new history starts. A bad
  restore can be undone by restoring `history.before-restore.db`.
- A crash writes `crash.log` to the same folder and names it in the error message.
- A bad release: install the previous release's installer over it. Settings stay where they are.
