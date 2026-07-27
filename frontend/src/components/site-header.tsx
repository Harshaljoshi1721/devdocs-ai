"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";

export function SiteHeader() {
  const { status, user, logout } = useAuth();
  const router = useRouter();

  async function handleLogout() {
    await logout();
    router.push("/");
  }

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-canvas/80 backdrop-blur-md">
      <div className="mx-auto flex h-14 w-full max-w-6xl items-center justify-between px-6">
        <Link
          href={status === "authenticated" ? "/dashboard" : "/"}
          className="group flex items-center gap-2.5"
        >
          <span className="grid h-7 w-7 place-items-center rounded-md border border-line-strong bg-panel font-mono text-sm text-accent">
            {"</>"}
          </span>
          <span className="font-display text-lg tracking-tight">
            DevDocs <span className="italic text-accent">AI</span>
          </span>
        </Link>

        <nav className="flex items-center gap-2">
          {status === "loading" && (
            <span className="h-4 w-20 animate-pulse rounded bg-panel" />
          )}

          {status === "unauthenticated" && (
            <>
              <Button variant="ghost" size="sm" onClick={() => router.push("/login")}>
                Log in
              </Button>
              <Button size="sm" onClick={() => router.push("/register")}>
                Sign up
              </Button>
            </>
          )}

          {status === "authenticated" && (
            <>
              <Link
                href="/dashboard"
                className="rounded-md px-3 py-1.5 text-sm text-muted transition-colors hover:text-ink"
              >
                Dashboard
              </Link>
              <span className="hidden items-center gap-2 border-l border-line pl-3 sm:flex">
                <span className="grid h-6 w-6 place-items-center rounded-full bg-accent text-xs font-semibold text-accent-ink">
                  {user?.name?.charAt(0).toUpperCase() ?? "?"}
                </span>
                <span className="max-w-[10rem] truncate text-sm text-muted">{user?.name}</span>
              </span>
              <Button variant="ghost" size="sm" onClick={handleLogout}>
                Log out
              </Button>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}
