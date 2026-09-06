# Security

## Reporting a vulnerability

Please report security problems privately through
[GitHub's advisory form](https://github.com/zedle/BlackScreens/security/advisories/new) rather than
in a public issue. I will confirm receipt, and if the report holds up, fix it and credit you in the
release notes unless you would rather I did not.

This is a small project maintained by one person, so please allow a little time for a reply.

## What BlackScreens does on your machine

- It draws black windows over monitors and reads the position, size and style of other windows to
  decide when to. It never reads window contents, sends input, or touches another process.
- Settings and an error log live in `%LocalAppData%\BlackScreens`. The log holds file paths and
  process names from your machine, so check it before pasting it into an issue.
- With the optional screensaver mode, it starts your chosen `.scr` in preview mode inside its own
  overlay window. That program is whatever Windows has installed, not something shipped here.
- It writes a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` only while
  "Start with Windows" is on, and removes it when you turn that off.
- It never needs administrator rights. The installer puts the app in your user profile.

## Network access

BlackScreens makes exactly one kind of request, and only when automatic updates are switched on,
which they are not by default:

- `GET https://api.github.com/repos/zedle/BlackScreens/releases/latest`, at most once a day
- a download of that release's asset from GitHub, when there is a newer version

The request sends nothing but a `BlackScreens/<version>` user agent. There is no telemetry, no
analytics, no crash reporting and no account. A downloaded update whose size does not match what the
release says is discarded instead of being installed.

## Verifying what you downloaded

Releases are built in public by the GitHub Actions workflow in this repository, from the tagged
commit, and never uploaded from a developer machine. You can check any build against its run in the
Actions tab.
