"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { SearchResponse } from "@/lib/types";

export function SearchPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const [query, setQuery] = useState("");

  const search = useMutation({
    mutationFn: (q: string) =>
      authFetch<SearchResponse>(`/api/v1/projects/${projectId}/search`, {
        method: "POST",
        body: { query: q },
      }),
  });

  const results = search.data?.results ?? [];

  return (
    <div>
      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (query.trim()) search.mutate(query.trim());
        }}
      >
        <Input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search the codebase — e.g. 'where is auth handled?'"
          aria-label="Search query"
        />
        <Button type="submit" disabled={search.isPending || !query.trim()}>
          {search.isPending ? <Spinner /> : "Search"}
        </Button>
      </form>

      {search.isError && (
        <p className="mt-4 text-sm text-danger">
          {search.error instanceof ApiError ? search.error.message : "Search failed."}
        </p>
      )}

      {search.isSuccess && results.length === 0 && (
        <p className="mt-8 text-center text-sm text-faint">
          No matches. Upload and index relevant files first.
        </p>
      )}

      <div className="mt-5 space-y-3">
        {results.map((hit) => (
          <div key={hit.chunkId} className="rounded-lg border border-line bg-panel/40 p-4">
            <div className="flex items-baseline justify-between gap-3">
              <span className="font-mono text-xs text-ink">
                {hit.path}
                <span className="text-faint">
                  :{hit.startLine}-{hit.endLine}
                </span>
              </span>
              <span className="font-mono text-[0.7rem] text-accent">{hit.score.toFixed(3)}</span>
            </div>
            <pre className="mt-2 overflow-x-auto whitespace-pre-wrap font-mono text-xs text-muted">
              {hit.snippet}
            </pre>
          </div>
        ))}
      </div>
    </div>
  );
}
