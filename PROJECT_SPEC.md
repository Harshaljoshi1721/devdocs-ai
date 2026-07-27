# DevDocs AI

## Project Overview

DevDocs AI is a production-style, AI-powered developer knowledge and codebase intelligence platform.

The goal is to build a portfolio-quality full-stack application that allows developers to connect or upload software repositories and interact with their codebases using natural language.

The application should help developers:

- Understand unfamiliar codebases
- Search code using natural language
- Ask questions about project architecture
- Find relevant files and code
- Generate technical documentation
- Analyse errors and possible causes
- Generate project summaries
- Use AI agents with tools to perform developer productivity tasks

This is not intended to be a basic chatbot.

The project should demonstrate real-world software engineering and AI application development.

---

# Primary Goals

The project should demonstrate expertise in:

- C#
- ASP.NET Core
- REST API design
- Clean architecture
- Entity Framework Core
- PostgreSQL
- Next.js
- React
- TypeScript
- AI/LLM API integration
- Retrieval-Augmented Generation (RAG)
- Embeddings
- Vector search
- AI agents
- Tool/function calling
- Background processing
- Authentication and authorisation
- Testing
- Docker
- CI/CD
- Production-oriented architecture

The application should be designed as a real SaaS-style product.

---

# Product Name

DevDocs AI

Possible tagline:

> Understand any codebase. Ask questions. Generate knowledge.

---

# Core User Experience

A user should be able to:

1. Create an account
2. Create a project
3. Upload source code and documentation OR connect a GitHub repository
4. Process and index the project
5. Ask questions about the codebase
6. Receive AI-generated answers
7. See the sources used to generate the answer
8. Search the codebase using natural language
9. Generate technical documentation
10. Analyse errors
11. Use specialised AI agents

Example:

User:

> How does authentication work in this project?

DevDocs AI:

> Authentication is implemented using JWT tokens.

Relevant files:

- `AuthController.cs`
- `JwtTokenService.cs`
- `AuthenticationMiddleware.cs`

The answer should include references to the relevant files and, where possible, line numbers.

---

# High-Level Architecture

The system should be divided into:

## Frontend

Next.js
React
TypeScript
Tailwind CSS

Responsibilities:

- Authentication UI
- Project dashboard
- Project creation
- File upload
- Repository connection
- Document processing status
- AI chat interface
- Source citations
- Agent interface
- Documentation generation UI
- Error analysis UI
- Settings

---

## Backend

ASP.NET Core Web API

Responsibilities:

- Authentication
- User management
- Project management
- File management
- Repository ingestion
- Document processing
- Chunking
- Embedding generation
- Vector search
- RAG orchestration
- AI agent orchestration
- Tool execution
- Conversation management
- Background jobs
- Usage tracking
- Logging
- API security

---

## Database

PostgreSQL

The database should store:

- Users
- Projects
- Project members
- Documents
- Source files
- Document chunks
- Conversations
- Messages
- AI agents
- Tool executions
- Processing jobs
- Usage data

---

## Vector Storage

The system should use a vector-capable storage solution.

The initial implementation should prefer a solution that is:

- Easy to run locally
- Easy to deploy
- Compatible with PostgreSQL where practical
- Suitable for semantic search

The implementation should abstract vector storage behind an interface so the provider can be changed later.

---

# Recommended Backend Architecture

Use a clean, modular architecture.

Recommended structure:

backend/

    DevDocsAI.sln

    src/

        DevDocsAI.Api/

        DevDocsAI.Application/

        DevDocsAI.Domain/

        DevDocsAI.Infrastructure/

    tests/

        DevDocsAI.UnitTests/

        DevDocsAI.IntegrationTests/

The architecture should follow clear separation of concerns.

---

# Domain Layer

The Domain layer should contain:

- Entities
- Value objects
- Domain rules
- Enums
- Domain exceptions

The Domain layer must not depend on:

