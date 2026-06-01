---
name: aspire-dev-loop
description: Operate the CeoAgent .NET Aspire local development loop. Use when starting or checking CeoAgent.AppHost, inspecting API/Worker/PostgreSQL/Azure Storage resource health, finding local endpoints, reading Aspire logs, verifying /health or /scalar, or choosing the right dotnet build/test commands for this repository.
---

# Aspire Dev Loop

Use this skill for local runtime work in the CeoAgent repository. The AppHost is `CeoAgent.AppHost/CeoAgent.AppHost.csproj`; it orchestrates API, Worker, PostgreSQL, Azure Queue Storage, Azure Blob Storage, and optional Langfuse/Key Vault wiring.

## Workflow

1. Confirm repository root contains `CEOAgent.slnx` and `aspire.config.json`.
2. Check local prerequisites only when needed: .NET SDK from `global.json`, Docker Desktop or equivalent container runtime, and user-secrets described in `README.md`.
3. Prefer the AppHost path from `aspire.config.json`. Start with:

```powershell
dotnet run --project CeoAgent.AppHost/CeoAgent.AppHost.csproj
```

4. If Aspire CLI is available, use it for resource status, waits, logs, and endpoint discovery instead of guessing ports.
5. Verify the API through `/health` first. Use `/scalar` only for development OpenAPI inspection.
6. For code changes, run the narrowest meaningful check first, then broaden:

```powershell
dotnet build CEOAgent.slnx
dotnet test CEOAgent.slnx --no-build
```

## Repository Rules

- Do not apply database migrations automatically. Scaffolding/review is allowed; `dotnet ef database update` is an operator action unless the user explicitly asks.
- Keep Aspire as local orchestration only. Do not infer production Azure topology from AppHost code.
- Do not put real provider secrets, tokens, API keys, or connection strings into repo files.
- Treat `CeoAgent.Web` as non-core unless the user is specifically working on the web template.
- When diagnosing API/Worker issues, inspect `CeoAgent.ServiceDefaults`, `CeoAgent.ApiService`, `CeoAgent.Worker`, and `CeoAgent.Infrastructure` before changing adapters.

## Common Focused Tests

- API shell/error/health: `dotnet test CEOAgent.slnx --filter "RuntimeShell|Health|Exception"`
- Admin/company isolation: `dotnet test CEOAgent.slnx --filter "Company|Admin|Persistence"`
- WhatsApp ingestion: `dotnet test CEOAgent.slnx --filter "WhatsApp|Webhook"`
- Queue/blob/job work: `dotnet test CEOAgent.slnx --filter "Queue|Blob|Job"`
- Google Calendar tools: `dotnet test CEOAgent.slnx --filter "Calendar|Availability|Reservation"`
