# Rhodium Vault

A local, encrypted password manager for Windows. No accounts, no cloud, no server that can be breached - your vault never leaves your PC.

Part of [Rhodium Software](https://rhodium-software.de), a set of small, focused desktop tools.

## Features

- Master-password protected vault, stored as a single encrypted file on your machine
- Add, edit, delete and search password entries (title, username, password, URL, notes)
- Built-in cryptographically secure password generator
- Favorites and "last changed" tracking with a staleness warning after 1 year
- Clipboard copy that asks Windows and clipboard managers not to record the item, and auto-clears after 25 seconds (also on lock/exit). These flags are requests: software that ignores them can still read the clipboard.
- Auto-lock after 5 minutes of inactivity; closing the window locks the vault and keeps the app in the tray
- Atomic saves with an automatic `vault.dat.bak`, plus a Backup button for the encrypted vault file
- Global hotkey (`Ctrl+Alt+Shift+V`) to bring the vault to front from anywhere
- Change your master password at any time (re-encrypts the whole vault)

## Security

- **Key derivation:** [Argon2id](https://en.wikipedia.org/wiki/Argon2) (via `Konscious.Security.Cryptography`), with parameters stored in the file header (default 64 MiB memory, 3 iterations, 4 lanes) so they can be raised later; the header is authenticated
- **Encryption:** AES-256-GCM (authenticated encryption - a wrong master password or tampered file is rejected outright, never silently decrypted into garbage)
- The master password itself is never stored anywhere, not even hashed - it only exists transiently to derive the encryption key
- 100% offline. No network calls, no telemetry, no analytics

**Honest limitations:** this is a solo-built project using well-established cryptographic primitives, but it has not been through an independent security audit. Managed-memory languages like .NET also can't guarantee secrets are wiped from RAM. If you need password management for high-stakes/business-critical accounts, an established, independently-audited tool (Bitwarden, KeePass) is the safer choice. Rhodium Vault is built for everyday personal use.

There is intentionally no password-reset flow: if you forget your master password, your data cannot be recovered.

## Download

Prebuilt installer: see [rhodium-software.de](https://rhodium-software.de) (or the [Releases](../../releases) page, once published).

## Building from source

Requirements: [.NET SDK](https://dotnet.microsoft.com/) (net10.0-windows), Windows.

```
dotnet build
dotnet test tests/RhodiumVault.Tests
```

To produce a self-contained installer build:

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Then compile `RhodiumVault.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php) to produce the `.exe` installer.

## License

All rights reserved. No license is currently granted for reuse or redistribution.
