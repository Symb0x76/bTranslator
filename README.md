# bTranslator

C# bootstrap implementation for a WinUI 3 rewrite of xTranslator.

## Current status
- Multi-project architecture is in place:
  - `src/bTranslator.App` (WinUI 3 shell + DI + logging)
  - `src/bTranslator.Domain`
  - `src/bTranslator.Application`
  - `src/bTranslator.Infrastructure.Bethesda`
  - `src/bTranslator.Infrastructure.Translation`
  - `src/bTranslator.Infrastructure.Persistence`
  - `src/bTranslator.Infrastructure.Security`
  - `src/bTranslator.Automation`
- Core contracts and domain models are implemented.
- Implemented services:
  - Bethesda STRINGS/DLSTRINGS/ILSTRINGS codec (round-trip tested)
  - Plugin document service with STRINGS and ESP/ESM record-field load/save flow
  - Translation orchestrator with retry, rate limiting and fallback
  - Placeholder protector for tags/numbers/newlines
  - DPAPI-backed credential store
  - SQLite settings store with WAL mode
  - Legacy batch parser/executor and YAML v2 parser
  - XML compatibility import/export scaffold
  - SST compatibility with legacy binary v1-v8 parser/writer plus JSON fallback
  - `_recorddefs.txt` parser and record-field mapper with xTranslator proc rules
  - BSA/BA2 archive toolchain (listing + BA2 GNRL/DX10 extraction + compressed/uncompressed BSA extraction)
  - PEX string-table toolchain baseline (load + export)
  - Translation providers: OpenAI-compatible, Ollama, Azure Translator, DeepL, Google Cloud Translate, Baidu, Tencent TMT, Volcengine, Anthropic, Gemini
- Tests:
  - unit tests for placeholder protection, batch execution, orchestrator fallback, DPAPI store
  - integration tests for STRINGS codec round-trip

## Build
```powershell
dotnet restore bTranslator.slnx
dotnet build bTranslator.slnx
dotnet test bTranslator.slnx
```

## Package (PowerShell)
```powershell
# Default: Release + win-x64, run tests before publish
.\Build.ps1

# Multi-RID output into .\Package\<rid>\
.\Build.ps1 -RuntimeIdentifiers win-x64,win-arm64

# Skip tests when needed
.\Build.ps1 -SkipTests
```

## Notes
- Provider adapters are implemented, but several require provider-specific credentials beyond a single API key (for example dual-key/signature providers like Baidu/Tencent).
- OpenAI-compatible and Ollama remain the easiest baseline for first-run end-to-end flow.
- Archive notes: BA2 `DX10` extraction now emits DDS files (DX10 header + payload chunks), and compressed BSA extraction supports zlib/deflate with LZ4 fallback.

