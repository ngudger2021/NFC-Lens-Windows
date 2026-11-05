# Repository Guidelines

## Project Structure & Module Organization

NFC Lens is a Windows Forms card inspector targeting .NET 10 (`net10.0-windows`). `NfcLens.csproj` defines the application with nullable reference types and implicit usings enabled.

Application code currently resides in `Program.cs`: `MainWindow` handles the interface and report export; `FieldHelp` supplies explanations; `Tlv` decodes card fields; `EmvScanner` implements the scan flow; `Pcsc` wraps Windows smartcard APIs; and `SelfTest` contains hardware-independent checks. There are no separate test or asset directories. Treat `bin/`, `obj/`, and `.vs/` as generated local output. The root `iex` script is a Codex installer, separate from application functionality.

## Build, Test, and Development Commands

Use Windows with the .NET 10 SDK installed. Run these commands from this directory:

- `dotnet build NfcLens.csproj` — restore dependencies and compile the application.
- `dotnet run --project NfcLens.csproj` — launch the desktop interface.
- `dotnet run --project NfcLens.csproj -- --self-test` — run simulated parser and scanner checks without a reader.
- `dotnet build NfcLens.csproj -c Release` — compile a release build.

Physical scans require a PC/SC reader, its driver, and the Windows Smart Card service. Use **Load demo** for interface checks without hardware.

## Coding Style & Naming Conventions

Follow the existing C# style: four-space indentation, opening braces on their own lines, a file-scoped namespace, PascalCase types and methods, and camelCase fields and locals. Preserve nullable annotations and deterministic disposal of native resources. Keep protocol logic separate from UI behavior, using the scanner's injected transport for simulation. No formatter or lint configuration is checked in.

## Testing Guidelines

Extend `SelfTest.Run()` with descriptive assertion labels and fictional hexadecimal fixtures. Cover malformed TLV input, PDOL construction, record addressing, and APDU response handling when changing those paths. The project uses custom assertions rather than a test framework and specifies no coverage threshold. Successful execution writes `self-test-result.txt` beside the built executable. Manually check demo rendering, reveal controls, and report export for UI changes.

## Commit & Pull Request Guidelines

Git history is unavailable in this checkout, so no established commit convention can be confirmed. Use concise, imperative subjects describing the change. Pull requests should explain behavior changes, link relevant issues, report validation commands and results, and include screenshots for UI changes.

## Sensitive Data

Preserve default masking in the interface and exports. Use fictional card data in fixtures, screenshots, and review materials; never commit real card responses or exported reports.