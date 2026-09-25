# Releasing

A release is a `vX.Y.Z` tag on `main` whose number matches `Version.props`. The release workflow
builds the installer from that tag and drafts a GitHub Release. Nothing reaches users until someone
publishes the draft.

## Versioning

- `Version.props` holds the next release's number. Bump it right after a release, or in the release
  commit if it was not bumped yet.
- Major: a change that breaks settings or the CSV format, or a redesign. Minor: new features. Patch:
  fixes only.
- Local builds and tests are always `X.Y.Z-dev` and keep their settings in `%LOCALAPPDATA%\PingRunner Dev`.
  Only builds made with `-p:PingRunnerReleaseBuild=true` carry the plain version.

## Steps

1. Start from a clean `main` with CI green.
2. In one commit, `chore(release): X.Y.Z`: set `Version.props` if needed and rename
   `## [Unreleased]` in `CHANGELOG.md` to `## [X.Y.Z] - YYYY-MM-DD`, with a fresh empty
   `[Unreleased]` above it.
3. Build the installer locally and try it:

   ```powershell
   .\installer\build-installer.ps1
   .\installer\dist\PingRunner-X.Y.Z-setup.exe
   ```

   Check a fresh install, an upgrade over the previous version (settings survive), launching from the
   Start menu, and uninstalling (program files go, `%LOCALAPPDATA%\PingRunner` stays).
4. Tag and push:

   ```powershell
   git tag vX.Y.Z
   git push origin main vX.Y.Z
   ```

5. The release workflow checks the tag against `Version.props`, runs the tests, builds
   `PingRunner-X.Y.Z-setup.exe` and its `.sha256`, and drafts the release with the changelog section as
   its notes.
6. Download the draft's installer, compare its hash with the `.sha256`, and publish the draft. The
   in-app update check sees it from then on.

## Recovery

- A wrong tag: delete the draft and the tag (`git push origin :refs/tags/vX.Y.Z`), fix, and tag again.
- A bad published release: publish a patch release. Users can also reinstall the previous installer
  over it; settings are not touched.

## Signing

The installer is not Authenticode-signed yet. When a certificate exists, sign
`installer/dist/PingRunner-X.Y.Z-setup.exe` before hashing it and run the build with `-RequireSigned`.
Keep the certificate and its password out of the repository.
