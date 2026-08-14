"use client";

import Link from "next/link";
import type { CSSProperties } from "react";
import { ApiStatus } from "@/components/ApiStatus";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/misc";
import { useRedirectIfAuthenticated } from "@/lib/use-require-auth";

const delay = (i: number) => ({ "--i": i }) as CSSProperties;

const features = [
  {
    no: "01",
    title: "Ask your codebase",
    body: "Natural-language questions answered from your indexed project — grounded, with citations to file and line.",
  },
  {
    no: "02",
    title: "Semantic search",
    body: "Find code by meaning, not keywords. Results ranked by relevance across the whole project.",
  },
  {
    no: "03",
    title: "Generate documentation",
    body: "Turn source into Markdown docs, API references, and architecture summaries on demand.",
  },
  {
    no: "04",
    title: "Agents with tools",
    body: "Code Explorer, Documentation Generator, Bug Analysis, and Architecture Analyst — agents that use tools.",
  },
];

const steps = [
  "Create a project",
  "Upload code or connect a repo",
  "Process & index",
  "Ask, and get cited answers",
];

export default function Home() {
  const status = useRedirectIfAuthenticated();

  // Avoid flashing the marketing page to a signed-in visitor mid-redirect.
  if (status !== "unauthenticated") {
    return (
      <div className="flex flex-1 items-center justify-center text-muted">
        <Spinner />
      </div>
    );
  }

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-24 px-6 py-20">
      {/* Hero */}
      <section className="flex flex-col gap-7">
        <div className="reveal flex items-center gap-3" style={delay(0)}>
          <span className="eyebrow">Codebase intelligence</span>
          <span className="h-px w-10 bg-line-strong" />
          <ApiStatus />
        </div>

        <h1
          className="reveal max-w-4xl font-display text-5xl leading-[1.05] tracking-tight sm:text-7xl"
          style={delay(1)}
        >
          Understand any codebase.
          <br />
          <span className="italic text-accent">Ask questions.</span> Generate knowledge.
        </h1>

        <p
          className="reveal max-w-2xl text-lg leading-relaxed text-muted"
          style={delay(2)}
        >
          An AI-powered developer knowledge platform. Connect or upload repositories and
          interact with your codebase in natural language — grounded answers, semantic
          search, documentation, and tool-using agents.
        </p>

        <div className="reveal flex flex-wrap gap-3" style={delay(3)}>
          <Link href="/register">
            <Button size="md" className="h-11 px-6">
              Get started →
            </Button>
          </Link>
          <Link href="/login">
            <Button variant="outline" size="md" className="h-11 px-6">
              Log in
            </Button>
          </Link>
        </div>

        {/* Show, don't tell: a representative grounded answer with citations. */}
        <div className="reveal mt-4 max-w-xl rounded-xl border border-line bg-panel/40 p-5" style={delay(4)}>
          <span className="eyebrow text-ink/60">You</span>
          <p className="mt-1 text-sm text-ink">How does authentication work in this codebase?</p>
          <span className="eyebrow mt-4 block text-accent/80">DevDocs AI</span>
          <p className="mt-1 text-sm leading-relaxed text-muted">
            Requests carry a JWT bearer token. <span className="font-mono text-ink">AuthService</span> issues a
            short-lived access token plus a rotating refresh token, and middleware verifies it on every request.
          </p>
          <div className="mt-3 flex flex-wrap items-center gap-1.5">
            <span className="eyebrow text-[0.6rem]">Sources</span>
            <span className="inline-flex items-center rounded border border-line bg-panel px-1.5 py-0.5 font-mono text-[0.7rem]">
              <span className="text-ink">auth/AuthService.cs</span>
              <span className="text-faint">:24-58</span>
            </span>
            <span className="inline-flex items-center rounded border border-line bg-panel px-1.5 py-0.5 font-mono text-[0.7rem]">
              <span className="text-ink">middleware/jwt.ts</span>
              <span className="text-faint">:12-30</span>
            </span>
          </div>
        </div>
      </section>

      {/* Features */}
      <section className="grid gap-px overflow-hidden rounded-xl border border-line bg-line sm:grid-cols-2">
        {features.map((f) => (
          <article
            key={f.no}
            className="group flex flex-col gap-3 bg-canvas p-7 transition-colors hover:bg-panel"
          >
            <div className="flex items-baseline justify-between">
              <h2 className="font-display text-xl">{f.title}</h2>
              <span className="font-mono text-xs text-faint group-hover:text-accent">{f.no}</span>
            </div>
            <p className="text-sm leading-relaxed text-muted">{f.body}</p>
          </article>
        ))}
      </section>

      {/* How it works */}
      <section className="flex flex-col gap-6">
        <span className="eyebrow">How it works</span>
        <ol className="grid gap-3 sm:grid-cols-4">
          {steps.map((step, i) => (
            <li key={step} className="rounded-xl border border-line bg-panel/40 p-5">
              <span className="font-mono text-xs text-accent">0{i + 1}</span>
              <span className="mt-2 block text-sm text-ink">{step}</span>
            </li>
          ))}
        </ol>
      </section>

      <footer className="mt-auto border-t border-line pt-6 font-mono text-xs text-faint">
        DevDocs AI · developer knowledge &amp; codebase intelligence
      </footer>
    </main>
  );
}
