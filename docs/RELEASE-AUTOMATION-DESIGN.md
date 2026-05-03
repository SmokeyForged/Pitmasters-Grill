# PMG Release Automation Design

## Purpose

PMG releases should be repeatable, reviewable, and hard to publish incorrectly.

This document scopes the release-automation path for v1.4.0. It is intentionally a design and guardrail document first. It does not create releases, push tags, upload artifacts, or bypass validation.

## Goals

- Reduce manual release assembly work.
- Preserve PMG's existing release naming and presentation conventions.
- Use the canonical app version source introduced by `PmgReleaseVersion`.
- Produce consistently named release artifacts.
- Generate or prepare checksums for release artifacts.
- Support GitHub release draft or release creation only after validation.
- Keep the operator in control of final publish decisions.

## Non-Goals

- No automatic publish without operator confirmation.
- No automatic merge, tag push, or GitHub release creation from unvalidated code.
- No update/self-updater behavior changes.
- No installer replacement or SmartScreen bypass.
- No rewrite of historical release notes.

## Canonical Release Inputs

Release automation should read from explicit repository state rather than duplicated manual inputs.

Primary input:

```text
PitmastersGrill/PitmastersGrill.csproj -> PmgReleaseVersion
```

Related release files to verify:

```text
README.md
Patch Notes/
docs/RELEASE-CHECKLIST.md
PitmastersGrill/PitmastersGrill.csproj
```

The automation should fail closed if the intended version cannot be identified, if required files are missing, or if current-release references disagree.

## Release Artifact Naming

Artifact naming should preserve PMG's established release style.

Before implementation, inventory the most recent GitHub release assets and document the canonical pattern. The automation should then generate filenames from that pattern rather than relying on ad hoc manual names.

Required properties:

- includes product identity,
- includes version,
- indicates Windows build target when applicable,
- avoids ambiguous duplicate filenames,
- remains stable across releases.

## Release Naming Inventory

The v1.4.0 release-automation work inventoried recent stable GitHub releases before choosing a forward naming convention.

| Release | Tag | Release title | ZIP asset |
|---|---|---|---|
| 1.3.0 | `v1.3.0` | `Pitmasters Grill 1.3.0` | `PMG_General-Release_v1.3.zip` |
| 1.2.0 | `v1.2.0` | `Pitmasters Grill 1.2.0` | `PMG_General-Release_v1.2.zip` |
| 1.1.0 | `v1.1.0` | `Pitmasters Grill 1.1.0` | `PMG_General-Release_v1.1.0.zip` |

Historical asset naming drift exists. v1.2.0 and v1.3.0 used shortened `major.minor` asset names, while v1.1.0 used full semantic version text.

Release automation should tolerate historical drift when reading older releases, but it should generate full semantic version artifact names going forward.

## Canonical Release Naming

Starting with v1.4.0, release automation should use these canonical patterns:

```text
Release title: Pitmasters Grill <major>.<minor>.<patch>
Tag: v<major>.<minor>.<patch>
ZIP asset: PMG_General-Release_v<major>.<minor>.<patch>.zip
```

Example for v1.4.0:

```text
Release title: Pitmasters Grill 1.4.0
Tag: v1.4.0
ZIP asset: PMG_General-Release_v1.4.0.zip
```

The ZIP asset should use the same full version value as `PmgReleaseVersion`, prefixed with `v`.

## Proposed Automation Phases

### Phase 1: Release Readiness Check

A local command or script verifies:

- clean working tree,
- correct branch,
- intended version from `PmgReleaseVersion`,
- tests pass,
- build/publish succeeds,
- patch notes exist for the version,
- README current-release reference matches intended version,
- release checklist has been reviewed,
- no stale current-release references remain.

Output should be a readable release readiness summary.

### Phase 2: Package Artifact Preparation

Automation prepares publish output and release artifacts, but does not publish.

Expected outputs:

- versioned release ZIP,
- checksum file,
- artifact summary,
- release notes source path,
- validation summary.

The script should refuse to overwrite existing artifacts unless explicitly allowed.

### Phase 3: GitHub Release Draft

Automation may create or update a GitHub release draft after validation.

Required behavior:

- use the intended version tag,
- include release notes from the approved source,
- attach the prepared artifact and checksum,
- preserve PMG release naming conventions,
- leave final publish under operator control unless explicitly requested.

### Phase 4: Optional Publish

Publishing a GitHub release should remain a deliberate operator action.

If automated later, it must require a clear confirmation gate and should never run from a dirty tree or failed validation state.

## Safety Rules

Release automation must not:

- create releases from dirty worktrees,
- hide validation failures,
- silently overwrite artifacts,
- create tags/releases with mismatched version metadata,
- upload artifacts whose checksum was not generated or verified,
- bypass human review.

## Validation Expectations

Minimum validation before any release artifact is considered usable:

```powershell
dotnet test .\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj
dotnet build .\PitmastersGrill\PitmastersGrill.csproj
git --no-pager diff --check
```

Release-specific validation should also include:

- publish output exists,
- artifact filename matches canonical pattern,
- checksum exists,
- release notes exist,
- README current release matches intended version,
- GitHub release draft metadata matches intended version.

## Open Questions

- What exact artifact naming pattern should be canonicalized from prior releases?
- Should the first automated flow create drafts only, or also support publishing with confirmation?
- Should release artifacts be generated by Visual Studio publish, `dotnet publish`, or a wrapper script around one of those?
- Where should generated release artifacts live locally before upload?
- Should release notes be copied from `Patch Notes/` directly or composed from a separate release-summary file?

## Initial Recommendation

For v1.4.0, implement automation in small PRs:

1. Design and checklist documentation.
2. Readiness/check-only script.
3. Package artifact preparation.
4. GitHub release draft creation.
5. Optional publish behavior only after the above flow is trusted.
