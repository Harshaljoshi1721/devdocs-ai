"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { ProjectDocument, UploadResult } from "@/lib/types";
import {
  chunk,
  filterUploadFiles,
  sanitizeUploadPath,
  uploadPathOf,
  uploadSummary,
  type UploadMode,
} from "@/lib/upload";

// Must not exceed the server's UploadOptions.MaxFilesPerRequest; folders are sent in batches of this size.
const BATCH_SIZE = 50;

const STATUS_TONE: Record<string, "ok" | "warn" | "danger" | "muted"> = {
  Completed: "ok",
  Processing: "warn",
  Pending: "muted",
  Failed: "danger",
};

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const iconProps = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 2,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  "aria-hidden": true,
} as const;

function PlusIcon() {
  return (
    <svg width="16" height="16" {...iconProps}>
      <path d="M12 5v14M5 12h14" />
    </svg>
  );
}

function FileIcon() {
  return (
    <svg width="14" height="14" {...iconProps}>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
      <path d="M14 3v5h5" />
    </svg>
  );
}

function FolderIcon() {
  return (
    <svg width="14" height="14" {...iconProps}>
      <path d="M3 7a2 2 0 0 1 2-2h4l2 2h6a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
    </svg>
  );
}

/** A "+" trigger that opens a small menu to add files or a whole folder. */
function UploadMenu({
  busy,
  onAddFiles,
  onAddFolder,
}: {
  busy: boolean;
  onAddFiles: () => void;
  onAddFolder: () => void;
}) {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function onPointerDown(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  function choose(action: () => void) {
    setOpen(false);
    action();
  }

  const item =
    "flex w-full items-center gap-2.5 px-3 py-2 text-left text-sm text-ink hover:bg-line/40";

  return (
    <div ref={wrapRef} className="relative">
      <Button
        variant="outline"
        size="sm"
        className="gap-1.5 border-line-strong"
        disabled={busy}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Add documents"
        onClick={() => setOpen((v) => !v)}
      >
        {busy ? <Spinner /> : <PlusIcon />}
        Add
      </Button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 z-10 mt-1 w-44 overflow-hidden rounded-md border border-line bg-panel py-1 shadow-lg"
        >
          <button type="button" role="menuitem" className={item} onClick={() => choose(onAddFiles)}>
            <span className="text-muted">
              <FileIcon />
            </span>
            Add files
          </button>
          <button
            type="button"
            role="menuitem"
            className={item}
            onClick={() => choose(onAddFolder)}
          >
            <span className="text-muted">
              <FolderIcon />
            </span>
            Upload folder
          </button>
        </div>
      )}
    </div>
  );
}

