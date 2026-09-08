# minimal-llm-api

A small ASP.NET Core API in front of a local [Ollama](https://ollama.com) model, with
token-by-token streaming, multi-turn conversations, and cancellation that actually stops
the model.

One project. One interface. No database.

```bash
curl -N -X POST localhost:8080/api/chat/stream \
     -H "Content-Type: application/json" \
     -d '{"message":"Write a haiku about databases"}'

event: conversation
data: 0499b35769104be1b67ac82f719bc7f6

event: token
data: Structured

event: token
data:  knowledge

event: token
data: Rows
...
```

Real output from `llama3.2:1b`: the first token lands about two seconds in, the rest
arrive roughly 60 ms apart.

## Run it

```bash
docker compose up
```

That starts Ollama, pulls `llama3.2:1b` (~1.3 GB — small enough that you will actually
wait for it), and serves the API on `http://localhost:8080`. Measured from a fresh clone
with no cached model: 47 seconds to a healthy API on a warm image cache; add the base
image pulls on a cold machine.

Interactive API docs: <http://localhost:8080/scalar/v1>

Without Docker, against an Ollama you already run:

```bash
ollama pull llama3.1
dotnet run --project src/MinimalLlm.Api
```

## Endpoints

| Method   | Route                      | Purpose                                            |
|----------|----------------------------|----------------------------------------------------|
| `GET`    | `/health`                  | Liveness plus Ollama reachability                  |
| `GET`    | `/api/models`              | Models available on the host                       |
| `POST`   | `/api/chat`                | Single response; optional `conversationId`         |
| `POST`   | `/api/chat/stream`         | SSE token stream; optional `conversationId`        |
| `GET`    | `/api/conversations/{id}`  | Message history                                    |
| `DELETE` | `/api/conversations/{id}`  | Clear a conversation                               |

Both chat endpoints take `{"message": "...", "conversationId": "..."}`. Omit
`conversationId` to start a new conversation — the id comes back on the response body, and
on the streaming endpoint as both an `X-Conversation-Id` header and a leading
`conversation` event, so you can continue the thread without reading the stream to the end.

Configuration is two required keys, validated at startup rather than on the first request:

```json
{ "Ollama": { "BaseUrl": "http://localhost:11434", "ChatModel": "llama3.1" } }
```

## Design notes

**Why one project, and not layers.** This codebase is about 500 lines. Splitting it into
Api / Application / Domain / Infrastructure would add four project files, four namespaces
and a dependency graph to navigate, and would not remove a single decision from the reader.
Layers earn their keep when there is enough behaviour that finding things becomes the
bottleneck; below that threshold they are cost without benefit. Folders here group by
feature — `Chat`, `Ollama`, `Endpoints`, `Health` — which is the same organising idea at a
size that fits in one project. When it outgrows that, the feature folders are already the
seams to split along.

**Why `IOllamaClient` is the only interface.** An interface is worth its indirection when
something other than production code needs to stand behind it. This one does, twice: the
test suite substitutes a stub so the whole suite runs with no model installed, and a second
provider would plug in here. Every other type in this project has exactly one
implementation and is used directly. The pass-through `IChatService` that the first draft
had — validate one string, forward the call — was deleted for failing that test.

**Why no database.** Conversation history here is ephemeral demo state with a natural
expiry: nobody resumes a chat with a locally-hosted model three days later. `IMemoryCache`
with a 30-minute sliding TTL and a message cap models exactly that, in one file and with no
schema, migrations or container. The tradeoff is explicit: history is per-process, so it is
lost on restart and would not survive a second replica. If the requirement changed to
durable, shareable history, the store is one class behind one call site — Postgres for
history you query, Redis if you only need it shared and still expiring. What would *not*
change is the endpoint surface, because `conversationId` is already the only handle the
client holds.

**Why no retry policy.** Reaching for `AddStandardResilienceHandler()` here would be a
reflex, not a decision. Retrying a half-streamed generation restarts it from scratch,
throwing away the tokens already delivered and doubling the work the model does. And
against a local model, a failed request almost always means the host is down or the model
is not pulled — conditions a retry cannot fix and will only prolong. The health check
reports that state instead. A hosted provider with transient 429s and 503s would be a
different situation, and would deserve a different answer.

**Why the rate limit is a concurrency limiter.** A local model serves one or two
generations at a time; sending it four in parallel does not serve four callers faster, it
makes all four slower. The limiter admits two and queues the rest, which is load-shedding
rather than decoration. Measured on a stubbed 3.5-second generation: four concurrent
requests finish at 3.6s, 5.7s, 7.1s and 9.2s — the first is not slowed at all.

## Testing

```bash
dotnet test
```

Green in under four seconds with Ollama not running. Ollama is stubbed at the
`HttpMessageHandler` boundary — canned JSON for the buffered path, canned NDJSON for the
streaming one — which is the right seam and needs no Testcontainers: there is no
infrastructure here to containerise. The suite covers the streaming order, the cancellation
path, conversation context reaching the model, and the health check's three states.

## What would change to make this production-grade

- **Authentication.** There is none. Anything public needs at minimum an API key per
  caller, and the rate limiter would key on the caller rather than the process.
- **Durable history**, per the design note above, plus a retention policy — chat logs are
  user data.
- **Multiple providers.** `IOllamaClient` becomes `IChatProvider`, with the model name
  selected per request instead of fixed in configuration.
- **Token accounting.** Ollama reports `prompt_eval_count` and `eval_count` per response;
  those belong in the logs and in a per-caller budget.
- **Tracing.** OpenTelemetry spans around generation, so a slow answer can be attributed to
  queueing versus the model itself.
