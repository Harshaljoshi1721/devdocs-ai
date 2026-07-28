"use client";

import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth";
import type { UsageSummary } from "@/lib/types";

export function UsagePanel({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const usage = useQuery({
    queryKey: ["usage", projectId],
    queryFn: () => authFetch<UsageSummary>(`/api/v1/projects/${projectId}/usage`),
  });

  const data = usage.data;

  return (
    <div className="rounded-xl border border-line bg-panel/40 p-6">
      <span className="eyebrow">AI usage</span>
      {!data || data.totalRequests === 0 ? (
        <p className="mt-3 text-sm text-muted">No AI activity yet. Ask a question or run an agent.</p>
      ) : (
        <div className="mt-3">
          <p className="text-sm text-ink">
            <span className="font-mono">{data.totalRequests}</span> request
            {data.totalRequests === 1 ? "" : "s"} ·{" "}
            <span className="font-mono">{(data.totalTokensIn + data.totalTokensOut).toLocaleString()}</span>{" "}
            <span className="text-muted">tokens (est.)</span>
          </p>
          <ul className="mt-2 space-y-0.5">
            {data.byKind.map((k) => (
              <li key={k.kind} className="flex justify-between font-mono text-xs text-muted">
                <span className="text-ink">{k.kind}</span>
                <span>
                  {k.requests} · {(k.tokensIn + k.tokensOut).toLocaleString()} tok
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
