# Robustness, Updates and Branding: Design

Date: 2026-11-09
Owner: James Starling
Builds on: `2026-11-09-venue-registration-app-design.md` (v1, built on
branch `feature/v1-build`). That design still holds except where this one
changes it.

## Why

James wants v1 to become a production app that a few hundred Wicklow
Archers members could use every week. In his words: "It will only be
successful if it's a good experience. If it's a bit flaky, people won't
want to use it."

Members are mostly not technical. They will run the app on whatever Windows
PC they have, with no one to ask when something goes wrong. The app has to
install easily, stay up to date without effort, explain every failure in
plain words, and never register anyone wrongly.

The club committee does not know about the app yet. James will hand it to
them only once it is tested and, in his view, bug-free. If they are happy,
they will promote it. If not, he won't hand it out.

## Users and distribution

- **Users:** club members, potentially a few hundred. Mixed Windows 10 and
  11 PCs, any display scale, light or dark mode, some locked-down or
  work-managed machines, antivirus of every kind.
- **Phase 1 (now):** James tells members to download from the GitHub
  Releases page of `StableFlux/wicklow-archery-app`. The repo stays public;
  its README marks it as in development.
- **Phase 2 (later):** the Microsoft Store. James has a Windows developer
  account. The Store signs and updates the apps it distributes.

## Decided by James

- **The GitHub version updates itself automatically.** It installs with a
  one-click installer into the user's own profile (no admin rights). It
  checks for new releases, downloads them in the background, and runs the
  new version on next launch. Members do nothing. This replaces the v1
  anti-goal "needing an installer": the rule is now **no admin rights, and
  no setup steps beyond one click**.
- **The Store version** comes later. It is built from the same code, with
  the GitHub updater switched off.
- **App icon:** reproduce the actual Wicklow Archers logo as closely as
  possible. The logo is a blue square, white concentric target rings, and
  "Wicklow Archers" in a white serif font. Source: the logo image on
  https://wicklowarchery.org. James is happy to use it because he won't
  distribute the app without the club knowing, and he sees it as good
  branding for the club.
- **Repo stays public.** The logo-based icon may be committed to the
  public repo and shipped in installers straight away, before the committee
  has seen the app. (James, after the builder check.)
- **Testing:** everything must be tested before distribution, including
  end-to-end against the test form, 150% display scaling (done
  2026-11-09: all screens fine), and the closed-form case (measured
  2026-11-09, see below).

## Measured facts this design relies on (2026-11-09, test form)

- **Loading a closed form:** `viewform` returns 302 to `…/closedform`. The
  closed page returns 200, contains "no longer accepting responses", and
  has no `FB_PUBLIC_LOAD_DATA_`.
- **Submitting to a closed form:** `formResponse` returns 302 to
  `…/closedform`, and nothing is stored. Today the app reports this as
  "Unexpected reply from Google (HTTP 302)".
- **150% display scaling:** the main, People, Settings and confirm screens,
  and the minimum window size, all render correctly. The window reported
  DPI 144.
- **Accessibility gaps:** UI Automation shows the header People and
  Settings buttons with **no name**. Grid cells are named only by session
  text, not by person.

## Goals

### 1. Install and updates (GitHub phase)

- A per-user installer and auto-updater, using **Velopack** (open source,
  works with GitHub Releases). (Claude's call; James approved the
  behaviour, not the tool.)
- On start, the app checks GitHub Releases for a newer version in the
  background. It downloads the update quietly and applies it on the next
  launch. No prompt is needed. A small "Updated to version X: what's new"
  note shows once after an update.
- The update check never blocks or slows the weekly flow. Failures (offline,
  GitHub down) are silent and retried on the next launch.
