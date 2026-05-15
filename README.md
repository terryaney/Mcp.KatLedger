# Mcp.KatLedger

KatLedger is a Windows-friendly stdio MCP server for KAT verification ledger operations.

## Repository assumptions

- Public repository: `terryaney/Mcp.KatLedger`
- Initial release tag: `v0.1.0`
- Windows release asset: `KatLedger-win-x64.zip`

## Runtime contract

- Tool namespace is preserved as `kat/ledger/*`
- Canonical install folder is `%USERPROFILE%\.kat\KatLedger\`
- Canonical database path is `%USERPROFILE%\.kat\KatLedger\KatLedger.db`
- The published executable expected by client installers is `%USERPROFILE%\.kat\KatLedger\KatLedger.exe`

The server bootstraps its SQLite schema on startup and creates:

- `anvil_checks`
- supporting indexes
- `PRAGMA user_version = 1`

## Tools

- `kat/ledger/insert_check`
- `kat/ledger/count_checks`
- `kat/ledger/list_checks`
- `kat/ledger/read_checks`

All tool calls require both `workspace` and `task_id`.

## Local development

```powershell
dotnet build .\KatLedger.csproj -c Release --nologo
dotnet run --project .\KatLedger.csproj -c Release -- --self-test
```

## Publish a Windows release artifact

```powershell
.\scripts\publish-win-x64.ps1 -Version 0.1.0
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
