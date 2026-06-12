# Changelog

## [1.0.0] — 2026-06-05

### Added
- Avalonia desktop GUI with 5 tabs (Dashboard, Build Config, Deploy Config, Push, Accounts)
- Spectre.Console CLI with 6 commands (build, deploy, push, init, profile, account)
- Unreal Engine build via UAT (RunUAT.bat/sh) using CliWrap
- SteamPipe deployment via steamcmd (app_build + depot_build VDF generation)
- Steam Guard interactive login (stdin-backed Process for steamcmd)
- Auto-discovery of Unreal Engine installations:
  - Registry lookup (GUID and version-string keys)
  - Epic Games Launcher manifest scanning
  - Filesystem scanning (C:/D:/E: drives, custom paths)
- Multi-account Steamworks credential management with encrypted storage (DPAPI on Windows, AES elsewhere)
- SSFN caching for passwordless subsequent logins
- Build profile management (platform, configuration, cook, clean build, extra UAT args)
- Deploy target management (AppID, depots, branches, file mappings, set-live)
- Push profiles linking build + deploy targets
- Real-time log streaming in both GUI and CLI
- Cancellable long-running operations (build/deploy)
- Progress reporting with stage tracking
- Copyable validation error messages
- File-based logging (Serilog rolling daily)
- Tooltips on all fields explaining Steamworks concepts
- Cross-platform support (Windows, macOS, Linux)

### Fixed
- Engine resolution for version-string associations (e.g. "5.7" not just GUIDs)
- Navigation button highlighting in sidebar
- Data loading on app startup (not requiring manual refresh)
