# Wicklow Archers Venue Registration App — Design

Date: 2026-11-09
Owner: James Starling

## Why

Every week, each archer who attends a Wicklow Archers session must fill in the
club's Google Form ("Wicklow Archers Venue Registration form 2026"), once per
person, ticking the sessions they'll attend. James does this for four people
every week. It's repetitive, and the details almost never change.

The app turns that into: open the app, check the ticks, press **Complete Form**,
read the result. No browser, no Google login.

It should also be usable by other club members, so nothing about James is
hard-coded.

## The form (as read on 2026-11-09)

URL: `https://docs.google.com/forms/d/e/1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw/viewform`

- Does **not** require Google sign-in and does **not** collect email addresses.
  The "Sign in to Google" link on the page is only for saving progress.
  Submission is an anonymous HTTP POST to `.../formResponse`.
- The page embeds its full structure as JSON in `FB_PUBLIC_LOAD_DATA_`
  (question titles, types, `entry.<id>` field ids, required flags, options).

| Question (title on form)              | Type       | Required | Notes |
|---------------------------------------|------------|----------|-------|
| Full Name:                            | short text | yes      | |
| Your Club name:                       | short text | yes      | |
| Archery Club Number - In filling in…  | short text | yes      | Probably the person's Archery Ireland number (James to confirm; doesn't change the build) |
| Venue options                         | checkboxes | yes      | **Changes weekly**, e.g. "Monday 9th - St Gerards Hall - Training - 7pm…", "Wednesday 11th - …", "Sunday 15th - Dargle Valley - …" |
| Select Payment Method                 | radio      | yes      | 4 options: WICKLOW ARCHERY CLUB MEMBER - FREE NO PAYMENT / NON CLUB MEMBER PAYMENT / NON SHOOTER (No Payment) / BEGINNERS COURSE ATTENDEE - Non Member (No Payment) |
| Payment link                          | image      | —        | SumUp link `https://wicklow-archers.sumup.link` in description |
| LOAN EQUIPMENT                        | radio      | no       | Left/Right handed adults/kids bow |

## Test form

A copy of the form lives in James's Google account (created 2026-11-09):

- Responder URL: `https://docs.google.com/forms/d/e/1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw/viewform`
- Editor: `https://docs.google.com/forms/d/1_Q0P8RGs863KmnnKEY7pY1kedRICC94kDwQ_Ty4MLRA/edit`
- Same titles, types, options and required flags as the real form; verified by
  parsing both. **Entry ids differ**, which confirms questions must be matched
  by title, not id.
- One difference: "Payment link:" is a text block (the real one is an image
  block). Both carry the SumUp URL in the description.
- Responses are checked through the editor's Responses tab (Claude via
  Chrome, or James by eye).
