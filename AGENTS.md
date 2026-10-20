# Repository working instructions

## Publishing and downloads

- Use the GitHub connector, API, or CLI for repository and release work. Do not control the user's desktop/browser unless the user specifically asks for it or approves it when needed.
- The default release Assets contain installation packages only: Windows ZIP, Linux install archive, and macOS app kit.
- Keep two working source download links (ZIP and tar.gz) in the release download table. Do not add explanations about automatic archive generation or guaranteed UI visibility to README/release notes. Preserve the source tag.
- Display actual filenames. Do not set descriptive asset labels that hide filenames.
- Put installation guides, usage notes, screenshots/theme previews, and package SHA256 values in README/release notes. Do not attach them as separate release assets.
- Package each OS separately: Windows ZIP excludes Linux/macOS folders and directs their documentation links to GitHub. Linux and macOS use their own packages.
- When updating a published release, preserve installation packages by default. If the user authorizes repackaging, stage and verify the replacement before swapping it, keep executable bytes and download filenames/URLs unchanged, and update the SHA256 in the release body. Otherwise preserve bytes and hashes. Move the checksum values and any needed guide/image links into the body before removing duplicate non-installation attachments.
- The release organization workflow only removes explicitly recognized duplicate guide/preview/checksum names. Keep unrecognized assets and ask the user if a new installer format, new resource, or exception to the agreed layout is needed.
- If the user asks to publish or update main/releases, that request authorizes the stated changes. Do not ask for the same approval again; ask only for missing information, new scope, or a required permission.
- Preserve personal Data directories, registered shortcuts, notes, and images; never include them in commits or release packages.

## Release cadence

- Do not create a release, tag, or version bump for every chat message or small change. Routine fixes and documentation changes accumulate on main after validation.
- Publish a release when a meaningful group of changes has accumulated, a major feature or development milestone is complete, or the user explicitly requests a release.
- Updating GitHub/main does not by itself request a release. Keep unreleased changes recorded so the next release can summarize the accumulated work.
- Preserve existing published releases; do not delete or consolidate them unless the user asks.

## Release note style

- Use v7 and v8.1 as the style reference: one short paragraph focused on what changed and its user-visible benefit; use a few short bullets only when clearer.
- Keep future release notes concise. Do not reproduce the README, the full feature list, theme galleries, lengthy installation guides, or routine test counts.
- Include a concrete before/after result or measurement only when it explains the main change.
- Use at most one brief installation reminder or a README link when needed.
- Keep necessary download links and checksums compact and secondary to the change summary; prefer a collapsed section for SHA256 values.
- This preference applies to future releases. Leave the current v8.3 release text unchanged unless the maintainer explicitly requests another edit.

- Release organization runs on publication or manual dispatch only. Editing automation must not rewrite the existing v8.3 release.
