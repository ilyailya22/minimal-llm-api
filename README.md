# minimal-llm-api

A deliberately small ASP.NET Core minimal API in front of a local [Ollama](https://ollama.com) model.

**Status: work in progress.** The structure is in place; streaming, conversations,
tests and Docker are still to come — see [UPGRADE_PLAN.md](UPGRADE_PLAN.md).

## Why it is one project

The point of this repository is restraint. There is no Clean Architecture folder
set, no mediator, no repositories and no database — because none of them would
pay for themselves at this size. The single abstraction is `IOllamaClient`: it is
the seam the tests stub, and where a second provider would plug in.

## Endpoints today

| Method | Route         | Purpose                                |
|--------|---------------|----------------------------------------|
| `GET`  | `/`           | Liveness                               |
| `GET`  | `/api/models` | Models available on the Ollama host    |
| `POST` | `/api/chat`   | Single-shot chat completion            |

## Running it

Requires the .NET 10 SDK and Ollama running locally on `http://localhost:11434`.

```bash
ollama pull llama3.1
dotnet run --project src/MinimalLlm.Api
```

Then open `/scalar/v1` for interactive API docs, or:

```bash
curl -X POST localhost:5210/api/chat \
     -H "Content-Type: application/json" \
     -d '{"message":"Write a haiku about databases"}'
```

Configuration lives in `src/MinimalLlm.Api/appsettings.json`. Both keys are required and
validated at startup — a missing or malformed `BaseUrl` stops the app from booting rather
than failing at the first request:

```json
{ "Ollama": { "BaseUrl": "http://localhost:11434", "ChatModel": "llama3.1" } }
```