- When the club changes the real form's structure, the test form is updated to
  match (Claude's call: whoever is working on the app at the time).

## Goals

1. Keep a list of **people** with their saved details.
2. Keep a list of **clubs**; one is the default for new people.
3. On open, read the **live** form and show this week's sessions, with each
   person's usual days pre-ticked.
4. One click (**Complete Form**) submits one response per ticked person.
5. Show a clear result: who is registered for which session, and whether each
   submission succeeded.
6. Shareable: a single Windows `.exe` another member can copy and run.

## Non-goals (for now)

- Google login of any kind. Not needed by the form.
- Mac version (later).
- Automation rules such as "home-club member ⇒ free payment" (James wants to use
  it first and add automation later). In v1 the payment method is simply a
  saved per-person setting.
- Cloud sync / sharing a people list between PCs.
- Paying non-member fees inside the app.

## Anti-goals (would make it a failure even if it "works")

- Sending an incomplete or wrong submission to the club's real response sheet
  because the form changed and the app didn't notice.
- Accidentally submitting the same person twice for the same session.
- Sending test submissions to the club's real form.
- Needing a browser, a login, or an installer to use it.

## Decided by James

- Windows PC first; Mac later.
- No login.
- People list editable in the app (add / edit / remove).
- Separate clubs list; each person picks a club from a dropdown; there's a default club.
- Each person can have default ("usual") days so a normal week is one click.
- Must accommodate people with different needs: any of the four payment methods,
  and an optional loan bow.
- Testing goes against an **identical copy of the form in James's own Google
  account**, never the club's form.

## How it works

### Data (saved per PC)

- **Club**: name (exactly as it should be typed into "Your Club name"),
  is-default flag. First run seeds one club: "Wicklow Archery" (default).
- **Person**: full name, club (from clubs list), archery club number,
  payment method, loan bow (none or one of the form's options),
  usual days (any of Mon–Sun).
- **Settings**: form URL (defaults to the club form; changeable so the test form
  can be used).
- **Submission history**: person + session option text + timestamp + result,
  used for the duplicate check and for showing "already registered".

Stored under `%APPDATA%\WicklowArcheryApp\` (the `.exe` itself stays a single
file; its data lives beside the user's profile so it survives replacing the
`.exe` with a newer version). The data file is **encrypted with Windows DPAPI
(current-user scope)**, so it can only be read by the same Windows user on the
same PC, with no password to remember. Saved automatically on every change.

James confirmed:
- **Only one session per day**, so matching usual days by day name is enough.
- **The club keeps the same form** all season and edits the text and session
  dates each week. The form URL is a setting with the club form as default;
  no need to discover the URL from elsewhere.

### Weekly flow

1. App opens and fetches the form URL. Parses `FB_PUBLIC_LOAD_DATA_`.
2. **Form check.** Each known question is located by its title (matched
   loosely, e.g. contains "Full Name", "Club name", "Club Number", "Venue",
   "Payment", "LOAN"), not by fixed entry ids, so the test copy and the real
   form both work. If any known required question is missing, or the form has
   a required question the app doesn't know, the app shows what changed and
   **disables Complete Form**.
3. Main screen: a grid of people (rows) × this week's sessions (columns).
   A tick is pre-set where the session's text contains one of the person's
   usual day names (e.g. "Wednesday"). Ticks can be changed for this week only.
   Sessions the person is already recorded as registered for are marked.
4. **Complete Form**: shows a short confirmation summary (who → which
   sessions); on confirm, sends one POST per person who has at least one tick:
   name, club name, number, ticked session option texts, payment method,
   loan bow if set.
5. Result list per person: "✔ James Starling — Wednesday 11th St Gerards Hall,
   Sunday 15th Dargle Valley — submitted" or "✘ … — failed: <reason>".
   If anyone was submitted with "NON CLUB MEMBER PAYMENT", show the SumUp link
   (read from the form) as a button that opens it in the default web browser,
   so they can pay there.

### Duplicate guard

Before sending, if history shows that person already submitted for that exact
session text, the confirmation summary flags it and that session is left out
unless the user explicitly re-ticks/accepts it.

### Success detection

A submission counts as successful only if the POST returns HTTP 200 and the
response page is Google's confirmation page (not the form re-displayed with
errors). The exact marker is confirmed against the test form during build.

## Build choices (Claude's call)

- C# / .NET 9 (already installed), WPF desktop app.
- Published as a single self-contained `.exe` (no .NET install needed by
  other members).
- Form parsing, day matching, payload building and success detection are plain
  classes with unit tests using saved copies of the form HTML (real form +
  test form).
- End-to-end test: submit to James's test form copy and verify the response
  appears there.

- The shared `.exe` is unsigned for now; members see a one-time Windows
  SmartScreen "Run anyway" prompt. James is fine with this; his later plan is
  to publish it free through the Microsoft Store, which handles signing.
  (Keep the code Store-friendly: no admin rights, data only in the user's
  profile.)
- The SumUp link shown is the one in the form's "Payment link:" block. (The
  form's thank-you message has a different one, `wicklow-archers.sumupstore.com`;
  ignored.) (Claude's call.)

## Look and feel

James (2026-11-09): the app must look **slick, modern and clean**, and be
**intuitive and efficient** to use.

### Style

- Native Windows 11 look using WPF's built-in **Fluent theme** (ships with
  .NET 9: `PresentationFramework.Fluent.dll` is present in the installed
  runtime 9.0.18). It follows the user's Windows light/dark setting and accent
  colour. No third-party UI library unless the first UI build shows the theme
  falls short (fallback: WPF-UI).
- Plenty of whitespace, one clear primary button per screen, content grouped
  in cards with rounded corners, Segoe UI Variable text, Fluent icons
  (Segoe Fluent Icons font). No dense tables, no grey "forms" look.
- Scales cleanly with Windows display scaling; window has a sensible minimum
  size and remembers its size and position.

### Main screen (the weekly job)

The everyday task should take **one glance and one click**.

```
┌────────────────────────────────────────────────────────────────┐
│ Wicklow Archers                          ⟳  People  Settings   │
│ This week's form ✓ up to date · 3 sessions                     │
├────────────────────────────────────────────────────────────────┤
│                 Mon 9th          Wed 11th         Sun 15th     │
│                 St Gerards Hall  St Gerards Hall  Dargle Valley│
│                 Training 7pm     Training 7pm     Outdoor 10am │
│ James Starling     [✓]              [✓]              [ ]       │
│ Person 2           [ ]              [✓]              [✓]       │
│ Person 3           [ ]          Registered ✓         [ ]       │
├────────────────────────────────────────────────────────────────┤
│ 3 people · 5 registrations            [  Complete Form  ]      │
└────────────────────────────────────────────────────────────────┘
```

- **Header:** form status as a coloured pill: checking / up to date /
  "form changed: Complete Form disabled" (click for details) / offline with
  Retry.
- **Grid:** people as rows, this week's sessions as columns. Session headers
  show the day in bold and the rest of the option text below (wrapped, not
  cut off). Each cell is a large toggle. Usual days are pre-ticked. Sessions
  already submitted show a "Registered" badge instead of a toggle.
- **Footer:** a live count ("3 people · 5 registrations") and the primary
  **Complete Form** button. When disabled, the reason is shown next to it.
- **Keyboard:** Tab/arrow keys move between cells, Space toggles,
  Ctrl+Enter opens Complete Form.

### Confirm and result

- Complete Form opens an in-app panel (not a Windows message box) listing
  each person → their sessions, with duplicates flagged. Buttons: **Submit**
  / Cancel.
- During sending, each person's row shows progress; then ✔ or ✘ with a
  short reason. A **Pay with SumUp** button appears if anyone used
  "NON CLUB MEMBER PAYMENT".

### People and clubs

- **People** page: list on the left, edit card on the right; add/remove
  inline; changes save automatically (small "Saved" indicator). Payment
  method and loan bow are dropdowns of the form's own option texts; usual
  days are seven day chips.
- **Clubs** are managed in **Settings**, along with the form URL. A
  visible "Test form" badge appears in the header whenever the URL isn't
  the club's form, so test mode is never mistaken for real.
- **First run:** an empty state with a single "Add your first person" button
  rather than an empty grid.

### UI checkpoint

James reviews the main screen (running app, with sample data) before the
rest of the UI is built out.

## Left to the builder

- Exact colours, spacing and icon choices within the style above.
- Exact loose-matching rules for question titles.
- Error wording.
- Project structure and test framework.
