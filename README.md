# RagDemo

A Retrieval Augmented Generation (RAG) demo application. Crawl any website, index it into a vector database, then chat with it using Claude.

## Architecture

```
RagDemo/
├── RagDemo.Core/          # Business logic (class library)
│   ├── Interfaces/        # Service contracts
│   ├── Models/            # Domain models
│   └── Services/          # Implementations
├── RagDemo.Api/           # ASP.NET Core Web API
│   └── Controllers/       # IngestController, ChatController, StatusController
└── RagDemo.Web/           # React + Vite frontend
    └── src/
        └── components/    # UrlIngest, ChatWindow
```

**Stack:** .NET 10 · React 18 · Vite · Tailwind CSS · Qdrant · OpenAI Embeddings · Anthropic Claude

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) for Qdrant

## Setup

### 1. Start Qdrant

```bash
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### 2. Configure API keys

Edit `RagDemo.Api/appsettings.json` and fill in your keys:

```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  },
  "Anthropic": {
    "ApiKey": "sk-ant-..."
  }
}
```

Or set environment variables (preferred for production):

```bash
export OpenAI__ApiKey=sk-...
export Anthropic__ApiKey=sk-ant-...
```

### 3. Run the API

```bash
cd RagDemo.Api
dotnet run
# API listens on http://localhost:5000
```

### 4. Run the frontend

```bash
cd RagDemo.Web
npm install
npm run dev
# Opens http://localhost:5173
```

## API Reference

### `POST /api/ingest`

Crawls a URL and indexes it. Streams progress via Server-Sent Events.

**Request:**
```json
{ "url": "https://example.com", "sessionId": "uuid" }
```

**SSE events:**
```json
{ "stage": "crawling", "message": "Crawled: https://...", "pagesFound": 5 }
{ "stage": "complete", "message": "...", "pagesFound": 12, "chunksStored": 87, "isComplete": true }
```

### `POST /api/chat`

Answers a question using RAG. Streams tokens via SSE.

**Request:**
```json
{ "sessionId": "uuid", "question": "What does this site do?" }
```

**SSE events:**
```
data: {"token": "This "}
data: {"token": "site "}
data: [DONE]
```

### `GET /api/status/{sessionId}`

Returns crawl stats for a session.

**Response:**
```json
{
  "sessionId": "uuid",
  "pagesCrawled": 12,
  "chunksStored": 87,
  "sourceUrls": ["https://..."],
  "lastIngestAt": "2025-01-01T00:00:00Z"
}
```

## How it works

1. **Crawl** — `WebCrawler` fetches pages via `HttpClient`, extracts clean text with HtmlAgilityPack, follows internal links up to 3 levels deep (max 50 pages).
2. **Chunk** — `TextChunker` splits text into ~600-token chunks with 100-token overlap, preserving sentence boundaries.
3. **Embed** — `EmbeddingService` calls OpenAI `text-embedding-3-small` in batches of 20.
4. **Store** — `QdrantService` upserts vectors with metadata (text, source URL, session ID) into Qdrant.
5. **Retrieve** — At chat time, the question is embedded, then top-5 similar chunks are fetched by cosine similarity filtered to the session.
6. **Generate** — `AnthropicService` builds a RAG prompt and streams the response from `claude-sonnet-4-20250514`.

## Session isolation

Each browser session gets a UUID (`sessionId`). All vectors are tagged with this ID, so queries only retrieve chunks from that session's ingest. Multiple users can run independently against the same Qdrant collection.
