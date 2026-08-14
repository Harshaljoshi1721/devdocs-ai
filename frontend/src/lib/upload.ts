/**
 * Client-side helpers for uploading whole folders. The browser's directory picker
 * hands us every file recursively — including dependency and build output — so we
 * filter locally before sending, and split the survivors into batches the API accepts.
 */

/** The minimal shape we need from a picked file (real `File` objects satisfy it). */
export interface FileLike {
  name: string;
  /** Set by `<input webkitdirectory>`; empty for a plain multi-file selection. */
  webkitRelativePath?: string;
}

/** Directory names whose contents are never source we want to index. */
const IGNORED_SEGMENTS = new Set([
  "node_modules",
  ".git",
  "dist",
  "build",
  ".next",
  "out",
  "coverage",
  "bin",
  "obj",
  ".vscode",
  ".idea",
]);

/** OS noise files that are never worth uploading. */
const IGNORED_FILES = new Set([".DS_Store", "Thumbs.db"]);

/** The path we treat a picked file as having: its folder-relative path, else its name. */
export function uploadPathOf(file: FileLike): string {
  return file.webkitRelativePath || file.name;
}

/**
 * True if the path lives in a known dependency/build directory or is an OS noise file.
 * Matches whole segments, so `src/distribution/x.ts` is kept while `app/dist/x.js` is not.
 * Meaningful dotfiles (`.gitignore`, `.github/…`) are kept; the backend blocks true secrets.
 */
export function isIgnoredPath(relativePath: string): boolean {
  const segments = relativePath.split("/").filter((s) => s.length > 0);
  if (segments.some((segment) => IGNORED_SEGMENTS.has(segment))) return true;
  const base = segments.at(-1);
  return base !== undefined && IGNORED_FILES.has(base);
}

/** Normalise a relative path: drop a leading slash and any `.`/`..` segments. */
export function sanitizeUploadPath(relativePath: string): string {
  return relativePath
    .split("/")
    .filter((segment) => segment.length > 0 && segment !== "." && segment !== "..")
    .join("/");
}

/** Split `items` into consecutive groups of at most `size`. */
export function chunk<T>(items: T[], size: number): T[][] {
  const groups: T[][] = [];
  for (let i = 0; i < items.length; i += size) {
    groups.push(items.slice(i, i + size));
  }
  return groups;
}

export type UploadMode = "files" | "folder";

/**
 * A one-line result message. A folder reports as a single outcome ("uploaded or not");
 * a file selection reports how many were added and how many failed — never per-file reasons.
 */
export function uploadSummary(mode: UploadMode, added: number, failed: number): string {
  const files = (n: number) => `${n} file${n === 1 ? "" : "s"}`;
  const failedSuffix = failed > 0 ? `, ${failed} failed` : "";

  if (mode === "folder") {
    return added === 0 ? "Folder upload failed." : `Folder uploaded — ${files(added)} added${failedSuffix}`;
  }
  return added === 0 ? `No files added${failedSuffix}` : `${files(added)} added${failedSuffix}`;
}

/** Keep only files worth indexing; report how many were skipped locally. */
export function filterUploadFiles<T extends FileLike>(
  files: T[],
): { accepted: T[]; skippedCount: number } {
  const accepted = files.filter((file) => !isIgnoredPath(uploadPathOf(file)));
  return { accepted, skippedCount: files.length - accepted.length };
}
