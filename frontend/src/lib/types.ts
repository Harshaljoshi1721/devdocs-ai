export interface User {
  id: string;
  email: string;
  name: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Project {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  createdAt: string;
  updatedAt: string;
}

export interface Citation {
  documentId: string;
  documentName: string;
  path: string;
  startLine: number;
  endLine: number;
}

export type MessageRole = "User" | "Assistant";

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  citations: Citation[];
  createdAt: string;
}

export interface Conversation {
  id: string;
  projectId: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface ConversationDetail extends Conversation {
  messages: ChatMessage[];
}

export interface SearchHit {
  chunkId: string;
  documentId: string;
  documentName: string;
  path: string;
  startLine: number;
  endLine: number;
  score: number;
  snippet: string;
}

export interface SearchResponse {
  query: string;
  results: SearchHit[];
}

export interface ProjectDocument {
  id: string;
  name: string;
  path: string;
  fileType: string;
  size: number;
  contentHash: string;
  status: string;
  error: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RejectedFile {
  fileName: string;
  reason: string;
}

export interface AgentInfo {
  type: string;
  displayName: string;
  description: string;
}

export interface TraceItem {
  sequence: number;
  toolName: string;
  input: string;
  output: string;
  status: string;
  error: string | null;
  durationMs: number;
}

export interface AgentRunResponse {
  id: string;
  agentType: string;
  status: string;
  output: string | null;
  error: string | null;
  iterations: number;
  trace: TraceItem[];
  createdAt: string;
}

export interface UsageByKind {
  kind: string;
  requests: number;
  tokensIn: number;
  tokensOut: number;
}

export interface UsageSummary {
  totalRequests: number;
  totalTokensIn: number;
  totalTokensOut: number;
  byKind: UsageByKind[];
}

export interface RepositoryConnection {
  id: string;
  projectId: string;
  provider: string;
  url: string;
  owner: string;
  repo: string;
  ref: string | null;
  commitSha: string | null;
  status: string;
  error: string | null;
  fileCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface UploadResult {
  accepted: ProjectDocument[];
  rejected: RejectedFile[];
}
