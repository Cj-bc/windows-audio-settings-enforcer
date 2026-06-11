# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 10 Windows service that enforces master audio volume and mute state. While running, it pins the default playback device's volume/mute to values from `appsettings.json` (`Audio:VolumePercent`, `Audio:Mute`) and reverts any external change (volume slider, mute toggle). Config edits apply live without restart.

## Commands

- `dotnet build` — build
- `dotnet run` — run in console mode for debugging (enforcement is active; Ctrl+C to stop)
- `dotnet publish -c Release -r win-x64` — publish for service installation

Service install (elevated): `sc.exe create AudioSettingsEnforcer binPath= "<publish>\windows-audio-settings-enforcer.exe" start= auto depend= Audiosrv`, then `sc start AudioSettingsEnforcer`. Remove with `sc stop` + `sc delete`.

There is no test project.

## Architecture

Single project, no solution file. Generic Host worker (`Program.cs`) with `AddWindowsService` so the same binary runs as console app or Windows service. Core Audio access goes through NAudio (`NAudio.Wasapi`).

`AudioEnforcerService.cs` holds all enforcement logic and has one critical invariant: NAudio's `OnVolumeNotification` and `IMMNotificationClient` callbacks fire on COM threads and must never set volume directly (setting volume re-triggers the notification → feedback loop / deadlock risk). Callbacks only enqueue signals into a `Channel<Signal>`; the `ExecuteAsync` loop consumes them and calls `ApplyIfNeeded()`, which compares before setting (epsilon 0.005 on the 0–1 scalar) so re-applying never loops. Default-device changes trigger detach/re-attach; a 5s watchdog signal covers missed notifications and no-device recovery.

Known risk: whether a session-0 LocalSystem service can control the logged-in user's endpoint volume is unverified. Fallback if it can't: run the same exe as a per-user logon task (Task Scheduler) instead of a service.

## Project Notes

- `RootNamespace` is `WindowsAudioSettingsEnforcer` (csproj file name uses hyphens).
- `Nullable` and `ImplicitUsings` enabled.
