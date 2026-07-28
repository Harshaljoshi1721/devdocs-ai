"use client";

import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { AgentTrace } from "@/components/agent-trace";
import { Markdown } from "@/components/Markdown";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { Spinner } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import type { AgentRunResponse } from "@/lib/types";

export function DocumentationPanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const [topic, setTopic] = useState("");

  const run = useMutation({
    mutationFn: () =>
      authFetch<AgentRunResponse>(`/api/v1/projects/${projectId}/agents/DocumentationGenerator/run`, {
        method: "POST",
        body: { input: topic.trim() },
      }),
  });

  return (
    <div>
      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (topic.trim()) run.mutate();
        }}
      >
        <Input
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
          placeholder="What should I document? e.g. 'the authentication flow'"
          aria-label="Documentation topic"
        />
        <Button type="submit" disabled={run.isPending || !topic.trim()}>
          {run.isPending ? <Spinner /> : "Generate"}
        </Button>
      </form>

      {run.isPending && (
        <p className="mt-4 text-sm text-muted">Reading the source and writing docs…</p>
      )}
      {run.isError && (
        <p className="mt-4 text-sm text-danger">
          {run.error instanceof ApiError ? run.error.message : "Generation failed."}
        </p>
      )}
      {run.data && (
        <div className="mt-5 rounded-xl border border-line bg-panel/30 p-5">
          {run.data.status === "Failed" ? (
            <p className="text-sm text-danger">{run.data.error ?? "Could not generate documentation."}</p>
          ) : (
            <Markdown content={run.data.output ?? ""} />
          )}
          <AgentTrace trace={run.data.trace} />
        </div>
      )}
    </div>
  );
}