- ASP.NET Core
- Entity Framework Core
- Specific LLM providers
- Specific vector database providers
- Infrastructure services

---

# Application Layer

The Application layer should contain:

- Use cases
- Application services
- Interfaces
- DTOs
- Validation
- Commands
- Queries

Examples:

- CreateProject
- GetProject
- UploadDocument
- ProcessDocument
- AskQuestion
- SearchProject
- GenerateDocumentation
- AnalyseError

The Application layer should depend on abstractions rather than concrete infrastructure implementations.

---

# Infrastructure Layer

The Infrastructure layer should contain:

- Entity Framework Core
- PostgreSQL implementation
- LLM provider integrations
- Embedding provider integrations
- Vector storage implementation
- File storage
- GitHub integration
- Background job implementation

All external integrations should be abstracted behind interfaces.

---

# API Layer

The API layer should contain:

- Controllers or endpoint definitions
- Authentication configuration
- Dependency injection
- Middleware
- API configuration
- Exception handling
- OpenAPI configuration

The API should expose versioned endpoints where appropriate.

Example:

/api/v1/projects

/api/v1/projects/{projectId}

/api/v1/projects/{projectId}/documents

/api/v1/projects/{projectId}/chat

/api/v1/projects/{projectId}/search

/api/v1/projects/{projectId}/agents

---

# Core Domain Entities

The initial domain model should include:

## User

Fields:

- Id
- Email
- Name
- PasswordHash or external authentication identifier
- CreatedAt
- UpdatedAt

---

## Project

Fields:

- Id
- Name
- Description
- OwnerId
- CreatedAt
- UpdatedAt

A project represents a codebase or knowledge space.

---

## Document

A document may represent:

- Markdown file
- PDF
- Text file
- Source code file
- Configuration file

Fields:

- Id
- ProjectId
- Name
- Path
- FileType
- ContentHash
- Size
- ProcessingStatus
- CreatedAt
- UpdatedAt

---

## DocumentChunk

Fields:

- Id
- DocumentId
- Content
- ChunkIndex
- StartLine
- EndLine
- EmbeddingReference
- CreatedAt

Chunks should preserve source metadata.

The system should be able to answer:

> Which file did this content come from?

And preferably:

> Which lines did this content come from?

---

## Conversation

Fields:

- Id
- ProjectId
- UserId
- Title
- CreatedAt
- UpdatedAt

---

## Message

Fields:

- Id
- ConversationId
- Role
- Content
- CreatedAt

Roles:

- User
- Assistant
- System
- Tool

---

## AI Agent

Fields:

- Id
- ProjectId
- Name
- Description
- SystemInstructions
- CreatedAt
- UpdatedAt

Examples:

- Code Explorer
- Documentation Generator
- Bug Analysis Agent
- Architecture Analyst

---

# RAG Pipeline

The core RAG pipeline should be:

1. User uploads or connects a repository
2. Files are discovered
3. Unsupported files are ignored
4. Supported files are read
5. Content is normalised
6. Files are split into chunks
7. Chunk metadata is created
8. Embeddings are generated
9. Embeddings are stored
10. User asks a question
11. User query is embedded
12. Relevant chunks are retrieved
13. Retrieved chunks are optionally reranked
14. Context is constructed
15. The LLM generates an answer
16. Sources are returned with the answer

The system should prioritise grounded answers.

The AI should not confidently invent information that cannot be found in the indexed project.

When sufficient information is unavailable, the assistant should clearly state that the answer could not be determined from the available project context.

---

# Source Citations

Every RAG answer should attempt to provide source references.

Example:

Answer:

> Authentication is implemented using JWT tokens.

Sources:

- `src/Auth/AuthController.cs`
- `src/Auth/JwtTokenService.cs`

Where possible:

- File path
- Start line
- End line
- Relevant code excerpt

The frontend should render sources clearly.

---

# Supported File Types

Initial supported file types:

## Code

- .cs
- .js
- .jsx
- .ts
- .tsx
- .py
- .java
- .go
- .rs
- .php
- .rb

