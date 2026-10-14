# Repository working instructions

## Publishing and downloads

- Use the GitHub connector, API, or CLI for repository and release work. Do not control the user's desktop/browser unless the user specifically asks for it or approves it when needed.
- The default release Assets contain installation packages only: Windows ZIP, Linux install archive, and macOS app kit.
- Keep GitHub's two automatic source archives, **Source code (zip)** and **Source code (tar.gz)**. Do not upload duplicate source archives or change/remove the release tag to hide them.
- Display actual filenames. Do not set descriptive asset labels that hide filenames.
- Put installation guides, usage notes, screenshots/theme previews, and package SHA256 values in README/release notes. Do not attach them as separate release assets.
- When updating an already published release, preserve installation package names, URLs, bytes, and hashes. Move the checksum values and any needed guide/image links into the body before removing duplicate non-installation attachments.
- The release organization workflow only removes explicitly recognized duplicate guide/preview/checksum names. Keep unrecognized assets and ask the user if a new installer format, new resource, or exception to the agreed layout is needed.
- If the user asks to publish or update main/releases, that request authorizes the stated changes. Do not ask for the same approval again; ask only for missing information, new scope, or a required permission.
- Preserve personal Data directories, registered shortcuts, notes, and images; never include them in commits or release packages.
