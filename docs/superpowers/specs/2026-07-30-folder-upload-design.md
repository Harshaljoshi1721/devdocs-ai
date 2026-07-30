# Folder upload for Documents

**Date:** 2026-07-30
**Status:** Approved
**Scope:** Frontend only. No backend or API changes.

## Problem

The Documents panel only lets a user pick individual files (`<input type="file" multiple>`).
For a "understand my codebase" tool, the natural interaction is pointing it at a whole
project folder. Users must currently hand-select files one directory at a time.

## Goal

Let a user upload an entire folder in one action — "like Codex / Claude Code": pick a
folder of any size and it just works, with dependency/build junk filtered out and no
silent truncation.

## Constraints (existing backend, unchanged)

- `POST /api/v1/projects/{id}/documents` accepts a multipart batch under field `files`.
- `UploadOptions.MaxFilesPerRequest = 50` — hard cap per request.
- `UploadOptions.MaxFileSizeBytes = 5 MB` — per-file cap.
- `ExtensionFileFilter` ingests only an allowlist of code/doc/config extensions and
  rejects secrets. **Note:** `node_modules/*.js` and `*.json` match the allowlist, so
  the backend alone will not keep dependency code out of the index — client filtering
  is required.

## Design

### 1. UI — two buttons
Keep `Upload files` (unchanged). Add `Upload folder` beside it. Two hidden inputs:

- files: `<input type="file" multiple>`
- folder: `<input type="file" multiple webkitdirectory>`

Both feed the same upload pipeline.

### 2. Client-side filtering — `filterUploadFiles(files)`
Pure function. Drops files whose `webkitRelativePath` contains any ignored path segment:

```
node_modules, .git, dist, build, .next, out, coverage, bin, obj, .vscode, .idea
```

plus any file or directory segment beginning with `.` (dotfiles/dotfolders).

Returns `{ accepted: File[], skippedCount: number }`.

### 3. Any-size upload — `chunk(items, size)`
Pure function that splits the accepted files into groups of `<= 50`
(`MaxFilesPerRequest`). Batches are POSTed **sequentially**; results are aggregated.
Button shows live progress: `Uploading 120 / 340…`.

### 4. Preserve folder structure
Each file is appended to `FormData` using its sanitized `webkitRelativePath`
(e.g. `src/api/auth.ts`) as the multipart filename, so two `index.ts` files don't
collide and citations stay meaningful. Sanitize client-side: strip leading `/`,
drop any `..` segments. Backend already uses `Path.GetFileName` for filtering, so
there is no traversal risk; verify the stored path is safe before relying on it.

### 5. Feedback & errors
- Local skips shown as a single summary line, not per-file:
  `Skipped 1,240 files (dependencies, build output, non-source).`
- Backend `rejected` entries aggregated across all batches, shown as today.
- On a mid-run batch failure: stop and report `Uploaded 80 of 340 before an error`.
  No silent partial success.

## Testing

Frontend has no test runner today. Add **Vitest** (dev dependency + `test` script) and
unit-test the two pure helpers:

- `filterUploadFiles` — keeps source, drops each ignored segment and dotfiles, counts skips.
- `chunk` — exact multiples, remainder, empty, single.

Plus `npm run lint` and `npm run build`. Manual sanity check: upload a real project
folder and confirm only source/docs are indexed with relative-path names.

## Out of scope (YAGNI)

- Respecting `.gitignore` semantics (negations, nested ignores). The static dev-junk
  list covers the common case; revisit only if needed.
- Drag-and-drop folder support.
- Backend changes to the file cap or allowlist.