## Documentation

- .md
- .txt
- .rst

## Configuration

- .json
- .yaml
- .yml
- .xml
- .toml

The system should support extension-based filtering.

Sensitive files should be ignored by default.

Examples:

- .env
- .env.*
- private keys
- certificates
- secrets

---

# GitHub Repository Integration

Users should eventually be able to provide a GitHub repository.

The system should:

1. Validate the repository URL
2. Clone or retrieve repository contents
3. Ignore unsupported files
4. Respect .gitignore where possible
5. Process supported files
6. Create embeddings
7. Store project metadata

The system should not store secrets.

The GitHub integration should be implemented behind an abstraction.

---

# AI Features

## Code Explorer

The Code Explorer Agent should be able to:

- Search relevant files
- Read relevant files
- Explain code
- Trace relationships
- Identify implementation locations

Example:

> Where is user registration implemented?

The agent should search the indexed codebase and return relevant files.

---

## Documentation Generator

The Documentation Agent should be able to:

- Analyse source code
- Generate Markdown documentation
- Explain APIs
- Explain classes
- Explain modules
- Generate architecture summaries

---

## Bug Analysis Agent

The Bug Analysis Agent should accept:

- Error message
- Stack trace
- Optional user description

It should:

1. Search relevant project files
2. Identify potentially related code
3. Analyse the error
4. Explain possible causes
5. Suggest debugging steps

The agent must clearly distinguish between:

- Evidence from the codebase
- AI-generated hypotheses

---

## Architecture Analyst

The Architecture Analyst should:

- Analyse project structure
- Identify technologies
- Identify major modules
- Identify dependencies
- Generate an architecture summary

---

# Tool Calling

AI agents should eventually be able to use tools.

Example tools:

- SearchProject
- SearchFiles
- ReadFile
- FindReferences
- GetProjectStructure
- GetDocument
- GenerateDocumentation

Tools should have:

- Clear input schemas
- Clear output schemas
- Validation
- Logging
- Error handling

Tool calls should be observable.

---

# Background Processing

Document processing should not block HTTP requests for large repositories.

The system should eventually use background jobs for:

- File ingestion
- Document processing
- Chunking
- Embedding generation
- Repository indexing

The user should be able to see processing status.

Example statuses:

- Pending
- Processing
- Completed
- Failed

---

# Frontend Pages

The frontend should eventually contain:

## Landing Page

Explain:

- What DevDocs AI does
- Main features
- How it works

---

## Authentication

Pages:

- Login
- Register

---

## Dashboard

Display:

- Projects
- Recent activity
- Processing status

---

## Project Page

Display:

- Project overview
- File count
- Processing status
- Chat
- Search
- Agents
- Documentation

---

## AI Chat

Features:

- Conversation history
- Streaming responses if supported
- Markdown rendering
- Source citations
- Loading states
- Error states

---

## Project Search

Natural language search.

Example:

> Find all code related to payment processing.

Results should include:

- File path
- Relevant content
- Relevance score where appropriate

---

# Security Requirements

The project must follow secure development practices.

Requirements:

- Never commit secrets
- Never expose API keys to the frontend
- Use environment variables
- Validate uploads
- Limit upload sizes
- Prevent path traversal
- Ignore secrets and private keys
- Validate repository URLs
- Implement authentication
- Implement authorisation
- Ensure users cannot access another user's projects

---

# Testing Strategy

The project should include:

## Unit Tests

Test:

- Domain logic
- Chunking
- File filtering
- Application services
- Validation

---

## Integration Tests

Test:

- API endpoints
- Database operations
- Authentication
- Project access

---

## AI Evaluation

Eventually evaluate:

- Retrieval relevance
- Answer groundedness
- Citation accuracy
- Response quality

AI functionality should not only be tested manually.

---

# Development Principles

Follow these principles:

