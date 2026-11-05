# Windows x64 Development Toolchain

## Current requirements

The [current build instructions](../../README.md) supersedes the old PC inventory and exact Node/npm restrictions below. Current verified baseline: Windows x64 build 26200, stable .NET SDK **10.0.403**, Core/WindowsDesktop runtime **10.0.14**, net10.0/net10.0-windows, Node 24.16.0, npm 11.13.0, WebView2 156.0.4314.50. Standard Release build, complete regression suites and self-contained win-x64 publish pass on this PC. No design-time build exception is needed.

- .NET SDK selection pins **10.0.403** in `global.json`, with `rollForward: disable`. All nine project targets and NuGet lock graphs are migrated. WPF, x64, nullable and win-x64 settings are preserved; the SDK selects C# 14.0 without preview settings.
- Stable Node `>=24.16.0 <25` x64 and npm `>=11.13.0 <12` are accepted. The reference pair recorded in `packageManager` is npm 11.13.0 with Node 24.16.0. The previous 24.22.1/11.19.1 pair is also accepted. Other accepted patches are not individually execution-qualified.
- All 104 locked package Node-engine declarations accept Node 24.16.0. Exact patch equality was a provisioning policy, not a requirement demonstrated by the packages or regression suite. Major boundaries and a tested minimum remain enforced; prereleases and alternate managers are rejected.
- Dependency versions, URLs, integrity records and npm lockfile version 3 are unchanged. Always use `npm ci`; the permitted tool range guarantees neither byte-identical asset generation across runtimes nor acceptance without regression checks.
- MSBuild uses `call npm.cmd` from PATH. In CMD run `where node`, `where npm.cmd`, `node --version`, and `call npm.cmd --version`. Explicit absolute `NpmExecutable` or legacy `NpmCliPath` overrides are optional. No `%ProgramFiles%` layout is assumed. Restart the build shell after an intentional tool switch.
- .NET CLI and VS Code are the .NET 10 development paths; Visual Studio 2022 does not officially support .NET 10 targeting. Visual Studio 2026 is optional. The user installed the SDK; this agent did not change an IDE installation.

- [Step 41 security review](dependency-security-review.md) adds an evaluation-only Cryptography.Xml 8.0.5 override, resolving Pkcs 8.0.2 and 14 advisory findings. No production/npm package or target changed. Five ImageSharp advisories remain visible in the synthetic-only NPOI graph; do not run that experiment on external inputs. See the review for exact inventory and local versus unexecuted release acceptance.
