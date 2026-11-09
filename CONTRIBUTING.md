# Contributing

Thanks for helping. The app is small, so the rules are short.

## Ground rules

1. **Never submit to the club's real form while testing.** Use the test form
   (URL in the
   [design doc](docs/superpowers/specs/2026-11-09-venue-registration-app-design.md)).
   Debug builds block submissions to the club's form. Release builds don't,
   so switch to the test form in Settings before trying anything.
2. **The design doc is the source of truth.** If a change alters behaviour,
   update the design first and say why.
3. **One change at a time.** Make each change small and test it before
   moving on.

## Workflow

- Branch from `main`. Open a pull request.
- Write the test first where you can. Unit tests use the saved form pages
  in `docs/reference/form-snapshots/` as fixtures and must not touch the
  network.
- Run `dotnet test --filter "Category!=Live"` before pushing. All tests
  must pass.
- Live tests (`[Trait("Category","Live")]`) may only target the test form.
  `dotnet test --filter "Category=Live"` submits a real response to the test
  form, so run it sparingly.

## When the club changes its form

The club edits the session texts every week. That's expected and needs no
code change. If it changes the *structure* of the form (adds, removes or
renames a question, or changes options), then:

1. Save a fresh snapshot of the form page to
   `docs/reference/form-snapshots/` (named `club-form-YYYY-MM-DD.html`).
   Use a plain GET; never submit.
2. Run `python parse_form.py <snapshot>` in that folder to see the
   structure.
3. Update the test form to match the real one.
4. Update the question-matching rules and tests if needed.

## Writing style

Docs and messages should be short, factual and plain. Keep what was measured
separate from what was inferred.