- An update must never lose saved data. Data stays in
  `%APPDATA%\WicklowArcheryApp\`, outside the install folder.
- An update must never be applied while a send is in progress.
- **Releases are built and published by GitHub Actions** from a version
  tag: build, run the unit tests, package the installer and update files,
  and create the GitHub Release with notes. A human (James) creates the
  tag. Nothing is published without that.
- Versions follow `MAJOR.MINOR.PATCH`. The version is shown in the app (an
  About line in Settings).
- **Signing:** find out what code-signing options exist for an individual
  developer in Ireland, and their cost, and report back. Until then,
  releases are unsigned, and the download page explains the one-time
  Windows "More info → Run anyway" step with a screenshot. (Open: the
  research is Claude's job; the decision to pay is James's.)
- **Download size:** reduce it where possible, for example by compressing
  inside the package. Measure before and after.

### 2. Every way a week can go wrong: clear, safe handling

Each case gets a plain-English message and a safe outcome. The app never
records a submission as successful unless Google confirmed it.

| Situation | Required behaviour |
|---|---|
| Form closed when loading | Already handled ("The form isn't accepting responses right now."). Keep, with a test using the measured closed-page fixture. |
| Form closes between loading and sending | A POST reply of 302 to `/closedform` → that person fails with "The form closed before this could be sent." No success in history, ticks kept. |
| Club changes the form's questions | Already handled (Complete Form disabled with the reason). Add a "Check again" hint and a one-line explanation that an app update may be needed. |
| No internet or a slow connection | Already handled (Offline, Retry). Add an automatic retry when the connection returns (network-change event), with no user action. |
| App left open for days | The weekly form may have changed. Re-check the form when the window is activated if the last check was more than 1 hour ago. Never send against a form checked more than 1 hour earlier without re-checking first. (Claude's call on the 1 hour.) |
| A session that has already happened | Don't pre-tick a session whose day-of-month in its text is earlier than today (allowing for month wrap). Show it as "Past" but still let the user tick it by hand. If the date can't be read, behave as today. (Claude's call.) |
| Google rejects or errors | Already handled per person. Keep. |
| Timeout during a send | Already says it may have been recorded. Keep. |
| Corrupted or unreadable data file | Already moved aside. Add: keep a rolling last-good backup (`data.bak`) before each save, and offer to restore it when the main file is unreadable. |
| Disk full or write denied on save | Show the save-failed notice (exists). Never show "Saved". Keep the in-memory data so nothing is lost while the app stays open. |
| Second copy started | Already handled (single instance). Confirm the bring-to-front works on a real launch. |
| Windows sign-out or shutdown during a send | Measure what happens. If WPF can't delay it, accept this, but history must be consistent for every person whose result came back. |
| Unexpected crash | A global handler shows an in-app "Something went wrong" panel with **Copy report** (to the clipboard) and writes a local log. The app should keep running where it safely can. |

### 3. Different PCs

- **Windows 10 (22H2) and Windows 11:**
  - check that WPF's Fluent theme renders acceptably on Windows 10, and fall
    back to the classic theme if not;
  - verify on a Windows 10 machine or VM if one is available. Otherwise flag
    it as an untested risk in the release notes.
- **Display scaling:** 100% to 200% (150% verified).
- **Light, dark and High Contrast modes:** all readable. High Contrast must
  not hide ticks or badges.
- **Locked-down or work PCs:** the per-user install and per-user data need
  no admin rights. Behaviour behind a company proxy uses the system proxy
  (HttpClient's default). Untested.
- **Antivirus:** unsigned apps may be flagged. Document it in the download
  page's FAQ.

### 4. Accessibility and keyboard

- Every button has an accessible name: People, Settings, Refresh, Remove,
  Back, etc.
- Each grid cell's name includes the person and the session, for example
  "James Starling, Wednesday 11th, ticked".
- Full keyboard use: Tab order, arrow keys in the grid, Space to tick,
  Ctrl+Enter, Esc. Focus is always visible.
- Check with Windows Narrator, at least for the weekly flow.

### 5. People mistakes

- Trim and collapse whitespace in names and club numbers (exists for
  sending; also apply when saving).
- Warn, without blocking, about two people with the same name.
- **The same family registered from two PCs** can't be detected, because
  there's no shared history and cloud sync is a non-goal. State it plainly
  in the README FAQ. (Known limitation.)
- **Export and import of the people list** as a file, for moving to a new
  PC or sharing within a family. The export holds names and club numbers,
  so the app warns that it contains personal details. Unencrypted export
  is opt-in. (Claude's call; drop it if James prefers.)

### 6. Testing before every release

- **Automated end-to-end test** driving the real built app through UI
  Automation:
  - it runs against the test form only, with its own data folder (a
    test-only data-folder override, which never ships as a user setting);
  - it covers add people, tick, Complete Form, Submit, results, and
    Registered on relaunch.
- **The test form guard:** the end-to-end and Live tests must fail fast if
  the configured form is the club's.
- **Unit tests** (existing 190) run on every push via GitHub Actions on a
  Windows runner.
- **A written release checklist** in `docs/`: automated E2E pass, Live test
  pass, manual smoke test on the installed build (install, update from the
  previous version, uninstall), 150% check, and light/dark/High Contrast
  check.

### 7. Support

- **Settings → About:** version, "Check for updates", "Copy diagnostic
  report", and a link for reporting a problem.
- **The diagnostic report:** app version, Windows version, display scale,
  the last form check status, and recent log lines. It carries **no
  names, club numbers or form answers**, and the copy step says so.
- **Local logs** in `%LocalAppData%\WicklowArcheryApp\logs`:
  - rolling, with at most 5 files of 1 MB each;
  - no personal data;
  - nothing ever leaves the PC unless the user copies it.

### 8. Branding

- **The app icon** (`.ico`, with 16, 24, 32, 48, 64 and 256 px sizes) is
  reproduced from the club logo. The 16–32 px sizes may drop the lettering
  and keep the blue square and rings, because text isn't legible at that
  size (Claude's call). It is used for the window, taskbar, installer,
  Start menu and the future Store listing.
- **The header** may show a small version of the logo beside "Wicklow
  Archers". (Claude's call; check with James at the look check.)

## Non-goals

- Cloud sync or sharing history between PCs.
- Telemetry or analytics of any kind.
- Submitting the Microsoft Store listing in this round. Keep the code
  Store-ready: per-user data, no admin, and an updater that can be
  switched off.
- Mac version.
- Payment automation rules.

## Anti-goals (failure even if it works)

- Any change that could send a submission to the wrong form, or mark
  someone registered who wasn't.
- An update that loses or corrupts saved people or history.
- Personal data in logs, reports or crash output.
- An update check or install that needs admin rights or blocks the weekly
  flow.
- Publishing a release without James creating the tag.

## Settled after the builder check

The repo exists: `StableFlux/wicklow-archery-app`. `main` holds the docs.
v1 is on `feature/v1-build`, which is not pushed. Calls marked (Claude) are
Claude's; James can overrule any of them.

1. **Packaging (Claude).** Stay **self-contained**, so members never need
   to install .NET (that needs admin). The current single-file exe is
   134 MB (measured). Size target: the installer and the full update
   package are no larger than today, and smaller where compression allows.
   Velopack's delta updates keep later downloads small. Don't use WPF
   trimming.
2. **E2E isolation (Claude).** A `WICKLOW_DATA_DIR` environment variable
   redirects only the data folder. It is honoured in all builds, so the
   E2E test runs the real Release build. The form address still comes from
   the saved data, which the user can already set. The E2E test seeds its
   own data folder with `FormUrls.TestForm` and refuses to start if the
   seeded URL is the club form. No runtime switch can point the app at a
   form.
3. **Branch and versions (Claude).** This work continues on
   `feature/v1-build`. It merges to `main` only when everything here passes.
   - The first public release is **1.0.0**.
   - Tags are `vX.Y.Z`. Pre-release tags (`vX.Y.Z-rc.N`) build a GitHub
     pre-release, which the updater ignores.
   - The update feed is the public GitHub Releases of this repo, with no
     token. Its URL is set in one constant.
4. **CI scope (Claude).** GitHub Actions on `windows-latest` builds and
   runs the unit tests on every push and PR, and on tags it also packages
   and publishes. E2E and Live tests need a desktop session and Google, so
   they run locally and are gates in the written release checklist, not
   in CI.
5. **Updates (Claude).** No automatic "update available" prompt, ever.
   Background updates apply on the next launch. Settings → About →
   **Check for updates** downloads and then offers **Restart now**. That
   is offered only when no send is running and no overlay is open.
6. **What's new (Claude).** It comes from `CHANGELOG.md` in the repo. The
   release workflow puts that version's section into the package as its
   release notes. After an update the app shows it once, from the package,
   with no network call.
7. **Past sessions (Claude).**
   - For each session, take the day-of-month from its headline (for example
     "Monday 9th"). Build candidate dates in last, this and next month.
     Keep the one that falls on the named weekday and is nearest to today.
   - If none fits the weekday, or no day-of-month can be read, the session
     counts as not past.
   - A session is "Past" only if that date is before today's local date.
   - This only affects **pre-ticking** and a "Past" label. The user can
     still tick it, and nothing is ever blocked.
8. **Sign-out or shutdown mid-send (Claude).** Make it consistent:
   - Before each person's POST, write a "sending" marker for them to
     history, and save.
   - Replace it with the final result when it comes back.
   - On launch, any leftover "sending" marker becomes "May have been sent."
     That cell shows a caution badge, and the confirm panel flags it like a
     repeat. Nobody is silently re-sent or silently marked registered.
9. **Export and import (Claude).** Plain JSON, chosen as a file. A clear
   warning says it contains names and club numbers. No encrypted option.
   Import merges by name, asks before overwriting, and never imports
   history.

## Left to the builder

- Exact copy for the new messages, following the existing style: short,
  plain, no blame.
- Velopack integration details. The release workflow YAML.
- The logging library: a small built-in logger is fine.
- How the E2E test drives UI Automation (FlaUI or
  System.Windows.Automation).
- Icon production: vector redraw versus scaling the site image. It must
  look sharp at 256 px.
