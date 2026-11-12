# Wicklow Archery App

A Windows desktop app that fills in the Wicklow Archers weekly venue
registration Google Form for several people at once, with no browser and no
login.

## Where things are

- Design, the source of truth for what to build:
  `docs/superpowers/specs/2026-11-09-venue-registration-app-design.md`
- Build plan: `docs/superpowers/plans/` (written once the design is approved)
- Session notes and history: `docs/session-notes/`. Read the latest one first.
- Form snapshots (club form and test form) and the parser:
  `docs/reference/form-snapshots/`

## Status

- 2026-11-09: the design has been written and builder-checked, and the test
  form has been created.
- 2026-11-09 (later): James approved the design and added a look-and-feel
  section (Fluent theme, main-screen layout). Build plan written:
  `docs/superpowers/plans/2026-11-09-venue-registration-app.md` (15 tasks).
- 2026-11-09 (build): all 15 tasks built and reviewed, plus security
  hardening and a final review fix wave. 190 tests pass and the build has
  0 warnings. The work is on branch `feature/v1-build` (not merged, not
  pushed). Repo: https://github.com/StableFlux/wicklow-archery-app.
- 2026-11-09 (end of day): James approved a second design,
  `docs/superpowers/specs/2026-11-09-robustness-and-distribution-design.md`
  (robustness, auto-updates, club-logo icon, E2E automation, support).
- 2026-11-12: GitHub secret-scanning alert (9 "Google API Key") assessed
  as a false positive: they are Google's own page keys from the form
  snapshots. The keys were blanked in the fixtures and the alerts closed.
  Start-of-session rule added.
- **Next:** write the robustness design's build plan (superpowers:writing-plans), then build it
  on `feature/v1-build`. Merge, push and the first release (1.0.0) come
  only after everything passes, and each needs James's OK.

## Browser note

- When using Claude in Chrome, don't close the last tab in Claude's tab
  group. Chrome shut down each time that happened on 2026-11-09. This is
  inferred, not confirmed.

## Code layout

- `src/WicklowArchery.Core/`: form parsing and checking, payloads, the
  encrypted store, and the submit service. No UI.
- `src/WicklowArchery.App/`: the WPF app. Debug builds: `--sample` mode,
  the `WicklowArcheryApp-Dev` data folder, and a guard that blocks
  submitting to the club form.
- `tests/WicklowArchery.Tests/`: run with `dotnet test --filter "Category!=Live"`.
  `Category=Live` submits a real response to the test form.

## Rules for working here

- **Start of every session, before any other work:** get up to speed. Read
  the Status section above, the latest session note(s) in
  `docs/session-notes/`, the design(s) and plan relevant to the next step,
  and `git log` / `git status`. Then tell James in a few lines where the
  project stands and what's next.
- **Keep the records current during the session**, not only at the end:
  decisions James makes, what was measured, what's still open. The session
  note and the Status section must give the next session full context.
- **Never submit to the club's real form while testing.** Use the test form
  (its URLs are in the design doc).
- At the end of each session, add a note to `docs/session-notes/` and update
  the Status section above.
- `docs/email_temp/` holds emails James gives for context. Its contents are
  git-ignored. Delete each email once it's been digested.

## How James works

- One change at a time. Verify it before moving on.
- Don't state unverified things as fact. Keep what was measured separate from
  what was inferred, and when he pushes back, go and measure.
- Written updates should be short, factual, and free of padding.
