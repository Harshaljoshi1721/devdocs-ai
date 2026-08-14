import { describe, expect, it } from "vitest";
import {
  chunk,
  filterUploadFiles,
  isIgnoredPath,
  sanitizeUploadPath,
  uploadPathOf,
  uploadSummary,
} from "./upload";

describe("chunk", () => {
  it("splits an exact multiple into equal groups", () => {
    expect(chunk([1, 2, 3, 4], 2)).toEqual([
      [1, 2],
      [3, 4],
    ]);
  });

  it("puts the remainder in a final smaller group", () => {
    expect(chunk([1, 2, 3, 4, 5], 2)).toEqual([[1, 2], [3, 4], [5]]);
  });

  it("returns a single group when size exceeds the list", () => {
    expect(chunk([1, 2], 50)).toEqual([[1, 2]]);
  });

  it("returns no groups for an empty list", () => {
    expect(chunk([], 50)).toEqual([]);
  });
});

describe("isIgnoredPath", () => {
  it("keeps ordinary source and doc files", () => {
    expect(isIgnoredPath("src/api/auth.ts")).toBe(false);
    expect(isIgnoredPath("README.md")).toBe(false);
  });

  it.each([
    "my-app/node_modules/react/index.js",
    "my-app/.git/config",
    "my-app/dist/bundle.js",
    "my-app/build/out.js",
    "web/.next/server/page.js",
    "web/out/index.html",
    "web/coverage/lcov.info",
    "api/bin/Debug/app.dll",
    "api/obj/project.assets.json",
    "repo/.vscode/settings.json",
    "repo/.idea/workspace.xml",
  ])("ignores dependency/build path %s", (path) => {
    expect(isIgnoredPath(path)).toBe(true);
  });

  it("ignores OS noise files", () => {
    expect(isIgnoredPath("app/.DS_Store")).toBe(true);
    expect(isIgnoredPath("app/Thumbs.db")).toBe(true);
  });

  it("keeps meaningful dotfiles and dotfolders (backend blocks true secrets)", () => {
    expect(isIgnoredPath(".github/workflows/ci.yml")).toBe(false);
    expect(isIgnoredPath("app/.gitignore")).toBe(false);
    expect(isIgnoredPath("app/.eslintrc.json")).toBe(false);
    expect(isIgnoredPath("app/.env")).toBe(false);
  });

  it("does not treat a normal segment containing a keyword as ignored", () => {
    expect(isIgnoredPath("src/distribution/index.ts")).toBe(false);
    expect(isIgnoredPath("src/building/plan.md")).toBe(false);
  });
});

describe("sanitizeUploadPath", () => {
  it("strips a leading slash", () => {
    expect(sanitizeUploadPath("/src/auth.ts")).toBe("src/auth.ts");
  });

  it("drops parent-directory traversal segments", () => {
    expect(sanitizeUploadPath("../../etc/passwd")).toBe("etc/passwd");
    expect(sanitizeUploadPath("src/../auth.ts")).toBe("src/auth.ts");
  });

  it("leaves a clean relative path unchanged", () => {
    expect(sanitizeUploadPath("src/api/auth.ts")).toBe("src/api/auth.ts");
  });
});

describe("uploadPathOf", () => {
  it("prefers webkitRelativePath when present", () => {
    expect(uploadPathOf({ name: "auth.ts", webkitRelativePath: "src/auth.ts" })).toBe(
      "src/auth.ts",
    );
  });

  it("falls back to the file name for plain file uploads", () => {
    expect(uploadPathOf({ name: "auth.ts", webkitRelativePath: "" })).toBe("auth.ts");
  });
});

describe("uploadSummary", () => {
  it("reports a folder upload as a single line, not per file", () => {
    expect(uploadSummary("folder", 142, 0)).toBe("Folder uploaded — 142 files added");
  });

  it("appends the failed count for a folder when some files fail", () => {
    expect(uploadSummary("folder", 140, 2)).toBe("Folder uploaded — 140 files added, 2 failed");
  });

  it("reports a folder with nothing added as failed", () => {
    expect(uploadSummary("folder", 0, 5)).toBe("Folder upload failed.");
  });

  it("reports added and failed counts for file uploads", () => {
    expect(uploadSummary("files", 3, 1)).toBe("3 files added, 1 failed");
  });

  it("singularises a single file", () => {
    expect(uploadSummary("files", 1, 0)).toBe("1 file added");
  });

  it("handles a file upload where everything failed", () => {
    expect(uploadSummary("files", 0, 2)).toBe("No files added, 2 failed");
  });
});

describe("filterUploadFiles", () => {
  it("keeps source files and counts ignored ones", () => {
    const files = [
      { name: "auth.ts", webkitRelativePath: "app/src/auth.ts" },
      { name: "index.js", webkitRelativePath: "app/node_modules/x/index.js" },
      { name: "README.md", webkitRelativePath: "app/README.md" },
      { name: "config", webkitRelativePath: "app/.git/config" },
    ];

    const result = filterUploadFiles(files);

    expect(result.accepted.map((f) => f.webkitRelativePath)).toEqual([
      "app/src/auth.ts",
      "app/README.md",
    ]);
    expect(result.skippedCount).toBe(2);
  });
});