export function DocumentsPanel({ projectId }: { projectId: string }) {
  const { authFetch, authRaw } = useAuth();
  const queryClient = useQueryClient();
  const fileInput = useRef<HTMLInputElement>(null);
  const folderInput = useRef<HTMLInputElement | null>(null);
  const [summary, setSummary] = useState<string | null>(null);
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const documents = useQuery({
    queryKey: ["documents", projectId],
    queryFn: () => authFetch<ProjectDocument[]>(`/api/v1/projects/${projectId}/documents`),
    // Poll while anything is still being indexed.
    refetchInterval: (query) =>
      (query.state.data ?? []).some((d) => d.status === "Pending" || d.status === "Processing")
        ? 1500
        : false,
  });

  const upload = useMutation({
    // Folder uploads (applyFilter) drop dependency/build junk before sending.
    mutationFn: async ({ files, applyFilter }: { files: File[]; applyFilter: boolean }) => {
      const mode: UploadMode = applyFilter ? "folder" : "files";
      const accepted = applyFilter ? filterUploadFiles(files).accepted : files;

      let added = 0;
      let failed = 0;
      let sent = 0;
      setProgress(accepted.length > 0 ? { done: 0, total: accepted.length } : null);

      // The API caps a request at BATCH_SIZE files, so send in sequential batches.
      for (const batch of chunk(accepted, BATCH_SIZE)) {
        const form = new FormData();
        // Send the folder-relative path as the filename so citations keep their structure.
        batch.forEach((file) => form.append("files", file, sanitizeUploadPath(uploadPathOf(file))));
        const response = await authRaw(`/api/v1/projects/${projectId}/documents`, {
          method: "POST",
          body: form,
        });
        if (!response.ok) {
          throw new ApiError(
            response.status,
            `Uploaded ${sent} of ${accepted.length} file(s) before the upload failed.`,
          );
        }
        const result = (await response.json()) as UploadResult;
        added += result.accepted.length;
        failed += result.rejected.length;
        sent += batch.length;
        setProgress({ done: sent, total: accepted.length });
      }

      return uploadSummary(mode, added, failed);
    },
    onSuccess: (message) => {
      setSummary(message);
      setError(null);
      setProgress(null);
      queryClient.invalidateQueries({ queryKey: ["documents", projectId] });
    },
    onError: (e) => {
      setProgress(null);
      setSummary(null);
      setError(e instanceof ApiError ? e.message : "Upload failed.");
    },
  });

  const remove = useMutation({
    mutationFn: (documentId: string) =>
      authFetch(`/api/v1/projects/${projectId}/documents/${documentId}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["documents", projectId] }),
  });

  const docs = documents.data ?? [];

  return (
    <div className="rounded-xl border border-line bg-panel/40 p-6">
      <div className="flex items-center justify-between gap-3">
        <span className="eyebrow">Documents</span>
        <input
          ref={fileInput}
          type="file"
          multiple
          className="hidden"
          onChange={(e) => {
            if (e.target.files?.length)
              upload.mutate({ files: Array.from(e.target.files), applyFilter: false });
            e.target.value = "";
          }}
        />
        <input
          // Callback ref sets the non-standard webkitdirectory attribute (no React type for it).
          ref={(el) => {
            folderInput.current = el;
            el?.setAttribute("webkitdirectory", "");
          }}
          type="file"
          multiple
          className="hidden"
          onChange={(e) => {
            if (e.target.files?.length)
              upload.mutate({ files: Array.from(e.target.files), applyFilter: true });
            e.target.value = "";
          }}
        />
        <UploadMenu
          busy={upload.isPending}
          onAddFiles={() => fileInput.current?.click()}
          onAddFolder={() => folderInput.current?.click()}
        />
      </div>

      <p className="mt-2 text-xs text-muted">
        Upload source and docs to index them. Chat and search draw only from indexed content.
      </p>

      {progress && (
        <p className="mt-3 text-sm text-muted">
          Uploading {progress.done} / {progress.total}…
        </p>
      )}

      {!progress && summary && <p className="mt-3 text-sm text-muted">{summary}</p>}

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      <div className="mt-4">
        {documents.isLoading ? (
          <div className="h-16 animate-pulse rounded-md border border-line bg-panel/40" />
        ) : docs.length === 0 ? (
          <p className="py-6 text-center text-sm text-faint">No documents yet.</p>
        ) : (
          <ul className="max-h-72 divide-y divide-line overflow-y-auto pr-1">
            {docs.map((doc) => (
              <li key={doc.id} className="flex items-center gap-3 py-2.5">
                <StatusDot
                  tone={STATUS_TONE[doc.status] ?? "muted"}
                  pulse={doc.status === "Processing"}
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-mono text-xs text-ink" title={doc.path}>
                    {doc.name}
                  </p>
                  <p className="text-[0.7rem] text-faint">
                    {doc.status} · {formatSize(doc.size)}
                  </p>
                </div>
                <button
                  onClick={() => {
                    if (confirm(`Remove "${doc.name}" from this project? This cannot be undone.`)) {
                      remove.mutate(doc.id);
                    }
                  }}
                  className="-mr-1 rounded px-2.5 py-1.5 text-xs text-faint transition-colors hover:bg-danger/10 hover:text-danger"
                  aria-label={`Remove ${doc.name}`}
                >
                  Remove
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
