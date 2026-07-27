"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { ProjectDocument, RejectedFile, UploadResult } from "@/lib/types";

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

export function DocumentsPanel({ projectId }: { projectId: string }) {
  const { authFetch, authRaw } = useAuth();
  const queryClient = useQueryClient();
  const fileInput = useRef<HTMLInputElement>(null);
  const [rejected, setRejected] = useState<RejectedFile[]>([]);
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
    mutationFn: async (files: FileList) => {
      const form = new FormData();
      Array.from(files).forEach((file) => form.append("files", file));
      const response = await authRaw(`/api/v1/projects/${projectId}/documents`, {
        method: "POST",
        body: form,
      });
      if (!response.ok) {
        throw new ApiError(response.status, "Upload failed. Check file types and sizes.");
      }
      return (await response.json()) as UploadResult;
    },
    onSuccess: (result) => {
      setRejected(result.rejected);
      setError(null);
      queryClient.invalidateQueries({ queryKey: ["documents", projectId] });
    },
    onError: (e) => setError(e instanceof ApiError ? e.message : "Upload failed."),
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
            if (e.target.files?.length) upload.mutate(e.target.files);
            e.target.value = "";
          }}
        />
        <Button
          variant="outline"
          size="sm"
          disabled={upload.isPending}
          onClick={() => fileInput.current?.click()}
        >
          {upload.isPending ? <Spinner /> : "Upload files"}
        </Button>
      </div>

      <p className="mt-2 text-xs text-muted">
        Upload source and docs to index them. Chat and search draw only from indexed content.
      </p>

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      {rejected.length > 0 && (
        <div className="mt-3 rounded-md border border-warn/20 bg-warn/5 p-3 text-xs">
          <p className="text-warn">Some files were skipped:</p>
          <ul className="mt-1 space-y-0.5 text-muted">
            {rejected.map((r) => (
              <li key={r.fileName}>
                <span className="font-mono text-ink">{r.fileName}</span> — {r.reason}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-4">
        {documents.isLoading ? (
          <div className="h-16 animate-pulse rounded-md border border-line bg-panel/40" />
        ) : docs.length === 0 ? (
          <p className="py-6 text-center text-sm text-faint">No documents yet.</p>
        ) : (
          <ul className="divide-y divide-line">
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
                    {doc.status}
                    {doc.status === "Failed" && doc.error ? ` — ${doc.error}` : ""} · {formatSize(doc.size)}
                  </p>
                </div>
                <button
                  onClick={() => remove.mutate(doc.id)}
                  className="text-xs text-faint transition-colors hover:text-danger"
                  aria-label={`Delete ${doc.name}`}
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
