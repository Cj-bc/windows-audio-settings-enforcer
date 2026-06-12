# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 10 per-user task-tray app (WinForms `NotifyIcon`) that enforces master audio volume and mute state. While running and enabled, it pins the default playback device's volume/mute to values from `appsettings.json` (`Audio:VolumePercent`, `Audio:Mute`) and reverts any external change (volume slider, mute toggle). Config edits apply live without restart. The tray menu toggles enforcement (in-memory, resets to enabled on restart), toggles mute, and opens a settings window (volume slider + mute) that writes back to `appsettings.json`.

## Commands

- `dotnet build` — build
- `dotnet run` — run for debugging (tray icon appears; exit via tray menu)
- `dotnet publish -c Release -r win-x64 --self-contained` — publish
- `.\install.ps1` — install to `%LocalAppData%\AudioSettingsEnforcer` with HKCU Run-key autostart (no elevation); `-Uninstall` to remove

There is no test project.

## Architecture

Single project, no solution file. Generic Host (`Program.cs`, `OutputType=WinExe`) with two hosted services: `AudioEnforcerService` (enforcement) and `TrayIconService` (UI). A named `Mutex` in `Program.cs` enforces single instance. Core Audio access goes through NAudio (`NAudio.Wasapi`).

`AudioEnforcerService.cs` holds all enforcement logic and has one critical invariant: NAudio's `OnVolumeNotification` and `IMMNotificationClient` callbacks fire on COM threads and must never set volume directly (setting volume re-triggers the notification → feedback loop / deadlock risk). Callbacks only enqueue signals into a `Channel<Signal>`; the `ExecuteAsync` loop consumes them and calls `ApplyIfNeeded()`, which compares before setting (epsilon 0.005 on the 0–1 scalar) so re-applying never loops. Default-device changes trigger detach/re-attach; a 5s watchdog signal covers missed notifications and no-device recovery.

UI ↔ enforcement decoupling: the tray never calls the enforcer directly.
- `EnforcementState.cs` — in-memory enabled flag (singleton); `ApplyIfNeeded()` early-returns when disabled, and its `Changed` event enqueues an Apply signal on re-enable.
- `SettingsWriter.cs` — writes volume/mute back to `appsettings.json` (JsonNode read-modify-write, preserves other sections); `IOptionsMonitor` live-reload then triggers re-apply. The config file is the single source of truth for volume/mute.

`TrayIconService.cs` runs WinForms on a dedicated STA thread (`Application.Run` with an `ApplicationContext`); shutdown posts `ExitThread` via the captured `WindowsFormsSynchronizationContext`. Tray Exit calls `IHostApplicationLifetime.StopApplication()`. `SettingsForm.cs` is the settings dialog (single instance, re-activated if already open).

## Project Notes

- `RootNamespace` is `WindowsAudioSettingsEnforcer` (csproj file name uses hyphens).
- `Nullable` and `ImplicitUsings` enabled.
- `UseWindowsForms` enabled; `System.Windows.Forms`/`System.Drawing` come from implicit usings.
