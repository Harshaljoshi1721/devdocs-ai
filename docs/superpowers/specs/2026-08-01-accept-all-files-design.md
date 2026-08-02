# Accept-all-files ingestion (Claude Code / Codex style)

**Date:** 2026-08-01
**Status:** Approved

## Goal

Index **every text file of any type** a user uploads or connects, instead of only a
hardcoded extension allowlist. Match how Claude Code / Codex behave: read all source/text,
skip dependency & build junk, never index secrets or binaries.

## Decisions (confirmed with user)

- **Junk folders** (`node_modules`, `.git`, `dist`, `build`, `.next`, `out`, `coverage`,
  `bin`, `obj`, `.vscode`, `.idea`) — still auto-skipped (client-side).
- **Secrets** (`.env*`, `.pem/.key/.crt/...`, `id_rsa`, …) — still auto-skipped.
- **Binaries** — auto-skipped (can't be embedded meaningfully).
- Per-file 5 MB cap, ≤50 client batching, GitHub repo caps — unchanged.

## Backend

`ExtensionFileFilter` moves from allowlist to **accept-by-default**. New `IFileFilter`:

```
bool IsSecret(string fileName);            // unchanged denylist
bool IsBinaryExtension(string fileName);   // NEW: known-binary extension denylist
bool LooksBinary(ReadOnlySpan<byte> sample); // NEW: NUL byte in first 8 KB => binary
bool IsAllowed(string fileName);           // cheap filename pre-filter: !IsSecret && !IsBinaryExtension
FileType Categorize(string fileName);      // unchanged mapping; unknown text => Other
```

Removes `IsSupported` (allowlist). Binary-extension denylist covers images, video, audio,
archives, compiled objects, office/pdf, fonts, db files, design files. `.svg` is treated as
text. Content sniffing (`LooksBinary`) catches binaries with unknown/no extension.

**DocumentIngestor.IngestAsync** — buffer content (≤5 MB) into a `MemoryStream`, then:
`empty` → `secret` → `binary` (extension) → `binary` (content sniff on 8 KB sample) →
dedupe → store. No allowlist check. Rejection reasons: `empty | secret | binary |
too_large | duplicate` (no more `unsupported`).

**RepositoryIngestor** — pre-filter `if (!IsAllowed(path)) continue`; the content binary
sniff still runs inside `IngestAsync`.

## Frontend

Relax `isIgnoredPath` in `frontend/src/lib/upload.ts`: keep the explicit junk-dir set, add
`.DS_Store`/`Thumbs.db`, but **stop blanket-skipping every dotfile** so `.github/workflows/*`,
`.gitignore`, `.eslintrc`, etc. upload. Backend still blocks true secrets.

## Testing

- TDD: rewrite `FileFilterTests`, update `DocumentEndpointsTests` &
  `RepositoryIngestorTests`, add binary-detection tests. Relax `upload.test.ts`.
- Full backend suite + frontend vitest/lint/build.
- Live: upload `.rb`, `.go`, no-extension, binary `.png`, `.env` → verify accept/reject.
- Responsive UI pass at 375 / 768 / 1280 across landing, dashboard, overview, chat.
