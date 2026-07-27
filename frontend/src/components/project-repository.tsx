"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { RepositoryConnection } from "@/lib/types";

const STATUS_TONE: Record<string, "ok" | "warn" | "danger" | "muted"> = {
  Completed: "ok",
  Processing: "warn",
  Pending: "muted",
  Failed: "danger",
};

export function RepositoryPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const queryClient = useQueryClient();
  const [url, setUrl] = useState("");
  const [error, setError] = useState<string | null>(null);

  const connection = useQuery({
    queryKey: ["repository", projectId],
    queryFn: async () => {
      try {
        return await authFetch<RepositoryConnection>(`/api/v1/projects/${projectId}/repository`);
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return null;
        throw e;
      }
    },
    refetchInterval: (query) =>
      query.state.data?.status === "Pending" || query.state.data?.status === "Processing" ? 1500 : false,
  });

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["repository", projectId] });
    queryClient.invalidateQueries({ queryKey: ["documents", projectId] });
  };

  const connect = useMutation({
    mutationFn: () =>
      authFetch<RepositoryConnection>(`/api/v1/projects/${projectId}/repository`, {
        method: "POST",
        body: { url: url.trim(), ref: null },
      }),
    onSuccess: () => {
      setUrl("");
      setError(null);
      invalidateAll();
    },
    onError: (e) => setError(e instanceof ApiError ? e.message : "Could not connect the repository."),
  });

  const resync = useMutation({
    mutationFn: () => authFetch(`/api/v1/projects/${projectId}/repository/resync`, { method: "POST" }),
    onSuccess: invalidateAll,
  });

  const disconnect = useMutation({
    mutationFn: () => authFetch(`/api/v1/projects/${projectId}/repository`, { method: "DELETE" }),
    onSuccess: invalidateAll,
  });

  const conn = connection.data;
  const busy = conn?.status === "Pending" || conn?.status === "Processing";

  return (
    <div className="rounded-xl border border-line bg-panel/40 p-6">
      <span className="eyebrow">Repository</span>

      {!conn ? (
        <form
          className="mt-3 flex flex-col gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (url.trim()) connect.mutate();
          }}
        >
          <p className="text-xs text-muted">Connect a public GitHub repository to index its files.</p>
          <div className="flex gap-2">
            <Input
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://github.com/owner/repo"
              aria-label="GitHub repository URL"
            />
            <Button type="submit" size="sm" disabled={connect.isPending || !url.trim()}>
              {connect.isPending ? <Spinner /> : "Connect"}
            </Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </form>
      ) : (
        <div className="mt-3">
          <div className="flex flex-wrap items-center gap-2">
            <StatusDot tone={STATUS_TONE[conn.status] ?? "muted"} pulse={busy} />
            <a
              href={conn.url}
              target="_blank"
              rel="noopener noreferrer"
              className="font-mono text-xs text-ink hover:text-accent"
            >
              {conn.owner}/{conn.repo}
            </a>
            <span className="font-mono text-[0.7rem] text-faint">
              {conn.status}
              {conn.commitSha ? ` · ${conn.commitSha.slice(0, 7)}` : ""}
              {conn.status === "Completed" ? ` · ${conn.fileCount} files` : ""}
            </span>
          </div>
          {conn.status === "Failed" && conn.error && (
            <p className="mt-2 text-xs text-danger">{conn.error}</p>
          )}
          <div className="mt-3 flex gap-2">
            <Button variant="outline" size="sm" disabled={busy || resync.isPending} onClick={() => resync.mutate()}>
              {resync.isPending ? <Spinner /> : "Re-sync"}
            </Button>
            <Button variant="ghost" size="sm" disabled={disconnect.isPending} onClick={() => disconnect.mutate()}>
              Disconnect
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
