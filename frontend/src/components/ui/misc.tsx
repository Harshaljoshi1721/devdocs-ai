export function Spinner({ className = "" }: { className?: string }) {
  return (
    <span
      className={`inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent ${className}`}
      role="status"
      aria-label="Loading"
    />
  );
}

type Tone = "ok" | "muted" | "warn" | "danger" | "accent";

const toneColor: Record<Tone, string> = {
  ok: "bg-ok",
  muted: "bg-faint",
  warn: "bg-warn",
  danger: "bg-danger",
  accent: "bg-accent",
};

export function StatusDot({ tone = "muted", pulse = false }: { tone?: Tone; pulse?: boolean }) {
  return (
    <span className="relative inline-flex h-2 w-2">
      {pulse && (
        <span
          className={`absolute inline-flex h-full w-full animate-ping rounded-full opacity-60 ${toneColor[tone]}`}
        />
      )}
      <span className={`relative inline-flex h-2 w-2 rounded-full ${toneColor[tone]}`} />
    </span>
  );
}
