import { Fragment, type ReactNode } from "react";

/**
 * Minimal, dependency-free Markdown renderer tuned for chat answers: fenced code
 * blocks, inline code, bold, headings, and bullet/numbered lists. Content is
 * rendered as React nodes (never raw HTML), so model output can't inject markup.
 */
export function Markdown({ content }: { content: string }) {
  return <div className="space-y-3 text-sm leading-relaxed text-ink/90">{renderBlocks(content)}</div>;
}

function renderBlocks(source: string): ReactNode[] {
  const lines = source.replace(/\r\n/g, "\n").split("\n");
  const blocks: ReactNode[] = [];
  let i = 0;
  let key = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Fenced code block.
    if (line.trimStart().startsWith("```")) {
      const fence = line.trimStart().slice(3).trim();
      const code: string[] = [];
      i++;
      while (i < lines.length && !lines[i].trimStart().startsWith("```")) {
        code.push(lines[i]);
        i++;
      }
      i++; // skip closing fence
      blocks.push(
        <pre
          key={key++}
          className="overflow-x-auto rounded-lg border border-line bg-canvas/80 p-3 font-mono text-xs text-ink/90"
        >
          {fence && <div className="mb-2 text-[0.65rem] uppercase tracking-wider text-faint">{fence}</div>}
          <code>{code.join("\n")}</code>
        </pre>,
      );
      continue;
    }

    // Heading.
    const heading = /^(#{1,4})\s+(.*)$/.exec(line);
    if (heading) {
      const level = heading[1].length;
      blocks.push(
        <p key={key++} className={`font-display ${level <= 2 ? "text-lg" : "text-base"} text-ink`}>
          {renderInline(heading[2])}
        </p>,
      );
      i++;
      continue;
    }

    // List (consecutive bullet or numbered items).
    if (isListItem(line)) {
      const items: string[] = [];
      const ordered = /^\s*\d+\.\s+/.test(line);
      while (i < lines.length && isListItem(lines[i])) {
        items.push(lines[i].replace(/^\s*(?:[-*]|\d+\.)\s+/, ""));
        i++;
      }
      const ListTag = ordered ? "ol" : "ul";
      blocks.push(
        <ListTag
          key={key++}
          className={`ml-5 space-y-1 ${ordered ? "list-decimal" : "list-disc"} marker:text-faint`}
        >
          {items.map((item, idx) => (
            <li key={idx}>{renderInline(item)}</li>
          ))}
        </ListTag>,
      );
      continue;
    }

    // Blank line.
    if (line.trim() === "") {
      i++;
      continue;
    }

    // Paragraph (gather until blank line or a block starter).
    const para: string[] = [];
    while (i < lines.length && lines[i].trim() !== "" && !isBlockStart(lines[i])) {
      para.push(lines[i]);
      i++;
    }
    blocks.push(
      <p key={key++} className="whitespace-pre-wrap">
        {renderInline(para.join("\n"))}
      </p>,
    );
  }

  return blocks;
}

function isListItem(line: string): boolean {
  return /^\s*(?:[-*]|\d+\.)\s+/.test(line);
}

function isBlockStart(line: string): boolean {
  return line.trimStart().startsWith("```") || /^#{1,4}\s/.test(line) || isListItem(line);
}

/** Inline formatting: `code` and **bold**. */
function renderInline(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  const pattern = /(`[^`]+`|\*\*[^*]+\*\*)/g;
  let last = 0;
  let match: RegExpExecArray | null;
  let key = 0;

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > last) {
      nodes.push(<Fragment key={key++}>{text.slice(last, match.index)}</Fragment>);
    }
    const token = match[0];
    if (token.startsWith("`")) {
      nodes.push(
        <code key={key++} className="rounded bg-panel px-1 py-0.5 font-mono text-[0.85em] text-accent">
          {token.slice(1, -1)}
        </code>,
      );
    } else {
      nodes.push(
        <strong key={key++} className="font-semibold text-ink">
          {token.slice(2, -2)}
        </strong>,
      );
    }
    last = match.index + token.length;
  }

  if (last < text.length) {
    nodes.push(<Fragment key={key++}>{text.slice(last)}</Fragment>);
  }

  return nodes;
}
