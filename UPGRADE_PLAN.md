# TestAI → MinimalLlm — upgrade plan

Turn the current experiment into a small, finished, defensible portfolio project.
Target: **~2 days** (≈13 focused hours). Written for Claude Code, one stage at a time.

---

## The governing constraint

**This project's value is that it is small.**

TicketPeak proves you can layer, extract services and justify complexity. This project proves the opposite and rarer skill: knowing when *not* to. One project, no Clean Architecture folders, no mediator, no repositories, no database. Every stage below either deletes something or adds a feature that pays for itself.

If a change makes the project bigger without making it better, it does not go in.

---

## Current state (audited)

| | |
|---|---|
| Git | Branch `master`, **zero commits**, **no remote** — clean slate |
| Projects | `TestAI.Api` + `TestAI.Application` — **2 projects for 11 files** |
| Framework | .NET 9, Swashbuckle |
| Endpoints | `POST /api/ai/chat`, `GET /api/ai/models` |
| Tests | None |
| README / Docker / CI | None |
| `appsettings.json` | Untracked — not in git |

### Three real defects to fix

1. **`OllamaOptions` is bound twice and injected never.** `Program.cs` calls `Configure<OllamaOptions>(...)`, but both `OllamaClient` and the `AddHttpClient` callback then re-read configuration manually with `GetSection().Get<OllamaOptions>()`. Inject `IOptions<OllamaOptions>` and delete the manual reads.
2. **Empty message returns 500.** `ChatService` throws `ArgumentException`, nothing maps it — a client mistake surfaces as a server error. Should be `400` + `ProblemDetails`.
3. **`IChatService` / `ChatService` is a pass-through.** It validates one string and forwards the call. That is not a service layer, it is a redirect. Delete both.

---

## Naming

| | Recommended |
|---|---|
| GitHub repo | `minimal-llm-api` |
| Solution | `MinimalLlm.sln` |
| Projects | `MinimalLlm.Api`, `MinimalLlm.Tests` |
| Root namespace | `MinimalLlm` |

`TestAI` reads as a throwaway — the word "Test" is doing real damage on a CV. `minimal-llm-api` is descriptive, keyword-rich for search, and states the architectural choice in the name.

*Alternative:* `ollama-chat-api` if you want it tied explicitly to Ollama. Weaker if you later add a second provider.

---

## Stage 0 — Rename and restructure · ~1 h

**Do**
- Rename local folder `C:\Repositories\TestAI` → `C:\Repositories\minimal-llm-api`.
- Collapse `TestAI.Application` into `MinimalLlm.Api`. Final layout:

```
minimal-llm-api/
├─ MinimalLlm.slnx
├─ README.md
├─ .gitignore                 ← extend: .vs/, *.user, .idea/, *.DS_Store
├─ .editorconfig
├─ docker-compose.yml
├─ src/MinimalLlm.Api/
│  ├─ Program.cs              ← wiring only
│  ├─ Endpoints/
│  │  ├─ ChatEndpoints.cs
│  │  ├─ ConversationEndpoints.cs
│  │  └─ ModelEndpoints.cs
│  ├─ Ollama/
│  │  ├─ OllamaClient.cs
│  │  ├─ IOllamaClient.cs
│  │  ├─ OllamaOptions.cs
│  │  └─ OllamaContracts.cs   ← request/response records for the Ollama wire format
│  ├─ Chat/
│  │  ├─ ChatContracts.cs     ← ChatRequest / ChatResponse / ChatMessage
│  │  └─ ConversationStore.cs
│  ├─ Health/OllamaHealthCheck.cs
│  ├─ appsettings.json        ← commit this
│  └─ Dockerfile
└─ tests/MinimalLlm.Tests/
```

- Delete `IChatService`, `ChatService`.
- Rename namespaces `TestAI.*` → `MinimalLlm`.
- `git branch -m master main`, then a real first commit.
- Create the GitHub repo `minimal-llm-api`, `git remote add origin`, push.

