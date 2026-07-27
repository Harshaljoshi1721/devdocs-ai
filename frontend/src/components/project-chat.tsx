"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Sources } from "@/components/citations";
import { Markdown } from "@/components/Markdown";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/misc";
import { useAuth } from "@/lib/auth";
import { readSse } from "@/lib/sse";
import type { ChatMessage, Conversation, ConversationDetail } from "@/lib/types";

interface Streaming {
  question: string;
  answer: string;
  error: string | null;
}

export function ChatPanel({ projectId }: { projectId: string }) {
  const { authFetch, authRaw } = useAuth();
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState<Streaming | null>(null);
  const threadRef = useRef<HTMLDivElement>(null);

  const conversations = useQuery({
    queryKey: ["conversations", projectId],
    queryFn: () => authFetch<Conversation[]>(`/api/v1/projects/${projectId}/conversations`),
  });

  const detail = useQuery({
    queryKey: ["conversation", selectedId],
    queryFn: () =>
      authFetch<ConversationDetail>(`/api/v1/projects/${projectId}/conversations/${selectedId}`),
    enabled: !!selectedId,
  });

  const messages = detail.data?.messages ?? [];
  const isStreaming = streaming !== null && streaming.error === null;

  // Keep the newest message in view as content streams in.
  useEffect(() => {
    threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight, behavior: "smooth" });
  }, [messages.length, streaming?.answer]);

  async function send(question: string) {
    setInput("");

    let conversationId = selectedId;
    if (!conversationId) {
      const created = await authFetch<Conversation>(
        `/api/v1/projects/${projectId}/conversations`,
        { method: "POST", body: { title: null } },
      );
      conversationId = created.id;
      setSelectedId(created.id);
      queryClient.invalidateQueries({ queryKey: ["conversations", projectId] });
    }

    setStreaming({ question, answer: "", error: null });

    try {
      const response = await authRaw(
        `/api/v1/projects/${projectId}/conversations/${conversationId}/messages/stream`,
        { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ question }) },
      );

      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new Error(problem?.detail || problem?.title || "The assistant could not respond.");
      }

      for await (const event of readSse(response)) {
        if (event.event === "token") {
          const { text } = JSON.parse(event.data) as { text: string };
          setStreaming((s) => (s ? { ...s, answer: s.answer + text } : s));
        }
      }

      // Persisted question + answer (with citations) now live server-side.
      await queryClient.invalidateQueries({ queryKey: ["conversation", conversationId] });
      queryClient.invalidateQueries({ queryKey: ["conversations", projectId] });
      setStreaming(null);
    } catch (e) {
      setStreaming((s) =>
        s ? { ...s, error: e instanceof Error ? e.message : "Something went wrong." } : s,
      );
    }
  }

  function startNewChat() {
    setSelectedId(null);
    setStreaming(null);
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[220px_1fr]">
      {/* Conversation list */}
      <aside className="flex flex-col gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={startNewChat}
          disabled={isStreaming}
        >
          + New chat
        </Button>
        <div className="flex flex-col gap-1">
          {(conversations.data ?? []).map((c) => (
            <button
              key={c.id}
              onClick={() => {
                if (!isStreaming) {
                  setSelectedId(c.id);
                  setStreaming(null);
                }
              }}
              className={`truncate rounded-md px-3 py-2 text-left text-sm transition-colors ${
                selectedId === c.id
                  ? "bg-panel text-ink"
                  : "text-muted hover:bg-panel/50 hover:text-ink"
              }`}
              title={c.title}
            >
              {c.title}
            </button>
          ))}
          {conversations.data?.length === 0 && (
            <p className="px-3 py-2 text-xs text-faint">No conversations yet.</p>
          )}
        </div>
      </aside>

      {/* Thread + composer */}
      <div className="flex min-h-[28rem] flex-col rounded-xl border border-line bg-panel/30">
        <div ref={threadRef} className="flex-1 space-y-5 overflow-y-auto p-5" style={{ maxHeight: "28rem" }}>
          {selectedId && detail.isLoading ? (
            <div className="flex h-full items-center justify-center text-muted">
              <Spinner />
            </div>
          ) : messages.length === 0 && !streaming ? (
            <EmptyThread />
          ) : (
            <>
              {messages.map((m) => (
                <MessageBubble key={m.id} message={m} />
              ))}
              {streaming && (
                <>
                  <MessageBubble
                    message={{ id: "pending-user", role: "User", content: streaming.question, citations: [], createdAt: "" }}
                  />
                  <div>
                    <span className="eyebrow text-accent/80">DevDocs AI</span>
                    <div className="mt-1.5">
                      {streaming.error ? (
                        <p className="text-sm text-danger">{streaming.error}</p>
                      ) : streaming.answer ? (
                        <Markdown content={streaming.answer} />
                      ) : (
                        <span className="inline-flex items-center gap-2 text-sm text-muted">
                          <Spinner /> Thinking…
                        </span>
                      )}
                    </div>
                  </div>
                </>
              )}
            </>
          )}
        </div>

        <Composer input={input} setInput={setInput} disabled={isStreaming} onSend={send} />
      </div>
    </div>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === "User";
  return (
    <div>
      <span className={`eyebrow ${isUser ? "text-ink/70" : "text-accent/80"}`}>
        {isUser ? "You" : "DevDocs AI"}
      </span>
      <div className="mt-1.5">
        {isUser ? (
          <p className="whitespace-pre-wrap text-sm text-ink">{message.content}</p>
        ) : (
          <>
            <Markdown content={message.content} />
            <Sources citations={message.citations} />
          </>
        )}
      </div>
    </div>
  );
}

function Composer({
  input,
  setInput,
  disabled,
  onSend,
}: {
  input: string;
  setInput: (v: string) => void;
  disabled: boolean;
  onSend: (q: string) => void;
}) {
  return (
    <form
      className="flex items-end gap-2 border-t border-line p-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (input.trim() && !disabled) onSend(input.trim());
      }}
    >
      <textarea
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            if (input.trim() && !disabled) onSend(input.trim());
          }
        }}
        rows={1}
        placeholder="Ask about this project…"
        className="max-h-32 flex-1 resize-none rounded-md border border-line bg-panel px-3 py-2 text-sm text-ink placeholder:text-faint focus:border-accent focus:outline-none"
      />
      <Button type="submit" disabled={disabled || !input.trim()}>
        {disabled ? <Spinner /> : "Send"}
      </Button>
    </form>
  );
}

function EmptyThread() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 text-center">
      <span className="grid h-11 w-11 place-items-center rounded-xl border border-line-strong bg-panel text-muted">
        ✦
      </span>
      <p className="font-display text-lg">Ask your codebase</p>
      <p className="max-w-sm text-sm text-muted">
        Answers are grounded in your indexed files and cite their sources. Upload documents in the
        Overview tab first.
      </p>
    </div>
  );
}