1. Build incrementally.
2. Do not generate the entire application in one step.
3. Do not introduce unnecessary complexity prematurely.
4. Prefer simple, maintainable solutions.
5. Use strong typing.
6. Keep abstractions meaningful.
7. Write tests for important business logic.
8. Keep commits focused.
9. Document important architectural decisions.
10. Never hide errors.
11. Never silently ignore failures.
12. Never hardcode secrets.
13. Prefer production-quality code over demo code.

---

# Development Phases

## Phase 1: Foundation

Build:

- Repository structure
- ASP.NET Core API
- Next.js frontend
- PostgreSQL
- Configuration
- Health check
- Basic logging
- Git setup

---

## Phase 2: Projects

Build:

- Project entity
- Create project
- List projects
- Get project
- Update project
- Delete project

---

## Phase 3: File Ingestion

Build:

- File upload
- File metadata
- File validation
- File storage
- Processing status

---

## Phase 4: Text Processing

Build:

- File readers
- Content extraction
- Chunking
- Source metadata

---

## Phase 5: RAG

Build:

- Embedding provider abstraction
- Embedding generation
- Vector storage
- Semantic search
- Context construction
- LLM integration
- Source citations

---

## Phase 6: AI Chat

Build:

- Conversations
- Messages
- Chat API
- Streaming responses if practical
- Conversation history

---

## Phase 7: GitHub Integration

Build:

- Repository connection
- Repository ingestion
- File indexing
- Processing status

---

## Phase 8: Agents

Build:

- Agent abstraction
- Code Explorer
- Documentation Agent
- Bug Analysis Agent
- Tool calling

---

## Phase 9: Production Quality

Add:

- Authentication
- Authorisation
- Rate limiting
- Structured logging
- Error handling
- Unit tests
- Integration tests
- Docker
- CI/CD

---

# Definition of Done

A feature is not complete until:

- It works
- It has appropriate validation
- It handles errors
- It has tests where appropriate
- It is documented where necessary
- It does not introduce unnecessary technical debt
- It is integrated cleanly with the existing architecture

---

# Important Instructions for the AI Coding Agent

The AI coding agent must:

1. Inspect the repository before making changes.
2. Understand the current state of the project.
3. Never assume that files exist.
4. Never overwrite existing work without checking first.
5. Explain the implementation plan before major changes.
6. Make changes in small logical increments.
7. Run tests after making changes.
8. Run builds after making changes.
9. Report errors clearly.
10. Avoid generating unnecessary files.
11. Avoid adding dependencies unless necessary.
12. Explain why a new dependency is needed.
13. Keep the architecture maintainable.
14. Prefer official documentation and current stable APIs.
15. Keep the project compatible with the development environment.

---

# Initial Technology Direction

Backend:

- C#
- ASP.NET Core
- Entity Framework Core
- PostgreSQL

Frontend:

- Next.js
- React
- TypeScript
- Tailwind CSS

AI:

- LLM provider abstraction
- Embedding provider abstraction
- RAG
- Vector search
- AI agents
- Tool calling

Development:

- Git
- Docker
- GitHub Actions

The exact provider choices should be evaluated during implementation rather than hardcoded prematurely.

---

# First Task

Before writing application code:

1. Inspect the current repository.
2. Confirm the repository is empty except for this specification document.
3. Propose the final initial folder structure.
4. Recommend the exact technology versions.
5. Recommend the initial database and AI provider strategy.
6. Identify any risks or decisions that should be made before implementation.
7. Create a concise implementation plan.
8. Do not start implementing the entire application yet.

After presenting the plan, wait for approval before creating the initial project structure.

---

# Project Philosophy

This project should be treated as a serious portfolio and learning project.

The goal is not merely to make something that works.

The goal is to build something that demonstrates:

- Strong backend engineering
- Modern frontend engineering
- Practical AI application development
- RAG understanding
- Agent architecture
- Clean code
- Testing
- Production thinking

Every major architectural decision should be understandable and explainable in a technical interview.