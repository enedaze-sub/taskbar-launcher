# Release download policy

Agreed with the maintainer on 2026-10-14.

The default download list is **three installation packages plus GitHub's two automatic source archives**:

1. Windows: `taskbar-launcher-vX.zip`
2. Linux: `DeckLauncher-Linux-x86_64-vX.tar.gz`
3. macOS: `DeckLauncher-macOS-vX.tar.gz`
4. Source code (zip)
5. Source code (tar.gz)

Show actual filenames. Installation steps, usage guides, screenshots, theme previews and SHA256 values belong in README/release notes, rather than separate Assets.

Windows ZIP includes Windows files and local Windows documentation only. Link to Linux/macOS documentation on GitHub instead of including their folders. Provide both source download links in the download table without extra explanations about generation or display.

For existing releases, update the body before removing duplicate attachments. Leave package bytes, hashes and URLs unchanged unless the maintainer explicitly authorizes repackaging; then verify a staged replacement, preserve executable contents, and update the release checksum. The organization workflow runs on publication, when its own configuration is updated on main, or manually for a chosen tag. It clears descriptive labels, builds package links and a checksum section, and removes only known duplicate document/image/checksum filenames. It leaves unfamiliar assets intact and reports a warning so the maintainer can decide.

Ask the user when a new installer format, additional downloadable resource or other exception is necessary. Do not introduce a second approval for publication the user already requested. Use connected GitHub tools rather than the user's browser/desktop.

See [repository instructions](../AGENTS.md) and [the workflow](../.github/workflows/release-asset-filenames.yml).

## Future release notes

Follow the concise style of [v7](https://github.com/enedaze-sub/taskbar-launcher/releases/tag/v7) and [v8.1](https://github.com/enedaze-sub/taskbar-launcher/releases/tag/v8.1). Lead with a short explanation of the core change and the resulting behavior. Use a few brief bullets when they make multiple changes easier to scan.

Detailed feature lists, installation instructions, theme previews and routine test counts belong in README or linked documentation. Add only a short installation reminder or README link when necessary. Keep download links compact and move SHA256 values into a collapsed section below the core notes.

Apply this style starting with the next release. The maintainer asked to keep the current v8.3 release text as it is.
