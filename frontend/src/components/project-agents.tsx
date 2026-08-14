"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { AgentTrace } from "@/components/agent-trace";
import { Markdown } from "@/components/Markdown";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { AgentRunResponse } from "@/lib/types";

const AGENTS = [
  { type: "CodeExplorer", label: "Code Explorer", hint: "Ask where something is implemented." },
  { type: "BugAnalysis", label: "Bug Analysis", hint: "Paste an error + stack trace." },
  { type: "ArchitectureAnalyst", label: "Architecture Analyst", hint: "Summarize the architecture." },
] as const;

export function AgentsPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const [agent, setAgent] = useState<(typeof AGENTS)[number]["type"]>("CodeExplorer");
  const [input, setInput] = useState("");
  const [error, setError] = useState("");
  const [stack, setStack] = useState("");

  const run = useMutation({
    mutationFn: (body: string) =>
      authFetch<AgentRunResponse>(`/api/v1/projects/${projectId}/agents/${agent}/run`, {
        method: "POST",
        body: { input: body },
      }),
  });

  const isBug = agent === "BugAnalysis";
  const composed = isBug
    ? [error && `Error: ${error}`, stack && `Stack trace:\n${stack}`, input && `Notes: ${input}`]
        .filter(Boolean)
        .join("\n\n")
    : input.trim();

  return (
    <div>
      <div role="tablist" aria-label="Agents" className="flex flex-wrap gap-1 border-b border-line pb-3">
        {AGENTS.map((a) => (
          <button
            key={a.type}
            role="tab"
            aria-selected={agent === a.type}
            onClick={() => {
              setAgent(a.type);
              run.reset();
            }}
            className={`rounded-md px-3 py-2 text-sm transition-colors ${
              agent === a.type ? "bg-panel text-ink" : "text-muted hover:text-ink"
            }`}
          >
            {a.label}
          </button>
        ))}
      </div>

      <form
        className="mt-4 flex flex-col gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (composed) run.mutate(composed);
        }}
      >
        <p className="text-xs text-muted">{AGENTS.find((a) => a.type === agent)!.hint}</p>
        {isBug && (
          <>
            <input
              value={error}
              onChange={(e) => setError(e.target.value)}
              placeholder="Error message"
              className="h-10 w-full rounded-md border border-line bg-panel px-3 text-sm text-ink placeholder:text-faint focus:border-accent focus:outline-none"
            />
            <textarea
              value={stack}
              onChange={(e) => setStack(e.target.value)}
              rows={3}
              placeholder="Stack trace (optional)"
              className="w-full rounded-md border border-line bg-panel px-3 py-2 font-mono text-xs text-ink placeholder:text-faint focus:border-accent focus:outline-none"
            />
          </>
        )}
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          rows={isBug ? 2 : 3}
          placeholder={isBug ? "Extra context (optional)" : "Ask the agent…"}
          className="w-full rounded-md border border-line bg-panel px-3 py-2 text-sm text-ink placeholder:text-faint focus:border-accent focus:outline-none"
        />
        <div>
          <Button type="submit" disabled={run.isPending || !composed}>
            {run.isPending ? <Spinner /> : "Run agent"}
          </Button>
        </div>
      </form>

      {run.isPending && (
        <p className="mt-4 text-sm text-muted">The agent is working — this can take a moment on a local model…</p>
      )}
      {run.isError && (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <p className="text-sm text-danger">
            {run.error instanceof ApiError ? run.error.message : "The agent run failed."}
          </p>
          <Button variant="outline" size="sm" onClick={() => composed && run.mutate(composed)}>
            Run again
          </Button>
        </div>
      )}
      {run.data && (
        <div className="mt-5">
          {run.data.status === "Failed" ? (
            <div className="rounded-lg border border-danger/20 bg-danger/5 p-4">
              <p className="text-sm text-ink">The agent couldn&rsquo;t finish this one.</p>
              <p className="mt-1 text-xs text-muted">
                Local models sometimes struggle with multi-step tool use. Try a more specific question, or run it
                again.
              </p>
              {run.data.error && (
                <p className="mt-2 font-mono text-xs text-faint">{run.data.error}</p>
              )}
              <Button
                variant="outline"
                size="sm"
                className="mt-3"
                onClick={() => composed && run.mutate(composed)}
              >
                Run again
              </Button>
            </div>
          ) : (
            <Markdown content={run.data.output ?? ""} />
          )}
          <AgentTrace trace={run.data.trace} />
        </div>
      )}
    </div>
  );
}
