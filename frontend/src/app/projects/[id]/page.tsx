"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { AgentsPanel } from "@/components/project-agents";
import { ChatPanel } from "@/components/project-chat";
import { DocumentationPanel } from "@/components/project-documentation";
import { DocumentsPanel } from "@/components/project-documents";
import { RepositoryPanel } from "@/components/project-repository";
import { SearchPanel } from "@/components/project-search";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { formatDate } from "@/lib/format";
import type { Project, ProjectDocument } from "@/lib/types";
import { useRequireAuth } from "@/lib/use-require-auth";

const TABS = [
  { key: "overview", label: "Overview" },
  { key: "chat", label: "Chat" },
  { key: "search", label: "Search" },
  { key: "agents", label: "Agents" },
  { key: "docs", label: "Documentation" },
] as const;

type TabKey = (typeof TABS)[number]["key"];

export default function ProjectPage() {
  const authStatus = useRequireAuth();
  const { authFetch } = useAuth();
  const { id } = useParams<{ id: string }>();
  const [tab, setTab] = useState<TabKey>("overview");

  const project = useQuery({
    queryKey: ["project", id],
    queryFn: () => authFetch<Project>(`/api/v1/projects/${id}`),
    enabled: authStatus === "authenticated",
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 1,
  });

  if (authStatus !== "authenticated") {
    return (
      <div className="flex flex-1 items-center justify-center text-muted">
        <Spinner />
      </div>
    );
  }

  if (project.isError && project.error instanceof ApiError && project.error.status === 404) {
    return <NotFound />;
  }

  return (
    <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">
      <Link href="/dashboard" className="font-mono text-xs text-muted transition-colors hover:text-accent">
        ← All projects
      </Link>

      {project.isLoading || !project.data ? (
        <div className="mt-6 h-40 animate-pulse rounded-xl border border-line bg-panel/40" />
      ) : (
        <>
          <IndexStatus projectId={project.data.id} />
          <h1 className="mt-3 font-display text-4xl tracking-tight">{project.data.name}</h1>
          <p className="mt-2 max-w-2xl text-muted">{project.data.description || "No description"}</p>

          {/* Tabs */}
          <div className="mt-8 flex flex-wrap gap-1 border-b border-line">
            {TABS.map((t) => (
              <button
                key={t.key}
                onClick={() => setTab(t.key)}
                className={`relative -mb-px border-b-2 px-4 py-2.5 text-sm transition-colors ${
                  tab === t.key
                    ? "border-accent text-ink"
                    : "border-transparent text-muted hover:text-ink"
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>

          <div className="mt-6">
            {tab === "overview" ? (
              <Overview project={project.data} />
            ) : tab === "chat" ? (
              <ChatPanel projectId={project.data.id} />
            ) : tab === "search" ? (
              <SearchPanel projectId={project.data.id} />
            ) : tab === "agents" ? (
              <AgentsPanel projectId={project.data.id} />
            ) : (
              <DocumentationPanel projectId={project.data.id} />
            )}
          </div>
        </>
      )}
    </main>
  );
}

/** Header badge reflecting the project's live indexing state (shares the documents query cache). */
function IndexStatus({ projectId }: { projectId: string }) {
  const { authFetch } = useAuth();
  const documents = useQuery({
    queryKey: ["documents", projectId],
    queryFn: () => authFetch<ProjectDocument[]>(`/api/v1/projects/${projectId}/documents`),
    refetchInterval: (query) =>
      (query.state.data ?? []).some((d) => d.status === "Pending" || d.status === "Processing")
        ? 1500
        : false,
  });

  let tone: "ok" | "warn" | "danger" | "muted" = "muted";
  let label = "Not indexed";
  let pulse = false;

  if (documents.isLoading) {
    label = "Checking status…";
  } else {
    const docs = documents.data ?? [];
    const indexed = docs.filter((d) => d.status === "Completed").length;
    if (docs.some((d) => d.status === "Pending" || d.status === "Processing")) {
      tone = "warn";
      label = "Indexing…";
      pulse = true;
    } else if (indexed > 0) {
      tone = "ok";
      label = `Indexed · ${indexed} ${indexed === 1 ? "document" : "documents"}`;
    } else if (docs.length > 0) {
      tone = "danger";
      label = "Indexing failed";
    }
  }

  return (
    <div className="mt-4 flex items-center gap-3">
      <StatusDot tone={tone} pulse={pulse} />
      <span className="eyebrow">{label}</span>
    </div>
  );
}

function Overview({ project }: { project: Project }) {
  const { authFetch } = useAuth();
  const queryClient = useQueryClient();
  const router = useRouter();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description ?? "");
  const [error, setError] = useState<string | null>(null);

  const updateMutation = useMutation({
    mutationFn: () =>
      authFetch<Project>(`/api/v1/projects/${project.id}`, {
        method: "PUT",
        body: { name: name.trim(), description: description.trim() || null },
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["project", project.id], updated);
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      setEditing(false);
    },
    onError: (e) => setError(e instanceof ApiError ? e.message : "Could not save changes."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => authFetch(`/api/v1/projects/${project.id}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] });
      router.push("/dashboard");
    },
  });

  const meta = [
    { label: "Project ID", value: project.id, mono: true },
    { label: "Created", value: formatDate(project.createdAt) },
    { label: "Updated", value: formatDate(project.updatedAt) },
  ];

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <section className="lg:col-span-2">
        <div className="rounded-xl border border-line bg-panel/40 p-6">
          <div className="flex items-center justify-between">
            <span className="eyebrow">Details</span>
            {!editing && (
              <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                Edit
              </Button>
            )}
          </div>

          {editing ? (
            <form
              className="mt-5 flex flex-col gap-4"
              onSubmit={(e) => {
                e.preventDefault();
                setError(null);
                if (!name.trim()) {
                  setError("Project name is required.");
                  return;
                }
                updateMutation.mutate();
              }}
            >
              <Field label="Project name" htmlFor="name">
                <Input id="name" value={name} onChange={(e) => setName(e.target.value)} maxLength={200} />
              </Field>
              <Field label="Description" htmlFor="description" hint="Optional">
                <Textarea
                  id="description"
                  rows={3}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  maxLength={2000}
                />
              </Field>
              {error && <p className="text-sm text-danger">{error}</p>}
              <div className="flex gap-2">
                <Button type="submit" size="sm" disabled={updateMutation.isPending}>
                  {updateMutation.isPending ? <Spinner /> : "Save"}
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => {
                    setEditing(false);
                    setName(project.name);
                    setDescription(project.description ?? "");
                    setError(null);
                  }}
                >
                  Cancel
                </Button>
              </div>
            </form>
          ) : (
            <dl className="mt-5 grid gap-4 sm:grid-cols-2">
              {meta.map((m) => (
                <div key={m.label}>
                  <dt className="eyebrow">{m.label}</dt>
                  <dd className={`mt-1 text-sm text-ink ${m.mono ? "break-all font-mono text-xs" : ""}`}>
                    {m.value}
                  </dd>
                </div>
              ))}
            </dl>
          )}
        </div>
      </section>

      <aside className="flex flex-col gap-4">
        <RepositoryPanel projectId={project.id} />
        <DocumentsPanel projectId={project.id} />
        <div className="rounded-xl border border-danger/20 bg-danger/5 p-6">
          <span className="eyebrow text-danger/80">Danger zone</span>
          <p className="mt-2 text-sm text-muted">Permanently delete this project.</p>
          <Button
            variant="danger"
            size="sm"
            className="mt-3"
            disabled={deleteMutation.isPending}
            onClick={() => {
              if (confirm(`Delete "${project.name}"? This cannot be undone.`)) {
                deleteMutation.mutate();
              }
            }}
          >
            {deleteMutation.isPending ? <Spinner /> : "Delete project"}
          </Button>
        </div>
      </aside>
    </div>
  );
}

function NotFound() {
  return (
    <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col items-center justify-center px-6 py-24 text-center">
      <span className="font-mono text-sm text-accent">404</span>
      <h1 className="mt-2 font-display text-3xl">Project not found</h1>
      <p className="mt-2 text-muted">It may have been deleted, or it belongs to another account.</p>
      <Link href="/dashboard" className="mt-6">
        <Button variant="outline">← Back to projects</Button>
      </Link>
    </main>
  );
}
