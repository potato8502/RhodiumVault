# Changelog

## 1.3.0
Security and reliability release, based on a public code review. Existing vaults are upgraded automatically on first unlock (v1 -> v2 format).

- **Atomic saves:** the vault is written to a temp file, flushed to disk, then swapped in; the previous version is kept as `vault.dat.bak`. A crash or full disk can no longer destroy the vault. A damaged `vault.dat` is restored from the backup automatically.
- **Stronger, upgradable key derivation:** Argon2id parameters are now stored in the file header (default 64 MiB / 3 iterations / 4 lanes, up from 19 MiB / 2 / 1). The format version is checked, and old vaults are re-encrypted with the new parameters on first unlock.
- **Authenticated header:** the file header is now covered by AES-GCM as associated data, so tampering with version or KDF parameters is detected.
- **Single instance:** starting the app a second time now brings up the running instance instead of creating a second copy that could overwrite changes.
- **Error handling:** unreadable/locked/corrupt vault files and failed saves show a message instead of crashing; in-memory state is kept consistent with what is on disk.
- **Clipboard:** exclusion flags now use the documented 4-byte DWORD, clipboard monitors are asked to skip the item, and the clipboard is cleared on lock and exit if it still holds the copied secret.
- **Hotkey:** changed from Ctrl+Shift+V (paste-as-plain-text in many apps) to Ctrl+Alt+Shift+V; a failed registration is reported.
- **Closing the window now locks the vault** (it no longer stays unlocked in the tray).
- **Auto-lock:** input in the Add/Edit/Change-password dialogs counts as activity, the vault is never locked while a dialog is open, and a lock while hidden no longer pops up a window.
- **Edit dialog:** the password is masked by default with a Show/Hide toggle.
- **Master password strength** indicator when creating or changing the password.
- **Backup** button (copies the encrypted vault file).
- Tray icon handle leak fixed; key-derivation input bytes are zeroed after use.
- Automated tests (crypto, file format, tamper detection, backup recovery, legacy upgrade) and a CI workflow.

## 1.2.0
- Change master password.

## 1.1.x
- Favorites, password age tracking, tray icon and global hotkey, crash fix on unlock.

## 1.0.0
- Initial release.
