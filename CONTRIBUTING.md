# Contributing

Bug reports, ideas and pull requests are all welcome. BlackScreens is a small app with a narrow job,
so the bar for a change is simply that it makes that job better without making the app harder to
trust.

## Reporting a bug

Open an issue and fill in the form. The monitor layout and the game involved matter more than
anything else, because almost every detection bug is specific to one of the two.

Found a security problem? Please
[report it privately](https://github.com/zedle/BlackScreens/security/advisories/new) rather than
opening a public issue. See [`SECURITY.md`](SECURITY.md).

## Building it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows 10 1809 or
newer.

```
dotnet test
dotnet run --project src/BlackScreens
```

To build the release artifacts, and the installer if you have
[NSIS](https://nsis.sourceforge.io) installed:

```
pwsh scripts/publish.ps1
```

## How the code is laid out

- `src/BlackScreens/` is the tray app. `Program.cs` is the entry point and everything else sits in a
  folder named for what it does:
  - `Detection/` decides what should go black. `GameDetector` and the types around it are pure logic
    over a snapshot of windows, with no Win32 calls and no UI, which is what makes them testable.
    Keep them that way. `WindowEnumerator` is the one part that touches Win32, and only to gather
    the snapshot the rest works from.
  - `Monitors/` finds the screens. `MonitorHardware` reads the make and model out of each panel's
    EDID; the parsing is pure and tested, and only the lookup touches Win32 and the registry.
  - `Overlays/` puts the black windows on screens. `OverlayManager`, `OverlayForm` and `PlacedForm`.
  - `Configuration/` is the settings file, the blackout modes, the screensaver list and the startup
    registration.
  - `App/` is the tray app itself: the application context that owns the icon and the menu, single
    instance handling, and the crash and error logs.
  - `Interop/` is the P/Invoke surface, plus the two hidden windows built on it.
  - `Updates/` checks GitHub for a newer release and installs it.
  - `Ui/` is the WPF settings window and its view models, and `Themes/` holds the palettes and
    control styles.

  `GlobalUsings.cs` names those namespaces once, so files reaching across two or three folders do
  not each repeat the same block of usings. `Ui` is deliberately left out of it.
- `tests/BlackScreens.Tests/` is xUnit
- `installer/` is the NSIS script
- `docs/` is the website, served by GitHub Pages with no build step

## Conventions

- Warnings are errors in both projects. A pull request that builds with warnings will not merge.
- The detector stays pure. If a change needs the real screen state, gather it in the enumerators and
  pass it in.
- View models hold no WPF types, so they can be unit tested. Button handlers live in code behind.
- Anything that can be tested without a screen gets a test. Overlay placement and self replacement
  cannot be, and that is understood.
- User facing text is plain and lower key, and uses no em dashes.
- Settings changes need a default that behaves like the old build did, so an existing install does
  not change behaviour when someone updates.

## Things to know before changing certain areas

- **Overlays.** WinForms rescales a window when it moves to a monitor with a different DPI, which is
  why `PlacedForm` swallows `WM_DPICHANGED` and reapplies its own rectangle in raw pixels. Removing
  that turns a full screen overlay into a small square in the corner on mixed DPI desktops.
- **Screensaver mode.** The screensaver runs as a child window inside the overlay, so the overlay
  sets `WS_CLIPCHILDREN` and is deliberately not double buffered.
- **Updates.** The app must not touch the network unless the user turned updates on. Anything that
  changes that has to change the readme and the website too, because both make the claim.
- **The settings window.** The nav rail is a `ListBox` rather than a `TabControl`, because a
  retemplated `TabControl` stops exposing its selected page to accessibility tools.

## Releases

Maintainers only: pushing a `v1.2.3` tag builds and versions everything, then leaves a draft release
with the artifacts attached. Read the notes, edit them, and press Publish. Nothing in the repository
stores the version number.
