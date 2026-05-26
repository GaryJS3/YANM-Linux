# Agent Notes

## Project Shape
- Single .NET console app: `YANM-Linux.csproj` targets `net10.0`; solution file is `YANM-Linux.slnx`.
- Runtime entrypoint and app classes currently live in `Program.cs`; keep terminal UI concerns in `ConsoleUi` and network probing/command execution outside it.
- This is intended as a Linux network utility, but development may happen on Windows; Linux-only probes should fail safely with `unknown` values.

## Commands
- Build: `dotnet build "YANM-Linux.slnx"`.
- Run interactive UI: `dotnet run --`.
- Print help without UI: `dotnet run -- --help` or `dotnet run -- --no-ui`.
- List interfaces: `dotnet run -- list`.
- Show one interface: `dotnet run -- show <interface>`.

## Behavioral Constraints
- No third-party packages by default. If the console UI grows too complex, use Spectre.Console only behind `ConsoleUi` so core networking logic stays package-independent.
- No-argument execution must start the interactive UI; `--help`, `--no-ui`, and `help` must print help without starting UI.
- Connectivity-changing actions must show exact backend commands and require typing `yes`; when not root, print equivalent `sudo ...` commands and do not execute anything.

## Repo Hygiene
- There is no test project, formatter config, CI, README, or existing instruction file beyond this one.
- Ignore generated/local folders: `.vs/`, `bin/`, and `obj/`.
