# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 10 console application intended to enforce Windows audio settings. Currently a fresh `dotnet new console` scaffold — `Program.cs` is still the Hello World template, so most of the implementation is yet to be written.

## Commands

- `dotnet build` — build the project
- `dotnet run` — build and run

There is no test project yet.

## Project Notes

- Single project, no solution file. Entry point is `Program.cs` (top-level statements).
- `RootNamespace` is `windows_audio_settings_enforcer` (the csproj file name uses hyphens, the namespace uses underscores).
- `Nullable` and `ImplicitUsings` are enabled — write null-safe code.
