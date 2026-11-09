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
- **Next:** James runs the README end-to-end check with
  `publish/WicklowArchers.exe` (TEST people only), then decides on the
  merge, push and GitHub Release.

## Code layout

- `src/WicklowArchery.Core/`: form parsing and checking, payloads, the
  encrypted store, and the submit service. No UI.
- `src/WicklowArchery.App/`: the WPF app. Debug builds: `--sample` mode,
  the `WicklowArcheryApp-Dev` data folder, and a guard that blocks
  submitting to the club form.
- `tests/WicklowArchery.Tests/`: run with `dotnet test --filter "Category!=Live"`.
  `Category=Live` submits a real response to the test form.

## Rules for working here

- **Never submit to the club's real form while testing.** Use the test form
  (its URLs are in the design doc).
- At the end of each session, add a note to `docs/session-notes/` and update
  the Status section above.

## How James works

- One change at a time. Verify it before moving on.
- Don't state unverified things as fact. Keep what was measured separate from
  what was inferred, and when he pushes back, go and measure.
- Written updates should be short, factual, and free of padding.
