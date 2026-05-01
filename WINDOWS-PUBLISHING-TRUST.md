# Windows Publishing Trust for PMG

This note summarizes practical ways to reduce Windows "Unknown Publisher" friction for Pitmasters Grill releases without changing PMG build identity or adding secrets to the repository.

## Strong-Name vs Authenticode

`.NET` strong-name signing and Windows Authenticode signing solve different problems.

- Strong-name signing identifies and version-protects a managed assembly for `.NET` loading scenarios.
- Authenticode signing identifies the software publisher to Windows and SmartScreen-style trust flows.

Strong-name signing does **not** satisfy Windows publisher trust prompts for downloaded desktop apps.

## Does strong-name signing help with Windows trust prompts?

No, not in the way PMG users care about for download/install friction.

- It can help with assembly integrity and dependency identity inside the `.NET` ecosystem.
- It does not make the EXE show a verified publisher in Windows download or launch prompts.
- It does not replace code signing for SmartScreen or "Unknown Publisher" warnings.

## Authenticode signing options

PMG can reduce trust friction by Authenticode-signing the shipped Windows artifact.

Common targets:

- the main `PitmastersGrill.exe`
- a setup/bootstrapper EXE, if PMG later ships one
- an MSIX/AppX package, if PMG later moves to packaged distribution

Operationally, Authenticode usually means:

- obtain a code-signing certificate from a public CA
- keep the private key outside the repo
- sign artifacts during release packaging
- preferably timestamp signatures so old releases remain valid after certificate expiration

## MSIX / Microsoft Store option

If PMG later moves to `MSIX`, Windows gets a more standard app-install experience and a cleaner trust story than loose ZIP/exe distribution.

High-level tradeoffs:

- better installation/update/uninstall ergonomics
- signing is still required
- packaging and testing complexity increases
- Store distribution adds policy and account overhead
- some power users may still prefer an unpackaged portable-style release

This is relevant if PMG grows into a broader consumer-facing Windows distribution, but it is more process-heavy than simple EXE signing.

## OV code-signing certificate

`OV` (Organization Validation) code-signing is the usual first professional step for an independently distributed Windows desktop app.

High-level profile:

- lower cost than EV
- requires real organization/business identity checks from the CA
- easier operational model than EV
- still requires secure private-key handling outside the repo
- improves publisher identity in Windows prompts, but SmartScreen reputation may still need time/download volume to build

This is usually the most practical path when a project is legitimate, public, and maintained by a real publisher but not yet at large commercial scale.

## EV code-signing certificate

`EV` (Extended Validation) code-signing is the higher-trust, higher-friction option.

High-level profile:

- higher cost
- stricter legal/identity verification
- stronger operational requirements, often involving hardware-backed key storage or managed signing workflows
- better trust posture for Windows reputation-building and enterprise expectations

This is more appropriate once PMG has meaningful adoption, regular release cadence, or a need to minimize trust friction for less technical users.

## Cost, complexity, identity, and risk

At a high level:

- Strong-name signing: low cost, low complexity, no meaningful Windows publisher-trust improvement.
- OV Authenticode: moderate cost, moderate process overhead, requires a verifiable publisher identity, meaningful improvement for Windows publisher identification.
- EV Authenticode: higher cost and higher operational rigor, strongest long-term trust posture for direct Windows distribution.
- MSIX/Store: adds packaging and release-process complexity, but can improve install/update trust and polish when paired with proper signing.

Main operational risks:

- leaking a private signing key
- baking signing secrets into the repo or CI logs
- shipping unsigned fallback artifacts by mistake
- certificate expiration or missing timestamping

PMG should keep all signing credentials out of source control and out of developer worktrees.

## Recommended short-term path

Short term, the best PMG path is:

1. Keep the current unpackaged distribution model.
2. Do **not** spend time on strong-name signing for Windows trust purposes.
3. When ready, add Authenticode signing for release artifacts with an `OV` code-signing certificate.
4. Keep signing keys outside the repo and use timestamping during release signing.

This gives PMG the best cost-to-benefit ratio for reducing "Unknown Publisher" friction without prematurely increasing release complexity.

## Recommended long-term path

If PMG adoption grows, consider this progression:

1. Continue Authenticode signing every public release.
2. Re-evaluate `EV` signing if trust friction, SmartScreen reputation, or enterprise acceptance becomes a recurring issue.
3. Re-evaluate `MSIX` or Store-style packaging if PMG needs smoother installation, updates, and wider non-technical distribution.

For most growth paths, `OV` signing first and `EV` or `MSIX` later is the most practical sequence.
