# Rhodium Vault

A local, encrypted password manager for Windows. No accounts, no cloud, no server that can be breached - your vault never leaves your PC.

Part of the [Rhodium Software](https://rhodium-software.de) family of small, focused desktop tools.

## Features

- Master-password protected vault, stored as a single encrypted file on your machine
- Add, edit, delete and search password entries (title, username, password, URL, notes)
- Built-in cryptographically secure password generator
- Favorites and "last changed" tracking with a staleness warning after 1 year
- Secure clipboard copy - excluded from Windows clipboard history, auto-clears after 25 seconds
- Auto-lock after 5 minutes of inactivity
- Global hotkey (`Ctrl+Shift+V`) to bring the vault to front from anywhere
- Change your master password at any time (re-encrypts the whole vault)

## Security

- **Key derivation:** [Argon2id](https://en.wikipedia.org/wiki/Argon2) (via `Konscious.Security.Cryptography`), using OWASP baseline parameters (19 MiB memory, 2 iterations, 1 degree of parallelism)
- **Encryption:** AES-256-GCM (authenticated encryption - a wrong master password or tampered file is rejected outright, never silently decrypted into garbage)
- The master password itself is never stored anywhere, not even hashed - it only exists transiently to derive the encryption key
- 100% offline. No network calls, no telemetry, no analytics

**Honest limitations:** this is a solo-built project using well-established cryptographic primitives, but it has not been through an independent security audit. Managed-memory languages like .NET also can't guarantee secrets are wiped from RAM. If you need password management for high-stakes/business-critical accounts, an established, independently-audited tool (Bitwarden, KeePass) is the safer choice. Rhodium Vault is built for everyday personal use.

There is intentionally no password-reset flow: if you forget your master password, your data cannot be recovered.

## Download

Prebuilt installer: see [rhodium-software.de](https://rhodium-software.de) (or the [Releases](../../releases) page, once published).

