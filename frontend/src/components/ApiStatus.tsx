"use client";

import { useEffect, useState } from "react";
import { apiBaseUrl } from "@/lib/config";
import { StatusDot } from "@/components/ui/misc";

type State = "checking" | "healthy" | "unreachable";

export function ApiStatus() {
  const [state, setState] = useState<State>("checking");

  useEffect(() => {
    let cancelled = false;
    fetch(`${apiBaseUrl}/health`, { cache: "no-store" })
      .then((r) => r.json())
      .then((body) => {
        if (!cancelled) setState(body?.status === "Healthy" ? "healthy" : "unreachable");
      })
      .catch(() => {
        if (!cancelled) setState("unreachable");
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const config = {
    checking: { label: "Checking API", tone: "muted" as const, pulse: true },
    healthy: { label: "API healthy", tone: "ok" as const, pulse: true },
    unreachable: { label: "API unreachable", tone: "danger" as const, pulse: false },
  }[state];

  return (
    <div className="flex w-fit items-center gap-2 rounded-full border border-line bg-panel/60 px-3 py-1">
      <StatusDot tone={config.tone} pulse={config.pulse} />
      <span className="font-mono text-xs text-muted">{config.label}</span>
    </div>
  );
}
