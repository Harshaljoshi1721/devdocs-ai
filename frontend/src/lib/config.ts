/**
 * Central place for frontend configuration. Only NEXT_PUBLIC_* values are
 * exposed to the browser — never secrets or provider API keys.
 */
export const apiBaseUrl =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