**Keep `IOllamaClient`.** One interface, and it earns its place twice: it is the seam the tests stub, and it is where a second provider would plug in. That is the *only* abstraction in the project — be ready to say so.

**Acceptance:** solution builds, one project plus tests, `git log` shows a clean first commit on `main`, repo is on GitHub.

---

## Stage 1 — Fix and modernise · ~1.5 h

**Do**
- Target **`net10.0`**.
- Replace Swashbuckle with `Microsoft.AspNetCore.OpenApi` + **Scalar** (`Scalar.AspNetCore`). Swashbuckle left the templates in .NET 9 — using it now dates the project.
- Fix defect 1: inject `IOptions<OllamaOptions>` into `OllamaClient`; configure the typed client with `AddHttpClient<IOllamaClient, OllamaClient>()` reading options from DI, not `IConfiguration`.
- Validate config at startup:
  ```csharp
  builder.Services.AddOptions<OllamaOptions>()
      .BindConfiguration(OllamaOptions.SectionName)
      .ValidateDataAnnotations()
      .ValidateOnStart();
  ```
- Fix defect 2: `AddProblemDetails()` + validation in the endpoint returning `Results.ValidationProblem`.
- Turn on `<Nullable>enable</Nullable>` (already on) and add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Replace the anonymous-object request bodies in `OllamaClient` with `record` contracts and source-generated `JsonSerializerContext`.

**Acceptance:** a missing `Ollama:BaseUrl` fails at startup with a clear message, not at first request. Empty message returns `400` with a problem document.

---

## Stage 2 — Streaming · ~2–3 h · **the headline feature**

A chat API that makes you wait 30 seconds for a wall of text is a demo. Streaming is what makes it look like a real product, and it is the most interesting code in the project.

**Do**
- Add `POST /api/chat/stream`.
- In `OllamaClient`, add `IAsyncEnumerable<string> StreamChatAsync(...)`: post with `stream: true`, read the response with `HttpCompletionOption.ResponseHeadersRead`, and parse the NDJSON line by line, yielding each token.
- Return it as **server-sent events**, propagating the client's `CancellationToken` so closing the browser actually stops generation on the model.
- Sanity-check the cancellation path — it is the part interviewers ask about.

**Acceptance:**
```bash
curl -N -X POST localhost:8080/api/chat/stream -H "Content-Type: application/json" \
     -d '{"message":"Write a haiku about databases"}'
```
prints tokens progressively. Killing the curl stops the Ollama generation.

---

## Stage 3 — Conversations · ~2 h

Multi-turn memory, without a database.

**Do**
- `ConversationStore` over `IMemoryCache`: `conversationId → List<ChatMessage>`, sliding expiration ~30 min, a cap on messages per conversation.
- `conversationId` becomes an **optional field on the chat request** — absent means a new conversation; the id comes back in the response. This keeps the endpoint count down.
- `GET /api/conversations/{id}` returns history; `DELETE /api/conversations/{id}` clears it.
- Send the full message list to Ollama's `/api/chat` so the model actually has context.

**Why no database:** conversations here are ephemeral demo state with a natural TTL — the same reasoning as Redis seat holds in TicketPeak. Write one paragraph in the README saying exactly that, and note what would change if the requirement were durable history. This is a deliberate decision you can defend, not a shortcut.

**Acceptance:** two sequential calls with the same `conversationId` — the model correctly answers a follow-up like "what did I just ask you?".

---

## Stage 4 — Production manners · ~2 h

Small, built-in, no new packages.

**Do**
- **Rate limiting** — `AddRateLimiter` with a **concurrency limiter** (permit limit 2, small queue). A local model serves one or two generations at a time; parallel requests thrash it. This is a genuinely justified use, unlike the usual decorative rate limit.
- **Health check** — `/health` with `OllamaHealthCheck` pinging `/api/tags`. Marks the app unhealthy when the model host is down.
- **Timeouts** — a generous `HttpClient.Timeout` (generation is slow) plus `CancellationToken` end to end.
- **Logging** — log model name, prompt length and elapsed time per request. No prompt contents (privacy).

