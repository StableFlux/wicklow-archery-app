# Wicklow Archers Venue Registration App

A small Windows desktop app for filling in the Wicklow Archers weekly venue
registration Google Form for several people at once. It needs no browser and
no Google login.

> **Status: in development.** The design is approved and the build plan is
> being worked through. No release is available yet. See
> [Project status](#project-status).

## Why

Every archer who attends a Wicklow Archers session must fill in the club's
Google Form ("Wicklow Archers Venue Registration form 2026") each week, once
per person, ticking the sessions they will attend. If you register a family
or a group, that means filling in the same details several times every week.

This app saves each person's details once. Each week you open the app, check
the ticks, and press **Complete Form**. It sends one response per person and
shows you exactly what was submitted.

## What it does

- **People list.** Each person's full name, club, archery club number,
  payment method, optional loan bow, and usual training days are saved.
- **Clubs list.** One club is the default for new people.
- **Reads the live form.** When the app opens, it downloads the current form
  and shows this week's sessions. Each person's usual days are already ticked.
- **Form-change check.** Questions are matched by their titles. If the club
  adds, removes or changes a required question, the app shows what changed
  and won't submit until the form matches what it knows.
- **One click.** It sends one form response per ticked person, then lists
  who was registered for which session and whether each submission worked.
- **Duplicate guard.** It remembers what it has already submitted and won't
  silently register the same person for the same session twice.
- **Payment link.** If anyone registered as a non-member, a button opens the
  club's SumUp payment page in your browser.

## How it works

The club's form doesn't need a Google sign-in and doesn't collect email
addresses. A form response is an ordinary anonymous web request, the same
one your browser sends when you press Submit. The app:

1. downloads the form page and reads the form's structure, which Google
   embeds in the page;
2. finds each question by its title (not by Google's internal ids, which
   differ between copies of a form);
3. sends one response per person, using the option texts exactly as they
   appear on the form;
4. treats a submission as successful only when Google returns its
   confirmation page.

The full design is in
[docs/superpowers/specs/2026-11-09-venue-registration-app-design.md](docs/superpowers/specs/2026-11-09-venue-registration-app-design.md).

## Privacy and data

- Everything stays on your PC. There is no server, no account and no cloud
  sync.
- Saved details are kept in `%APPDATA%\WicklowArcheryApp\`, encrypted with
  Windows DPAPI for your Windows user account. Only the same Windows user on
  the same PC can read them.
- The app only talks to Google Forms (to read and submit the form). The
  SumUp payment page opens in your own browser.

## Requirements

- Windows 10 or 11 (x64).
- To use it: nothing else. It ships as a single self-contained `.exe` with
  no installer and no .NET install needed. The `.exe` is not code-signed
  yet, so Windows SmartScreen will ask once: choose **More info → Run
  anyway**.
- To build it: the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

## Building from source

```powershell
git clone https://github.com/StableFlux/wicklow-archery-app.git
cd wicklow-archery-app
dotnet build
dotnet test --filter "Category!=Live"
dotnet run --project src/WicklowArchery.App
```

To produce the single-file `.exe`:

```powershell
dotnet publish src/WicklowArchery.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Debug builds keep their data in `%APPDATA%\WicklowArcheryApp-Dev\` and
refuse to submit to the club's real form.

## Testing

- **Unit tests** run against saved copies of the club form and the test form
  in [docs/reference/form-snapshots/](docs/reference/form-snapshots/). They
  make no network calls:
  `dotnet test --filter "Category!=Live"`.
- **Live tests** submit a real response, but only ever to a separate test
  copy of the form, never the club's:
  `dotnet test --filter "Category=Live"`.

**Never submit test data to the club's real form.** The club's form feeds
the club's real attendance records. Use the test form for any testing (its
URL is in the design doc), and set it in the app's Settings while
experimenting. A "Test form" badge shows whenever the app isn't pointed at
the club's form.

## Project layout

```
src/WicklowArchery.Core/     Form parsing, form check, payloads, storage, submit service (no UI)
src/WicklowArchery.App/      WPF desktop app (Windows 11 Fluent theme)
tests/WicklowArchery.Tests/  Unit tests plus live tests against the test form
docs/superpowers/specs/      Design (source of truth for what to build)
docs/superpowers/plans/      Build plan, task by task
docs/reference/form-snapshots/  Saved form pages, the parser script, and submit findings
docs/session-notes/          Working notes from each development session
```

## Project status

| Stage | State |
|---|---|
| Design | Approved 2026-11-09 |
| Build plan | Written 2026-11-09 ([plan](docs/superpowers/plans/2026-11-09-venue-registration-app.md)) |
| Build | In progress |
| First release (`.exe`) | Not yet |

Planned later: a Mac version, publishing through the Microsoft Store (which
handles code signing), and optional rules such as "club member ⇒ free
payment".

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE) © 2026 James Starling
