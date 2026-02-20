# Implementation Status

## Done in this bootstrap
- Solution and layered projects created.
- WinUI 3 shell with DI and Serilog bootstrapping.
- Core abstractions and domain models.
- STRINGS/DLSTRINGS/ILSTRINGS read/write codec.
- Plugin document service with linked STRINGS plus ESP/ESM record-field extraction and write-back.
- Translation orchestrator with fallback/retry/rate limiting.
- Placeholder protector for tags/numbers/newlines.
- DPAPI credential store.
- SQLite settings store (WAL).
- Legacy batch parser/executor + YAML v2 parser.
- XML compatibility import/export scaffold.
- SST compatibility service with legacy binary v1-v8 parser/writer and JSON fallback.
- `_recorddefs.txt`-driven mapping engine with xTranslator proc rules (`proc1/2/3/4/5`).
- BSA/BA2 archive toolchain with entry listing + BA2 GNRL/DX10 extraction + compressed/uncompressed BSA extraction.
- PEX toolchain baseline (string table loading + export).
- CI workflow for build/test on Windows.
- Unit and integration tests.

## Partially complete / still evolving
- Provider adapters:
  - Fully wired: OpenAI-compatible, Ollama, Azure Translator, DeepL, Google Cloud Translate, Baidu, Tencent TMT, Volcengine, Anthropic, Gemini.
  - Remaining gap: UI/credential management is still minimal (advanced provider credential UX and diagnostics pending).
- Archive toolchain notes:
  - BA2 `DX10` extraction writes DDS output by reconstructing DX10 headers from archive metadata.
  - Compressed payload decoding supports zlib/deflate with LZ4 fallback.
