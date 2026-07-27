"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { ApiError, apiRequest } from "@/lib/api";
import { apiBaseUrl } from "@/lib/config";
import type { AuthResponse, User } from "@/lib/types";

type Status = "loading" | "authenticated" | "unauthenticated";

interface AuthContextValue {
  user: User | null;
  status: Status;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, name: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** Fetch with the bearer token attached; transparently refreshes once on 401. */
  authFetch: <T>(path: string, init?: Parameters<typeof apiRequest>[1]) => Promise<T>;
  /** Raw bearer-authenticated fetch (refreshes once on 401). For streams and multipart bodies. */
  authRaw: (path: string, init: RequestInit) => Promise<Response>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const accessToken = useRef<string | null>(null);
  const [user, setUser] = useState<User | null>(null);
  const [status, setStatus] = useState<Status>("loading");

  const applySession = useCallback((auth: AuthResponse) => {
    accessToken.current = auth.accessToken;
    setUser(auth.user);
    setStatus("authenticated");
  }, []);

  const clearSession = useCallback(() => {
    accessToken.current = null;
    setUser(null);
    setStatus("unauthenticated");
  }, []);

  // Restore the session on load using the refresh cookie.
  useEffect(() => {
    let cancelled = false;
    apiRequest<AuthResponse>("/api/v1/auth/refresh", { method: "POST" })
      .then((auth) => {
        if (!cancelled) applySession(auth);
      })
      .catch(() => {
        if (!cancelled) clearSession();
      });
    return () => {
      cancelled = true;
    };
  }, [applySession, clearSession]);

  const login = useCallback(
    async (email: string, password: string) => {
      const auth = await apiRequest<AuthResponse>("/api/v1/auth/login", {
        method: "POST",
        body: { email, password },
      });
      applySession(auth);
    },
    [applySession],
  );

  const register = useCallback(
    async (email: string, name: string, password: string) => {
      const auth = await apiRequest<AuthResponse>("/api/v1/auth/register", {
        method: "POST",
        body: { email, name, password },
      });
      applySession(auth);
    },
    [applySession],
  );

  const logout = useCallback(async () => {
    try {
      await apiRequest("/api/v1/auth/logout", { method: "POST" });
    } catch {
      // best-effort; clear local state regardless
    }
    clearSession();
  }, [clearSession]);

  const authFetch = useCallback(
    async <T,>(path: string, init: Parameters<typeof apiRequest>[1] = {}): Promise<T> => {
      try {
        return await apiRequest<T>(path, { ...init, token: accessToken.current });
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          // Access token likely expired — refresh once and retry.
          try {
            const auth = await apiRequest<AuthResponse>("/api/v1/auth/refresh", {
              method: "POST",
            });
            applySession(auth);
            return await apiRequest<T>(path, { ...init, token: auth.accessToken });
          } catch {
            clearSession();
          }
        }
        throw error;
      }
    },
    [applySession, clearSession],
  );

  const authRaw = useCallback(
    async (path: string, init: RequestInit): Promise<Response> => {
      const send = (token: string | null) => {
        const headers = new Headers(init.headers);
        if (token) headers.set("Authorization", `Bearer ${token}`);
        return fetch(`${apiBaseUrl}${path}`, { ...init, headers, credentials: "include" });
      };

      const response = await send(accessToken.current);
      if (response.status !== 401) return response;

      // Access token likely expired — refresh once and retry.
      try {
        const auth = await apiRequest<AuthResponse>("/api/v1/auth/refresh", { method: "POST" });
        applySession(auth);
        return await send(auth.accessToken);
      } catch {
        clearSession();
        return response;
      }
    },
    [applySession, clearSession],
  );

  return (
    <AuthContext.Provider value={{ user, status, login, register, logout, authFetch, authRaw }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
