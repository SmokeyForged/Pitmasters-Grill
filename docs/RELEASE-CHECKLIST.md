# PMG Release Checklist

This checklist is for maintainers preparing a public PMG release ZIP.

PMG is a Windows desktop app distributed outside a store. Users may see trust prompts, especially while PMG remains unsigned. The goal of this checklist is to make each release repeatable, reviewable, and less dependent on memory.

This checklist does not replace judgment. It gives the release owner a consistent path to follow before publishing.

## Release identity

- [ ] Confirm the intended version number.
- [ ] Confirm the app version in the project file.
- [ ] Confirm README current-release references are correct.
- [ ] Confirm patch notes exist for the release.
- [ ] Confirm release notes accurately describe PMG's public-data boundaries.
- [ ] Confirm update-awareness wording is accurate and does not imply auto-install behavior.
- [ ] Confirm the release does not imply live grid, cloak, private ESI, client-memory, or network-traffic certainty.

## Local validation

Run from the repository root:

```powershell
dotnet restore .\PitmastersGrill\PitmastersGrill.slnx
dotnet build .\PitmastersGrill\PitmastersGrill.slnx --configuration Release --no-restore
dotnet test .\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj --configuration Release --no-build
```

- [ ] Restore succeeds.
- [ ] Release build succeeds.
- [ ] Tests pass.
- [ ] No unexpected generated/runtime files are staged for commit.

## Dependency and vulnerability review

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dependencies\check-dotnet-vulnerabilities.ps1
```

- [ ] Dependency vulnerability check succeeds.
- [ ] Any Dependabot PRs relevant to the release are reviewed.
- [ ] Any dependency update is validated with normal build/test flow before merge.
- [ ] If a dependency issue is intentionally deferred, document why in the release notes or PR.

## Manual smoke test

Use a clean local folder when practical.

- [ ] Launch PMG from a clean extracted copy.
- [ ] Confirm the app opens without crashing.
- [ ] Copy a small representative EVE local-style list.
- [ ] Confirm the Grill board populates.
- [ ] Confirm Analysis still renders.
- [ ] Confirm Settings opens.
- [ ] Confirm Help opens.
- [ ] Confirm Settings -> Version manual update check reports current/latest status or fails clearly.
- [ ] Confirm startup update awareness does not block launch if GitHub/network access is unavailable.
- [ ] Confirm diagnostics export still works.
- [ ] Confirm diagnostics do not expose unwanted local/private data.
- [ ] Confirm double-click / zKill-open behavior still works where available.
- [ ] Confirm Ignore List opens and existing ignore behavior is not obviously broken.

## Artifact creation

- [ ] Publish/build from the expected Release output.
- [ ] Create the release ZIP from the expected files only.
- [ ] Do not include local databases, diagnostics exports, secrets, user cache, or dev-only files.
- [ ] Do not include `.git`, `bin`, `obj`, or temporary workspace files unless the release process explicitly expects them.
- [ ] Name the artifact consistently, including the PMG version.

Example ZIP name:

```text
Pitmasters-Grill-vX.Y.Z-win-x64.zip
```

## Artifact verification

Run the verification helper against the release artifact directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\release-checks\verify-release-artifacts.ps1 -ReleaseDirectory ".\release"
```

The helper should:

- validate ZIP readability
- check for `PitmastersGrill.exe` inside each ZIP
- generate `SHA256SUMS.txt`

Checklist:

- [ ] ZIP opens successfully.
- [ ] ZIP contains `PitmastersGrill.exe`.
- [ ] SHA-256 checksum file generated.
- [ ] Extract ZIP into a clean folder.
- [ ] Launch PMG from the clean extracted folder.
- [ ] Confirm no runtime-only local data was packaged.

## GitHub Release

- [ ] Release title matches the version.
- [ ] Release notes match patch notes.
- [ ] Release notes include relevant public-data limitation language when needed.
- [ ] Release ZIP is attached.
- [ ] `SHA256SUMS.txt` is attached or the checksum is included in the release body.
- [ ] README latest-release link points to the new release.
- [ ] Full release history / patch notes link still works.

## Windows trust note

PMG may still trigger Windows SmartScreen or trust prompts while unsigned.

- [ ] Do not claim code signing unless Authenticode signing is actually implemented.
- [ ] If unsigned, keep the release notes and Windows publishing trust documentation honest.
- [ ] Leave room for future Authenticode signing/timestamping without blocking current releases.

## Post-release

- [ ] Download the release asset from GitHub.
- [ ] Verify checksum against the published checksum.
- [ ] Extract and launch from a clean folder.
- [ ] Confirm the release page displays the intended artifact and notes.
- [ ] Close release-process issues only after the published artifact is verified.
