# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

RetroFootballManager: a text-based football manager game (transfers, training, tactics, leagues,
cups, club management) built with .NET MAUI (Windows-only), C#, MVVM (CommunityToolkit.Mvvm).
Currently scoped to the 4 German leagues (18 teams each) + German Cup + two fictional European
competitions. See README.md for the full feature list and ROADMAP.md for planned-but-unbuilt work.

## Build & test

Solution file is `RetroFootballManager.slnx` (new XML slnx format, not `.sln`).

- Build game logic only: `dotnet build RetroFootballManager.Core/RetroFootballManager.Core.csproj`
- Run tests: `dotnet test RetroFootballManager.Tests/RetroFootballManager.Tests.csproj`
  - Single test: `dotnet test RetroFootballManager.Tests/RetroFootballManager.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"`
- Build the Windows app: `dotnet build RetroFootballManager/RetroFootballManager.csproj -f net10.0-windows10.0.19041.0`
- Exe lands at `RetroFootballManager/bin/Debug/net10.0-windows10.0.19041.0/win-x64/RetroFootballManager.exe`

Known-harmless build warnings: `NU1903` (SQLitePCLRaw advisory, no fix available) and
`MVVMTK0045` (suppressed via `NoWarn`; WinRT AOT hint, irrelevant for this non-AOT app).

Runtime data (unpackaged MAUI app) lives under `FileSystem.AppDataDirectory` →
`...\com.companyname.retrofootballmanager\Data\`: `retrofootball.db3` (SQLite save), `career.json`
(cross-save meta-progression), `logs/rfm-YYYYMMDD.log` (Serilog).

## Architecture

Three projects, strict dependency direction: `RetroFootballManager` (MAUI app) →
`RetroFootballManager.Core` (game logic, no MAUI dependency) ← `RetroFootballManager.Tests`.
Keep all simulation/domain logic in `Core` so it stays unit-testable without a MAUI host.

### RetroFootballManager.Core

- `Models/` — plain data classes persisted via sqlite-net-pcl. Many enums have a matching
  `*Display` companion (e.g. `PositionDisplay`, `PersonalityDisplay`, `RoleInTeamDisplay`) that
  maps the enum to the German UI string. **Never bind a raw enum to a Label/Span in XAML or
  interpolate it into a string directly** — always go through its `*Display` helper, or a
  ViewModel property that already wraps one. `PositionDisplay.Short(Position)` is the single
  canonical source for German position abbreviations (TW, IV, LV, RV, ZM, ST, …) — don't invent a
  second mapping anywhere.
- `Data/` — `AppDatabase` (sqlite-net connection) + one `Repository` per entity in
  `Data/Repositories/`. `SaveGameService` orchestrates new-game creation and save/load.
  Gotcha: sqlite-net `[AutoIncrement]` PKs ignore a pre-set `Id` on insert (the DB assigns it and
  writes it back) — code that needs child-entity FKs must insert parents first, then read back
  the assigned IDs before building dependents (see `SaveGameService.StartNewCareerAsync`).
- `Common/` — the actual game logic as one service class per concern (season progression, match
  simulation (`Match.cs`, `MatchDayService`), transfers, scouting, finances, staff, stadium,
  merchandise, cups, youth academy, AI opponents, etc.). This is by far the largest and most
  actively developed part of the codebase — when adding a feature, look for an existing service
  here first before creating a new one.
- `Logging/` — `ILog` + `LogManager.GetLogger<T>()` static-facade wrapper around Serilog. Use this
  (not `Serilog.Log` directly) from `Core` code.

### RetroFootballManager (MAUI app)

- `MauiProgram.cs` — DI composition root. Almost everything (`Core` services, repositories,
  ViewModels, Pages) is registered here; services are singletons, ViewModels/Pages are transient.
  Add new services/pages/viewmodels here when introducing them.
- `AppShell.xaml(.cs)` — Shell-based routing; `NavigationService` wraps `Shell.Current.GoToAsync`.
- `ViewModels/` — one per page, `CommunityToolkit.Mvvm` (`[ObservableProperty]`,
  `[RelayCommand]`), all extend `BaseViewModel`. `GameSession` (singleton) is the in-memory handle
  to the current save's `GameState` and loaded `Team`s — most ViewModels pull from it rather than
  re-querying the DB.
- `Views/` — one `.xaml` + `.xaml.cs` per page, registered as transient in `MauiProgram`.
- `Services/` — app-facing services that need MAUI (`INavigationService`, `IWindowService`,
  `AppSettingsService`, `CustomImageService`) plus `PositionDisplay`/`TrophyDisplay` UI mappings.
- `Platforms/Windows/` — this is the only supported platform target; `WindowService` there
  manages custom window chrome (see the Windows-crash gotchas below — this file has a documented,
  hard-won subclassed-WNDPROC workaround, don't casually touch it).

### Windows/WinUI crash gotchas (read before touching page layout or window chrome)

- **A `Grid` with more than one `*` (star) row crashes/hangs WinUI's native layout pass**
  (`0xC000027B`, unrecoverable native fastfail, bypasses managed exception handlers) — this
  happens regardless of `ScrollView` wrapping or row order. Rule: at most one `*` row per Grid;
  give other variable-length sections `MaximumHeightRequest` on an `Auto` row instead.
- **A large (~15-20+ item), per-item-complex `CollectionView` (image + several labels/buttons)**
  on Windows can trigger the same class of native crash by flooding the window with chrome
  negotiation messages (`WM_NCACTIVATE`, `WM_STYLECHANGING`, etc.) during virtualized layout.
  Fix pattern already used app-wide for squad/staff-style lists: `ScrollView` +
  `VerticalStackLayout` with `BindableLayout.ItemsSource`/`ItemTemplate` instead of
  `CollectionView`. Smaller/simpler lists (StaffPage, ScoutingPage, YouthPage, MatchDayPage) are
  fine with `CollectionView` as-is — this is a scale/complexity trigger, not a blanket ban.
- **Never call the full window chrome-setup routine (`WindowService.EnterFullScreen()`: border,
  title bar, resizable/minimizable toggles) more than once per window lifetime** — re-invoking it
  (e.g. on restore-from-minimize) retriggers `WM_STYLECHANGING` and crashes inside MAUI's own
  `SetTitleBarVisibility`. Only redo the specific corrective action needed (e.g. `Maximize()`).

## Language conventions

- Code comments, commit messages, and PR text: English, short, imperative (org policy). The
  existing codebase has a lot of German comments from before this policy — leave those as-is,
  don't mass-convert incidentally; only write new comments in English.
- German string literals that are in-game content (match commentary, UI status text, message
  bodies, club/city names) are intentional and stay in German — this is a German-language game.
  Only source comments and log messages follow the English-only rule.
