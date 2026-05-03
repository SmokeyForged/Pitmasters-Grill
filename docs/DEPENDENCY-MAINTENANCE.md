# PMG Dependency Maintenance

PMG intentionally keeps a small dependency surface, but it still depends on external .NET packages and GitHub Actions.

The goal of dependency maintenance is not to chase every update immediately. The goal is to make dependency drift and known vulnerabilities visible early enough that maintainers can respond deliberately.

## Sources of dependency visibility

PMG uses three lightweight mechanisms:

1. Dependabot for NuGet package update visibility.
2. Dependabot for GitHub Actions update visibility.
3. A dependency vulnerability check workflow using `dotnet list package --vulnerable --include-transitive`.

## Local vulnerability check

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dependencies\check-dotnet-vulnerabilities.ps1
```

The script checks:

- `PitmastersGrill\PitmastersGrill.csproj`
- `PitmastersGrill.Tests\PitmastersGrill.Tests.csproj`

The script fails when `dotnet list package --vulnerable --include-transitive` reports vulnerable packages.

## Dependency update review process

For dependency update PRs:

- [ ] Read what package or action changed.
- [ ] Check whether the update is routine, security-related, or breaking.
- [ ] Run restore/build/test locally or rely on CI only when the CI result is clear.
- [ ] Confirm PMG still launches if the dependency affects app runtime behavior.
- [ ] Confirm no public-data or EVE boundary changed accidentally.
- [ ] Prefer one dependency family per PR unless updates are tightly related.
- [ ] Merge only after validation passes.

## Vulnerability handling

When a vulnerability signal appears:

1. Identify the affected package.
2. Identify whether PMG uses the affected path.
3. Prefer updating to a patched version if available.
4. Run normal validation.
5. Add release-note or PR context if the vulnerability was user-relevant.
6. If deferring, document why.

Deferral is acceptable when evidence supports it. Silent deferral is not.

## GitHub settings note

Dependabot configuration can open update PRs, but repository security features such as dependency graph and Dependabot alerts may also need to be enabled in GitHub repository settings.

Suggested GitHub-side checks:

- Dependency graph enabled.
- Dependabot alerts enabled where available.
- Dependabot security updates enabled where appropriate.
- Branch protection requires validation before merge, if the repo is ready for that policy.

## Boundaries

Dependency automation must not change PMG's product boundaries.

It should not:

- add private ESI scopes
- read the EVE client
- inspect traffic
- automate gameplay
- add telemetry
- upload user data
- expand runtime permissions without explicit review

## Release relationship

Before publishing a public release, run the vulnerability check and review any open dependency/security PRs.

This is also listed in `docs/RELEASE-CHECKLIST.md`.
