# DevDocs AI

> Understand any codebase. Ask questions. Generate knowledge.

An AI-powered developer knowledge and codebase-intelligence platform. Connect or
upload repositories and interact with them in natural language: grounded,
citation-backed answers, semantic code search, documentation generation, error
analysis, and tool-using AI agents.

This is a production-oriented, portfolio-quality full-stack application. See
[`PROJECT_SPEC.md`](PROJECT_SPEC.md) for the product vision and
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for the architecture and phased
delivery plan.

## Tech stack

- **Backend:** C# / .NET 10, ASP.NET Core Web API, EF Core, PostgreSQL + pgvector
- **AI:** Google Gemini (embeddings + chat, free tier) — RAG, vector search, tool-using agents — all behind provider interfaces (swappable to Claude/OpenAI via config)
- **Frontend:** Next.js (App Router), React, TypeScript, Tailwind CSS
- **Ops:** Docker, docker-compose, GitHub Actions

## Architecture

Clean, layered backend — `Domain → Application → Infrastructure → Api`, with all
external integrations (LLM, embeddings, vector store, file storage, GitHub) behind
Application-layer interfaces. Full detail in [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

```
backend/
  src/DevDocsAI.Domain          # entities, value objects, enums, domain rules (no external deps)
  src/DevDocsAI.Application      # use cases, DTOs, validation, port interfaces
  src/DevDocsAI.Infrastructure   # EF Core, pgvector, provider adapters, storage, jobs
  src/DevDocsAI.Api              # controllers, auth, middleware, OpenAPI, DI
  tests/DevDocsAI.UnitTests
  tests/DevDocsAI.IntegrationTests
frontend/                        # Next.js app
```

## Getting started (local)

### Prerequisites

- .NET SDK 10+
- Node.js 20+ (22 recommended)
- Docker + Docker Compose

### 1. Configure environment

```bash
cp .env.example .env
# edit .env — set a local POSTGRES_PASSWORD and (from Phase 5) provider API keys
```

### 2. Run everything with Docker

```bash
docker compose up --build
```

- API: http://localhost:8080 — health at http://localhost:8080/health, API docs at http://localhost:8080/scalar
- Web: http://localhost:3000
- Postgres+pgvector: localhost:5432

### 3. Or run pieces directly

Backend:

```bash
cd backend
dotnet build
dotnet test
dotnet run --project src/DevDocsAI.Api
```

Frontend:

```bash
cd frontend
npm install
npm run dev
```

## Project status

Phase 1 (Foundation) — repo skeleton, layered backend, Next.js app, Postgres+pgvector,
config/logging/health, CI skeleton. Subsequent phases (auth, ingestion, RAG, chat,
GitHub, agents, production hardening) per [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

## Security

Secrets live only in `.env` (gitignored) / environment variables — never committed.
API keys are never exposed to the frontend. See the security requirements in
[`PROJECT_SPEC.md`](PROJECT_SPEC.md).
