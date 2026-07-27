export function AuthShell({
  eyebrow,
  title,
  subtitle,
  children,
  footer,
}: {
  eyebrow: string;
  title: React.ReactNode;
  subtitle: string;
  children: React.ReactNode;
  footer: React.ReactNode;
}) {
  return (
    <main className="flex flex-1 items-center justify-center px-6 py-16">
      <div className="reveal w-full max-w-md">
        <div className="rounded-2xl border border-line bg-panel/40 p-8 shadow-2xl shadow-black/40">
          <span className="eyebrow">{eyebrow}</span>
          <h1 className="mt-3 font-display text-3xl tracking-tight">{title}</h1>
          <p className="mt-2 text-sm text-muted">{subtitle}</p>
          <div className="mt-7">{children}</div>
        </div>
        <p className="mt-5 text-center text-sm text-muted">{footer}</p>
      </div>
    </main>
  );
}
