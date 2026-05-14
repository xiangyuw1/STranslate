# AGENTS.md

## Repository overview
- STranslate is a Windows-only WPF desktop translation and OCR tool built with .NET 10 and C#.
- The project uses a plugin architecture; official plugins live under `src/Plugins/`.
- A small Rust helper binary (`src/STranslate.Host/`) handles update/start/backup tasks for the packaged application.
- This repository does not contain an automated test suite; verification is manual.

## Key paths
- Repo root contains release packaging scripts: `build.ps1` and `post_build.ps1`.
- .NET solution: `src/STranslate.slnx` (XML-based `.slnx`, not `.sln`).
- Main WPF application: `src/STranslate/STranslate.csproj` (startup object `STranslate.App`).
- Plugin SDK: `src/STranslate.Plugin/STranslate.Plugin.csproj`.
- Official plugins: `src/Plugins/<plugin-name>/Main.cs` and `src/Plugins/<plugin-name>/plugin.json`.
- Rust host helper: `src/STranslate.Host/Cargo.toml` and `src/STranslate.Host/src/main.rs`.
- Documentation entry: `src/CLAUDE.md` with detailed docs linked from `src/docs/`.

## Developer commands
- From `src/`, run the application in debug mode:
  ```powershell
  dotnet run --project STranslate/STranslate.csproj
  ```
- From `src/`, build the solution in debug configuration:
  ```powershell
  dotnet build STranslate.slnx --configuration Debug
  ```
- From repo root, build a versioned release build:
  ```powershell
  ./build.ps1 <version>
  ```
  - This stamps `src/SolutionAssemblyInfo.cs`, temporarily replaces `src/STranslate/FodyWeavers.xml` with the release variant, builds `src/STranslate.slnx` in Release, then restores both files via `git restore`.

## Notes for agents
- `src/CLAUDE.md` is the primary operational and architectural documentation entry point; read it and the linked docs in `src/docs/` before making structural changes.
- Code style is defined in `src/.editorconfig`; follow its conventions (4-space indentation, CRLF line endings, block-scoped namespaces, no final newline).
- Build outputs are centralized in `src/.artifacts/Debug` or `src/.artifacts/Release` by `src/Directory.Build.props`.
- `FodyWeavers.xml` is configuration-dependent; do not commit release/debug-specific changes to it.
- Documentation and comments in source files are written in Simplified Chinese; code identifiers, interfaces, and filenames remain in English.