**Deliberately skipped: Polly / retries.** Retrying a half-streamed generation restarts it from scratch and double-bills the work; on a local model, a failure means it is down, and a retry will not help. Put that sentence in the README — a considered *no* reads stronger than a reflexive `AddStandardResilienceHandler()`.

**Acceptance:** a third concurrent request queues rather than degrading the other two. `/health` goes unhealthy when Ollama is stopped.

---

## Stage 5 — Tests · ~2 h

**Do**
- `MinimalLlm.Tests`: xUnit + `WebApplicationFactory`.
- Stub Ollama with a fake `HttpMessageHandler` returning canned JSON and canned NDJSON — **tests must not need a running model**.
- Cover: happy-path chat · empty message → 400 · model list · streaming yields multiple chunks in order · cancellation stops enumeration · conversation retains context across two calls · unknown conversation id → 404 · health check when the client throws.

**No Testcontainers here** — there is no infrastructure to containerise, and stubbing HTTP is the correct seam. Knowing when Testcontainers is overkill is the same judgment this project is about.

**Acceptance:** `dotnet test` green in under 10 seconds with Ollama not running.

---

## Stage 6 — Ship it · ~2 h

**Do**
- **Dockerfile** for the API (multi-stage, non-root user).
- **docker-compose.yml**: API + `ollama/ollama` + an init step pulling a **small** model (`llama3.2:1b` or `qwen2.5:0.5b`). A reviewer will not wait for a 5 GB download — this detail decides whether anyone actually runs your project.
- **GitHub Actions**: restore → build → test → `dotnet format --verify-no-changes`.
- **README.md** — the real deliverable:
  1. One-line description and an animated GIF or asciinema of streaming output.
  2. `docker compose up` → working in under 5 minutes.
  3. Endpoint table.
  4. **"Design notes"** — the section that matters: why one project and not layers; why `IOllamaClient` is the only interface; why no database; why no retry policy. Four short paragraphs.
  5. What would change to make it production-grade: auth, persistent history, multiple providers, token accounting.

**Acceptance:** clone → `docker compose up` → streaming chat works, on a machine with nothing preinstalled.

---

## Explicitly NOT doing

MediatR · AutoMapper · repository pattern · Clean Architecture / Domain layers · a database · Testcontainers · Polly · authentication · RAG, embeddings or a vector store · Aspire.

RAG is the obvious next project, not a bolt-on here. Keeping it out is the point.

---

## Endpoint surface (final)

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/health` | Liveness + Ollama reachability |
| `GET` | `/api/models` | Models available on the host |
| `POST` | `/api/chat` | Single response, optional `conversationId` |
| `POST` | `/api/chat/stream` | SSE token stream, optional `conversationId` |
| `GET` | `/api/conversations/{id}` | Message history |
| `DELETE` | `/api/conversations/{id}` | Clear a conversation |

---

## Order and time

| Stage | Hours | Blocking? |
|---|---|---|
| 0 Rename & restructure | 1.0 | yes — everything else builds on it |
| 1 Fix & modernise | 1.5 | yes |
| 2 Streaming | 2.5 | **highest value — do not skip** |
| 3 Conversations | 2.0 | |
| 4 Production manners | 2.0 | |
| 5 Tests | 2.0 | |
| 6 Docker + README + CI | 2.0 | **README is what gets read first** |

**≈13 hours.** If you run short, cut Stage 3 before Stage 5 or 6 — a tested, documented, streaming single-turn API beats an untested multi-turn one with no README.

---

## Interview beats this project buys you

1. *Why is this one project when TicketPeak is twelve?* — the answer that shows architecture is a decision, not a habit.
2. *Walk me through streaming an LLM response and cancelling it mid-generation.*
3. *Why no retry policy?*
4. *Why no database for conversation history — and what would change your mind?*
5. *Why is `IOllamaClient` the only interface in the codebase?*
