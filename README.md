# Wicklow Archers Venue Registration App

A small Windows desktop app for filling in the Wicklow Archers weekly venue
registration Google Form for several people at once. It needs no browser and
no Google login.

> **Status: build complete (2026-11-09), awaiting the owner's end-to-end
> check on the test form.** No release is published yet. See
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

- **People list.** Each person's full name, club, club number,
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

## Using the app

**First run.** Open **Settings** and check the clubs list (one club is the
default for new people). Open **People** and add each person: full name,
club, club number, payment method, optional loan bow, and usual
training days. Changes save automatically.

**Each week.**

1. Open the app. It downloads the club's form and shows this week's sessions.
   Each person's usual days are already ticked.
2. Check the ticks. Change any you need.
3. Press **Complete Form** and review what will be sent.
4. Press **Submit (N)**, where N is the number of people who will be sent.
   The results list shows each person with a tick or a failure.
5. If anyone paid as a non-member, press **Pay with SumUp**. It opens the
   club's payment page in your browser.

**Registered ✓ and Send again.** A cell showing **Registered ✓** means the
app already submitted that person for that session on this form. It is not
ticked for you next time. If you really want to register them a second time,
click the **Registered ✓** cell: it changes to **Send again**, and that
session will be sent again. Click it once more to change your mind.

**Test form badge.** A **Test form** badge shows whenever the app isn't
pointed at the club's form. Switch forms in **Settings**.

**If the form changes.** The app compares the form's questions with what it
knows. If a required question was added, removed or changed, **Complete
Form** is disabled and the reason is shown. If you are offline, the form
can't be read and nothing can be sent.

**Where your data lives.** In `%APPDATA%\WicklowArcheryApp\`, encrypted to
your Windows account. The app won't close while it is sending.

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

### End-to-end check (manual)

Run this on a Release build before publishing a release. It submits to the
test form only.

1. Open **Settings**, switch to the test form, and confirm the **Test form**
   badge is showing.
2. Add two people named `TEST E2E 1` and `TEST E2E 2`. Give one the payment
   `NON CLUB MEMBER PAYMENT` and a loan bow.
3. Use only the TEST people: clear every tick on real people's rows first,
   so no real person is sent, even to the test form.
4. Tick sessions for the TEST people, press **Complete Form**, then
   **Submit (N)**. Both rows show a tick. **Pay with SumUp** opens
   `https://wicklow-archers.sumup.link` in your default browser.
5. Open the test form's Responses tab. Both responses are there with the
   right sessions, payment and loan.
6. Close and reopen the app. Those cells show **Registered ✓** and are not
   pre-ticked.
7. Switch back to the club form in **Settings**. The badge disappears, and
   the TEST people's cells are not marked **Registered ✓** there (that
   history belongs to the test form). Do not submit.

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
| Build | Complete 2026-11-09; awaiting the owner's end-to-end check |
| First release (`.exe`) | Not yet published |

Planned later: a Mac version, publishing through the Microsoft Store (which
handles code signing), and optional rules such as "club member ⇒ free
payment".

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE) © 2026 James Starling
