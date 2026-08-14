"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { SearchResponse } from "@/lib/types";

const EXAMPLES = [
  "where is authentication handled?",
  "how are files uploaded?",
  "what validates user input?",
];

/** Turn a raw cosine score into a human label and a 0–100 bar width. */
function relevance(score: number): { label: string; pct: number } {
  const label = score >= 0.75 ? "Strong match" : score >= 0.6 ? "Good match" : "Partial match";
  const pct = Math.max(8, Math.min(100, Math.round(((score - 0.35) / 0.5) * 100)));
  return { label, pct };
}

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

  function runSearch(q: string) {
    const trimmed = q.trim();
    if (!trimmed) return;
    setQuery(trimmed);
    search.mutate(trimmed);
  }

  return (
    <div>
      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          runSearch(query);
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

      {/* Example queries before the first search. */}
      {search.isIdle && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <span className="text-xs text-faint">Try:</span>
          {EXAMPLES.map((ex) => (
            <button
              key={ex}
              type="button"
              onClick={() => runSearch(ex)}
              className="rounded-full border border-line px-3 py-1 text-xs text-muted transition-colors hover:border-line-strong hover:text-ink"
            >
              {ex}
            </button>
          ))}
        </div>
      )}

      {search.isError && (
        <div className="mt-4 flex items-center gap-3">
          <p className="text-sm text-danger">
            {search.error instanceof ApiError ? search.error.message : "Search failed."}
          </p>
          <Button variant="outline" size="sm" onClick={() => runSearch(query)}>
            Try again
          </Button>
        </div>
      )}

      {search.isSuccess && results.length === 0 && (
        <p className="mt-8 text-center text-sm text-muted">
          No matches for “{search.variables}”. Try different words, or upload and index more files first.
        </p>
      )}

      <div className="mt-5 space-y-3">
        {results.map((hit) => {
          const { label, pct } = relevance(hit.score);
          return (
            <div key={hit.chunkId} className="rounded-lg border border-line bg-panel/40 p-4">
              <div className="flex items-baseline justify-between gap-3">
                <span className="min-w-0 truncate font-mono text-xs text-ink">
                  {hit.path}
                  <span className="text-faint">
                    :{hit.startLine}-{hit.endLine}
                  </span>
                </span>
                <span
                  className="flex shrink-0 items-center gap-2"
                  title={`Relevance ${hit.score.toFixed(3)}`}
                >
                  <span className="hidden text-[0.7rem] text-muted sm:inline">{label}</span>
                  <span className="h-1.5 w-16 overflow-hidden rounded-full bg-line" aria-hidden="true">
                    <span className="block h-full rounded-full bg-accent" style={{ width: `${pct}%` }} />
                  </span>
                </span>
              </div>
              <pre className="mt-2 overflow-x-auto whitespace-pre-wrap font-mono text-xs text-muted">
                {hit.snippet}
              </pre>
            </div>
          );
        })}
      </div>
    </div>
  );
}
