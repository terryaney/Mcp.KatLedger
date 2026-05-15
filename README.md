# Mcp.KatLedger

KatLedger is a Windows-friendly stdio MCP server that exposes a generic local SQLite tool surface over `%USERPROFILE%\.kat\KatLedger\KatLedger.db`.

## Repository assumptions

- Public repository: `terryaney/Mcp.KatLedger`
- Current release tag example: `v0.2.1`
- Windows release asset: `KatLedger-win-x64.zip`

## Runtime contract

- Tool namespace is preserved as `kat/ledger/*`
- Canonical install folder is `%USERPROFILE%\.kat\KatLedger\`
- Canonical database path is `%USERPROFILE%\.kat\KatLedger\KatLedger.db`
- The published executable expected by client installers is `%USERPROFILE%\.kat\KatLedger\KatLedger.exe`
- The server does not bootstrap an application schema; callers own their tables and SQL

## Tools

- `kat/ledger/execute`
  - Executes exactly one non-`SELECT` / non-`WITH` SQLite statement
  - Returns `statementType`, `rowsAffected`, and `lastInsertRowId`
- `kat/ledger/query`
  - Executes exactly one `SELECT` or `WITH` statement
  - Optional `limit` defaults to `50`, max `200`
  - Returns `statementType`, `returned`, `limit`, `truncated`, `columns`, and `rows`
- `kat/ledger/query_one`
  - Executes exactly one `SELECT` or `WITH` statement
  - Returns `statementType`, `found`, `multiple`, `columns`, and `row`

## Safety boundaries

- `ATTACH` and `DETACH` are blocked
- Multi-statement SQL is blocked
- `execute` rejects `SELECT` and `WITH`
- `query` and `query_one` reject non-`SELECT` / non-`WITH`
- Row caps are enforced server-side regardless of caller SQL
- No database path parameter is accepted; all tools target the canonical KatLedger database

## Local development

```powershell
dotnet build .\KatLedger.csproj -c Release --nologo
dotnet run --project .\KatLedger.csproj -c Release -- --self-test
```

## Publish a Windows release artifact

```powershell
.\scripts\publish-win-x64.ps1 -Version 0.2.1
```

Expected output:

- Zip artifact: `artifacts\KatLedger-win-x64.zip`
- Published files: `artifacts\publish\win-x64\`

The zip is shaped so it can be extracted directly into `%USERPROFILE%\.kat\KatLedger\`.

Example local install:

```powershell
$installRoot = Join-Path $env:USERPROFILE '.kat\KatLedger'
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Expand-Archive -LiteralPath .\artifacts\KatLedger-win-x64.zip -DestinationPath $installRoot -Force
```

## GitHub release automation

`.github/workflows/release.yml` builds, self-tests, publishes `KatLedger-win-x64.zip`, uploads it as a workflow artifact, and when a `v*` tag is pushed it creates or updates the corresponding GitHub release asset.
