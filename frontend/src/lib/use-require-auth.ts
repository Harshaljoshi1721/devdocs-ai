"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuth } from "@/lib/auth";

/** Redirects to /login once we know the visitor is unauthenticated. */
export function useRequireAuth() {
  const { status } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "unauthenticated") router.replace("/login");
  }, [status, router]);

  return status;
}

/**
 * Redirects to /dashboard once we know the visitor is authenticated. For pre-login
 * pages (landing, login, register) that a signed-in user should not see.
 */
export function useRedirectIfAuthenticated() {
  const { status } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (status === "authenticated") router.replace("/dashboard");
  }, [status, router]);

  return status;
}
