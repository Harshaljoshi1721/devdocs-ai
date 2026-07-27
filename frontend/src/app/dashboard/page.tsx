"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { Spinner, StatusDot } from "@/components/ui/misc";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { formatDate } from "@/lib/format";
import type { Project } from "@/lib/types";
import { useRequireAuth } from "@/lib/use-require-auth";

export default function DashboardPage() {
  const status = useRequireAuth();
  const { authFetch, user } = useAuth();
  const queryClient = useQueryClient();
  const [showForm, setShowForm] = useState(false);

  const projects = useQuery({
    queryKey: ["projects"],
    queryFn: () => authFetch<Project[]>("/api/v1/projects"),
    enabled: status === "authenticated",
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => authFetch(`/api/v1/projects/${id}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["projects"] }),
  });

  if (status !== "authenticated") {
    return (
      <div className="flex flex-1 items-center justify-center text-muted">
        <Spinner />
      </div>
    );
  }

  const list = projects.data ?? [];

  return (
    <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-12">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <span className="eyebrow">Workspace</span>
          <h1 className="mt-2 font-display text-4xl tracking-tight">Projects</h1>
          <p className="mt-1 text-sm text-muted">
            Signed in as <span className="text-ink">{user?.email}</span>
          </p>
        </div>
        <Button onClick={() => setShowForm((v) => !v)} variant={showForm ? "outline" : "primary"}>
          {showForm ? "Cancel" : "+ New project"}
        </Button>
      </div>

      {showForm && (
        <div className="reveal mt-6">
          <CreateProjectForm
            onCreated={() => {
              setShowForm(false);
              queryClient.invalidateQueries({ queryKey: ["projects"] });
            }}
          />
        </div>
      )}

      <div className="mt-8">
        {projects.isLoading ? (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-36 animate-pulse rounded-xl border border-line bg-panel/40" />
            ))}
          </div>
        ) : projects.isError ? (
          <p className="rounded-xl border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger">
            Could not load projects. {(projects.error as ApiError)?.message}
          </p>
        ) : list.length === 0 ? (
          <EmptyState onCreate={() => setShowForm(true)} />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {list.map((project) => (
              <article
                key={project.id}
                className="group flex flex-col justify-between rounded-xl border border-line bg-panel/40 p-5 transition-colors hover:border-line-strong"
              >
                <div>
                  <div className="flex items-center gap-2">
                    <StatusDot tone="muted" />
                    <span className="eyebrow">Not indexed</span>
                  </div>
                  <Link
                    href={`/projects/${project.id}`}
                    className="mt-3 block font-display text-xl tracking-tight hover:text-accent"
                  >
                    {project.name}
                  </Link>
                  <p className="mt-1 line-clamp-2 min-h-[2.5rem] text-sm text-muted">
                    {project.description || "No description"}
                  </p>
                </div>
                <div className="mt-4 flex items-center justify-between border-t border-line pt-3">
                  <span className="font-mono text-xs text-faint">{formatDate(project.createdAt)}</span>
                  <div className="flex items-center gap-1">
                    <Link
                      href={`/projects/${project.id}`}
                      className="rounded px-2 py-1 text-xs text-muted transition-colors hover:text-accent"
                    >
                      Open →
                    </Link>
                    <button
                      onClick={() => {
                        if (confirm(`Delete "${project.name}"? This cannot be undone.`)) {
                          deleteMutation.mutate(project.id);
                        }
                      }}
                      className="rounded px-2 py-1 text-xs text-faint transition-colors hover:text-danger"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </main>
  );
}

function EmptyState({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex flex-col items-center gap-4 rounded-2xl border border-dashed border-line bg-panel/20 px-6 py-20 text-center">
      <span className="grid h-12 w-12 place-items-center rounded-xl border border-line-strong bg-panel font-mono text-accent">
        {"{ }"}
      </span>
      <div>
        <p className="font-display text-xl">No projects yet</p>
        <p className="mt-1 text-sm text-muted">Create your first project to start indexing a codebase.</p>
      </div>
      <Button onClick={onCreate}>+ New project</Button>
    </div>
  );
}

function CreateProjectForm({ onCreated }: { onCreated: () => void }) {
  const { authFetch } = useAuth();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      authFetch<Project>("/api/v1/projects", {
        method: "POST",
        body: { name: name.trim(), description: description.trim() || null },
      }),
    onSuccess: onCreated,
    onError: (e) => setError(e instanceof ApiError ? e.message : "Could not create project."),
  });

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        setError(null);
        if (!name.trim()) {
          setError("Project name is required.");
          return;
        }
        mutation.mutate();
      }}
      className="rounded-xl border border-line bg-panel/40 p-6"
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Project name" htmlFor="name">
          <Input
            id="name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Payments service"
            maxLength={200}
            autoFocus
          />
        </Field>
        <Field label="Description" htmlFor="description" hint="Optional">
          <Input
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="What is this codebase?"
            maxLength={2000}
          />
        </Field>
      </div>
      {error && <p className="mt-3 text-sm text-danger">{error}</p>}
      <div className="mt-4 flex justify-end">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? <Spinner /> : "Create project"}
        </Button>
      </div>
    </form>
  );
}
