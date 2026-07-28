"use client";

import { useState } from "react";
import type { TraceItem } from "@/lib/types";

export function AgentTrace({ trace }: { trace: TraceItem[] }) {
  const [open, setOpen] = useState(false);
  if (trace.length === 0) return null;

  return (
    <div className="mt-4 rounded-lg border border-line bg-panel/40">
      <button
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between px-4 py-2.5 text-left"
      >
        <span className="eyebrow">Tool trace · {trace.length} call{trace.length === 1 ? "" : "s"}</span>
        <span className="font-mono text-xs text-faint">{open ? "hide" : "show"}</span>
      </button>
      {open && (
        <ol className="space-y-3 border-t border-line px-4 py-3">
          {trace.map((t) => (
            <li key={t.sequence} className="text-xs">
              <div className="flex items-center gap-2">
                <span className="font-mono text-accent">{t.toolName}</span>
                <span
                  className={`font-mono text-[0.65rem] ${t.status === "Ok" ? "text-ok" : "text-danger"}`}
                >
                  {t.status}
                </span>
                <span className="font-mono text-[0.65rem] text-faint">{t.durationMs}ms</span>
              </div>
              <pre className="mt-1 overflow-x-auto whitespace-pre-wrap font-mono text-[0.7rem] text-muted">
                input: {t.input}
              </pre>
              <pre className="mt-1 max-h-40 overflow-auto whitespace-pre-wrap font-mono text-[0.7rem] text-muted">
                {t.output}
              </pre>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
