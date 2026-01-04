# CLAUDE.md

This file provides guidance to AI-assisted coding tools (such as Claude Code)
when working with the SynQPanel codebase.

SynQPanel is a Windows desktop visualization application focused on clean,
panel-based presentation of system telemetry.

---

## Build Commands

```bash
# Restore packages
dotnet restore

# Build Debug
dotnet build SynQPanel.sln -c Debug

# Build Release
dotnet build SynQPanel.sln -c Release

# Run the main application
dotnet run --project SynQPanel/SynQPanel.csproj

# Publish for deployment (Windows x64)
dotnet publish SynQPanel/SynQPanel.csproj -c Release -r win-x64 --self-contained
