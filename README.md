# Codex Launch

A quiet, dark launch animation for the Codex desktop app on Windows.

**Bring ideas to life.**

The animation runs while the app starts, expands to its window, then fades into
the workspace. If the app is already open, the launcher activates it immediately.

## Features

- Live orbit animation, terminal mark and centered greeting.
- Handles variable startup times; press **Esc** or **Skip animation** at any time.
- Matches the app's current window position without moving or resizing the app.
- Keeps artwork and text steady during expansion.
- Supports minimize/restore and yields when you switch to another app.
- Respects Windows reduced-motion settings.
- Native C# / WPF; no browser runtime, installer or NuGet dependencies.

## Build and use

Requirements: Windows 10/11 x64, .NET Framework 4.8, Windows PowerShell 5.1, and
the installed Codex desktop app. The default package identity is
`OpenAI.Codex_2p2nqsd0c76g0!App`; edit `settings.json` if your installation differs.

Open Windows PowerShell in this repository and run:

```powershell
.\scripts\build.ps1
& '.\dist\Codex-Launch\Codex Launch.exe'
```

The build uses the C# compiler included with .NET Framework. It obtains the
shortcut icon from your locally installed app; OpenAI icon files are not
included in this repository.

To preview without opening the app:

```powershell
& '.\dist\Codex-Launch\Codex Launch.exe' --preview --seconds 30
```

To create a separate desktop shortcut:

```powershell
.\scripts\create-shortcut.ps1 -Desktop
```

This creates **Codex (Animated)** without replacing an existing shortcut.
Use that shortcut to launch with the animation. Keep the generated executable,
configuration and icon files together, and recreate the shortcut if you move them.

## Settings

Edit `dist/Codex-Launch/settings.json` after building. Useful options include
`ExpansionSeconds` (default `0.28`), `TransitionSeconds` (`1.05`),
`MinimumAnimationSeconds` (`2.5`), `MaxWaitSeconds` (`120`) and `ReducedMotion`.
The source `settings.json` supplies defaults for a new build.

## Development

```powershell
.\scripts\test.ps1
.\scripts\test-native.ps1
.\scripts\test-host.ps1
.\scripts\package.ps1 -SkipShortcut
```

Native and host checks briefly open disposable test windows. Build outputs,
local icons, shortcuts and runtime logs are ignored by Git.

Window readiness means a visible, responsive window with stable bounds; it does
not guarantee that the app has connected or finished loading its main panel.
Testing has primarily been on one Windows PC. Mixed-DPI monitor transitions
remain unverified.

## Credits and license

Developed with [OpenAI Codex](https://github.com/codex) as an AI coding co-author.
The launch-animation idea was inspired by
[dragon-codex-boot](https://github.com/yushua0808-cpu/dragon-codex-boot).

Source code is licensed under the [MIT License](LICENSE). OpenAI names and logos
belong to OpenAI and are not covered by this project's license. This is an
independent project, not affiliated with or endorsed by OpenAI.
