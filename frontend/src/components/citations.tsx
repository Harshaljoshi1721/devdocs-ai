import type { Citation } from "@/lib/types";

/** Source chips (file path + line range) that grounded an answer. */
export function Sources({ citations }: { citations: Citation[] }) {
  if (citations.length === 0) return null;

  return (
    <div className="mt-3 flex flex-wrap items-center gap-1.5">
      <span className="eyebrow text-[0.6rem]">Sources</span>
      {citations.map((c, i) => (
        <span
          key={`${c.documentId}-${c.startLine}-${i}`}
          className="inline-flex items-center rounded border border-line bg-panel px-1.5 py-0.5 font-mono text-[0.7rem]"
          title={c.documentName}
        >
          <span className="text-ink">{c.path}</span>
          <span className="text-faint">
            :{c.startLine}-{c.endLine}
          </span>
        </span>
      ))}
    </div>
  );
}
