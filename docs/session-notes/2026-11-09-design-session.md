# Session notes — 2026-11-09 (design)

This session ran in a chat rooted at `C:\Users\james`, not in this folder. These
notes carry its context into sessions opened here.

## What was done

1. **Read the club's form** with an anonymous GET request. Nothing was
   submitted. Its full structure is in `FB_PUBLIC_LOAD_DATA_` on the page.
   - Measured: no sign-in is required and no email is collected. The
     "Sign in to Google" link only saves progress.
2. **Wrote the design**:
   `docs/superpowers/specs/2026-11-09-venue-registration-app-design.md`.
3. **Builder check.** A fresh reviewer read the design and raised 4 questions.
   All are answered and folded into the design:
   - only one session per day, so matching usual days by day name is enough;
   - the club keeps the same form URL all season;
   - an unsigned .exe is OK for now; the Microsoft Store is the later plan;
   - the payment link opens in the default browser.
4. **Created a test copy of the form** in James's Google account. It was
   published to "anyone with the link". It was verified by parsing it, and it
   matches the real form except for the entry ids. URLs are in the design doc.
5. **Saved snapshots** of both forms and the parser script in
   `docs/reference/form-snapshots/`. Use them as unit-test fixtures.

## Decisions made by James

- Windows first; Mac later.
- No Google login.
- People list, editable in the app.
- A separate clubs list, with a default club.
- Usual days per person, so a normal week is a single click.
- Support all four payment types and an optional loan bow.
- **Club-based automation is deferred** (for example, "Wicklow member ⇒ free
  payment"). In v1, payment is a saved per-person setting.
- Saved details must be stored securely. The design uses DPAPI-encrypted data
  in %APPDATA%.
- Test only against the test form.

## Open items / next steps

1. **James has not yet explicitly approved the design.** Get that approval
   first. Then write the build plan into `docs/superpowers/plans/` using
   superpowers:writing-plans.
2. Not confirmed: whether "Archery Club Number" is the person's Archery
   Ireland number. James thinks so. It doesn't change the build, because the
   field is labelled as on the form.
3. Not yet measured: what Google returns on a successful POST to
   `formResponse`, as opposed to a failed one. Confirm this against the
   **test form** during the build.
4. This folder is **not a git repository** yet. Ask James before running
   `git init`.
5. The Chrome extension (Claude in Chrome) needs site permission for
   docs.google.com before Claude can read the test form's Responses tab.
   Mid-session, Chrome reported "shutting down" on a tab close. The cause is
   unknown, and James didn't close it.
