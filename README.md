# BlackScreens 🖥️🌑

Windows tray app that blacks out the monitors you are not using while a fullscreen or
borderless game is running. The game keeps its monitor, the monitor you are working on stays
clear, and everything else goes dark so nothing pulls your eye mid fight.

**🌐 [blackscreens.app](https://blackscreens.app)** for screenshots and downloads.

- 🎯 Detects fullscreen and borderless windows, ignores the browser, chat app and shell processes
  on the denylist
- 🛡️ Keeps the game monitor, the focused monitor and any monitor you whitelist clear
- 🌙 Covered monitors go solid black, or run a Windows screensaver if you prefer
- ⏸️ Pause from the tray or with `Ctrl + Alt + B`
- 🎨 Themed settings window that follows Windows dark mode
- 🔄 Optional update check, off until you switch it on, and the only time it uses the network
- 🔒 No account, no telemetry, nothing about you ever leaves the machine

## 📥 Install

Download the latest release:

| File | What it is |
| --- | --- |
| `BlackScreens-<version>-setup.exe` | Installer. Goes into your user profile, no admin prompt |
| `BlackScreens-<version>-win-x64.exe` | The same app as one portable file. Nothing to install |
| `BlackScreens-<version>-win-x64-runtime.zip` | Much smaller, needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

Either way BlackScreens ends up in the tray. Turn on **Start with Windows** on the General page if
you want it back after a reboot. The installer leaves your settings behind when you uninstall unless
you say otherwise.

## 🖱️ Using it

Right click the tray icon:

- **Pause** shows a check mark while blackout is suspended. `Ctrl + Alt + B` does the same thing
- **Settings...** opens the settings window. Double clicking the tray icon does this too
- **Keep monitor clear** is a quick whitelist toggle per monitor
- **Quit** exits

Settings pages:

- **General** start with Windows, theme, pause hotkey, how often detection runs
- **Detection** whether background fullscreen counts, and whether the focused monitor is always kept clear
- **Blackout** solid black or a Windows screensaver on the covered monitors, with Configure and Test
- **Monitors** which monitors never go black, with an Identify button that flashes a number on each screen
- **Denylist** processes that never count as the game, with icons and a Browse button to pick a program
- **About** version, signature, updates, and the settings and log file locations

Blackout is put on hold while the settings window is open so it cannot cover what you are editing.

## 🌙 Screensavers on the covered monitors

Switch **Blackout** to "Windows screensaver" and the covered monitors run a screensaver instead of
showing black. The game monitor, the monitor you are working on, and any whitelisted monitor are
never touched. BlackScreens hosts the screensaver in preview mode inside its own overlay, the same
mechanism the Windows personalization dialog uses, so nothing takes over the desktop and no mouse
movement can dismiss it mid game. A busy screensaver does use the GPU, so a heavy one can cost you
frames. If the chosen screensaver refuses to run, the monitor simply stays black.

## 🔄 Updates

Updates are off by default. BlackScreens asks once, the first time it runs, whether you want
them; saying nothing or closing that window leaves them off. You can also turn
**Check for updates automatically** on or off at any time on the About page. When it is on,
BlackScreens asks the GitHub releases API for this repository once a day. If there is a newer
release it downloads the right file for how you installed it, an installed copy taking the
installer and a portable copy taking the self contained exe, and installs it when no monitor is
blacked out and the settings window is closed, so it will not restart on you mid game. **Check now**
on the same page does it on demand.

That request is the only network access in the app. It carries nothing but a `BlackScreens/<version>`
user agent, and a download that arrives the wrong size is thrown away rather than installed.

## 🔍 How detection works

Every poll, BlackScreens looks at the visible top level windows. A window counts as a game when it
is not cloaked, has no caption or is a popup, fills its monitor within two pixels, and its process
is not on the denylist. By default only the foreground window can trigger blackout, so a game you
alt tabbed away from leaves your desktop alone. Monitor rectangles are matched against
`EnumDisplayMonitors` before any overlay is shown, so a near miss can never black out every screen.

## 📁 Files

- Settings: `%LocalAppData%\BlackScreens\settings.json`
- Errors: `%LocalAppData%\BlackScreens\error.log`

Both are reachable from Settings > About.

## 🔨 Building

```
dotnet test
dotnet run --project src/BlackScreens
```

Release artifacts:

```
pwsh scripts/publish.ps1
```

That runs the tests, publishes both flavours, and builds the NSIS installer when `makensis` is
available. Requires the .NET 10 SDK, plus [NSIS](https://nsis.sourceforge.io) for the installer.

## 🚀 Cutting a release

```
git tag v1.2.3
git push origin v1.2.3
```

`.github/workflows/release.yml` takes the version from the tag, stamps it into the binaries and the
installer, runs the tests, builds all three artifacts and signs them when SignPath is configured.

It then leaves the release as a **draft** with the artifacts attached, so the notes can be read and
edited before anyone sees them. Press Publish and it goes live complete with its downloads. A draft
is invisible to the releases API, so the website and the updater in the app see nothing until then,
and a tag pushed by mistake only produces another draft.

Nothing in the repository stores the release version, so there is no file to bump. The `<Version>`
in the project file is only the fallback for local builds.

To preview the site against a real repository before it is published, serve `docs/` and add
`?repo=owner/name` to the URL.

See `AGENTS.MD` for the layout and conventions, and
[`docs/CODE-SIGNING.md`](docs/CODE-SIGNING.md) for how releases get signed.

The website is the [`docs/`](docs/) folder, served by GitHub Pages from `main` / `/docs` at
[blackscreens.app](https://blackscreens.app). It is plain HTML with no build step, and it reads the
latest release from the GitHub API, so the version, download links, file sizes and release notes
follow whatever was published last.

## 🤝 Contributing

Bug reports, ideas and pull requests are all welcome.

- **Something broken?** [Open a bug report](https://github.com/zedle/BlackScreens/issues/new?template=bug_report.yml).
  The monitor layout and the game involved matter more than anything else.
- **Want it to do something?** [Open a feature request](https://github.com/zedle/BlackScreens/issues/new?template=feature_request.yml).
- **Sending a patch?** [`CONTRIBUTING.md`](CONTRIBUTING.md) covers the layout, the conventions and
  the few places where the code looks odd on purpose.
- **Found a security problem?** Please [report it privately](https://github.com/zedle/BlackScreens/security/advisories/new)
  rather than in an issue. See [`SECURITY.md`](SECURITY.md).

Be decent to people while you are here: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).

## 🤖 Credits

Built with a lot of help from Claude. I decided what it should do and whether the result was any
good, the robot did most of the typing.

## 📄 License

MIT, see [`LICENSE`](LICENSE).
