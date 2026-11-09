# Wicklow Archers Venue Registration App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A single-file Windows app that reads the club's weekly Google Form and submits one response per ticked person, with no browser and no login.

**Architecture:** There are two projects. `WicklowArchery.Core` (net9.0) holds plain classes covering form parsing, form checking, day matching, payload building, success detection, encrypted storage and the submit service, all unit-tested against saved form snapshots. `WicklowArchery.App` (net9.0-windows, WPF, MVVM) is the UI: thin ViewModels over Core, wired by hand in `App.xaml.cs`, with no DI container.

**Tech Stack:** C# / .NET 9 SDK 9.0.316, WPF with the built-in Fluent theme (`ThemeMode="System"`), CommunityToolkit.Mvvm 8.4.0, System.Security.Cryptography.ProtectedData 9.0.0 (DPAPI), System.Text.Json, xUnit (template default versions).

**Spec:** `docs/superpowers/specs/2026-11-09-venue-registration-app-design.md`

## Global Constraints

- **Never POST to the club's real form while testing.** Club form id: `1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw`. Test form id: `1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw`.
- Club form URL: `https://docs.google.com/forms/d/e/1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw/viewform`
- Test form URL: `https://docs.google.com/forms/d/e/1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw/viewform`
- Questions are matched by **title**, never by entry id (the two forms' ids differ).
- Option texts (sessions, payment, loan) are sent **exactly as read from the live form**.
- Data lives in `%APPDATA%\WicklowArcheryApp\`, encrypted with DPAPI (CurrentUser). No admin rights. No files beside the `.exe`.
- First run seeds one club: `Wicklow Archery`, default.
- Nothing about James is hard-coded (no names, numbers or people).
- Shipped as one self-contained, unsigned `win-x64` `.exe`.
- UI: Fluent theme, follows the Windows light/dark setting and accent colour; confirmations happen in-app, never in a `MessageBox`.
- Test projects run with `--filter "Category!=Live"` by default; `Live` tests only ever target the test form.
- Commit after each task. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **The club rewords a payment or loan option**, so a person's saved text is no longer on the form. That person is blocked in the confirm panel with a reason, and the others can still be sent. Covered by Task 8: `Build_PaymentNoLongerOnForm_BlocksOnlyThatPerson`.
2. **The network drops partway through a run.** That person is marked failed with a reason, the remaining people are still attempted, and only successful submissions go into history. Covered by Task 9: `Submit_NetworkErrorForOnePerson_ContinuesWithOthers`.
3. **Irish names with fadas or apostrophes** (`Seán Ó Briain`, `O'Brien`) arrive at Google exactly as typed. Covered by Task 9: `Encode_IrishNamesRoundTrip`.
4. **The data file can't be decrypted** (copied from another PC or user) or is corrupt. The app doesn't crash or silently overwrite it: it moves the file aside, starts fresh and says so. Covered by Task 7: `Load_UndecryptableFile_MovesAsideAndReturnsSeedWithNotice`.
5. **Submit is double-clicked or pressed twice.** Only one POST is sent per person. Covered by Task 12: `Submit_InvokedTwiceWhileRunning_PostsOncePerPerson`.

---

## File Structure

```
WicklowArcheryApp.sln
.gitignore
src/WicklowArchery.Core/                 net9.0 class library
  Forms/FormDefinition.cs                FormDefinition, FormQuestion, QuestionType
  Forms/FormParser.cs                    HTML → FormDefinition
  Forms/RegistrationForm.cs              RegistrationForm, FormCheck
  Forms/FormMapper.cs                    FormDefinition → FormCheck (title matching, problems)
  Forms/SessionText.cs                   day-of-week + headline/detail split
  Forms/PayloadBuilder.cs                PersonSubmission → POST fields
  Forms/SubmissionResultDetector.cs      POST reply → SubmissionOutcome
  Data/Models.cs                         Club, Person, HistoryEntry, AppSettings, WindowPlacement, AppData
  Data/FormUrls.cs                       club/test URLs, IsClubForm
  Data/IDataProtector.cs, DpapiProtector.cs
  Data/AppDataStore.cs                   load/save encrypted JSON
  Net/IFormClient.cs                     IFormClient, HttpPostResult
  Net/HttpFormClient.cs                  HttpClient implementation + Encode
  Net/ClubFormGuardClient.cs             blocks POSTs to the club form (debug builds)
  Registration/WeekPlanner.cs            pre-ticks + already-registered
  Registration/ConfirmationBuilder.cs    selections → ConfirmItems (blocked/repeat)
  Registration/RegistrationService.cs    load form, submit sequentially
src/WicklowArchery.App/                  net9.0-windows WPF
  App.xaml / App.xaml.cs                 Fluent theme, composition root
  MainWindow.xaml / .cs                  header, page host, footer, overlay host
  Theme/Styles.xaml                      Card, StatusPill, SessionToggle, PrimaryButton, DayChip styles
  ViewModels/MainViewModel.cs
  ViewModels/WeekViewModel.cs            + PersonRowViewModel, SessionCellViewModel, SessionHeaderViewModel
  ViewModels/ConfirmViewModel.cs
  ViewModels/ResultsViewModel.cs
  ViewModels/PeopleViewModel.cs          + PersonEditorViewModel
  ViewModels/SettingsViewModel.cs
  Views/WeekView.xaml, ConfirmPanel.xaml, ResultsPanel.xaml, PeopleView.xaml, SettingsView.xaml
  Services/IUrlOpener.cs                 + ShellUrlOpener
  Services/SampleData.cs                 #if DEBUG only (`--sample`)
tests/WicklowArchery.Tests/              net9.0-windows xUnit, references both projects
  Fixtures.cs                            reads linked snapshot files
  Fakes/FakeFormClient.cs, Fakes/PassThroughProtector.cs
  Core/*Tests.cs, App/*Tests.cs, Live/LiveSubmitTests.cs
docs/reference/form-snapshots/           existing snapshots + Task 2 outputs (single source for fixtures)
```

---

### Task 1: Repository and solution skeleton with Fluent window

**Files:**
- Create: `.gitignore`, `WicklowArcheryApp.sln`, `src/WicklowArchery.Core/WicklowArchery.Core.csproj`, `src/WicklowArchery.App/*` (template), `tests/WicklowArchery.Tests/WicklowArchery.Tests.csproj`, `tests/WicklowArchery.Tests/Fixtures.cs`, `tests/WicklowArchery.Tests/Core/FixturesTests.cs`

**Interfaces:**
- Produces: `static class Fixtures { static string Read(string fileName); }` reads `Fixtures/<fileName>` from the test output directory.

- [ ] **Step 1: `git init`** (James approved this at plan review). Add `.gitignore` containing `bin/`, `obj/`, `.vs/`, `*.user`, `publish/`, `TestResults/`.

- [ ] **Step 2: Create the projects**

```bash
dotnet new sln -n WicklowArcheryApp
dotnet new classlib -n WicklowArchery.Core -o src/WicklowArchery.Core -f net9.0
dotnet new wpf -n WicklowArchery.App -o src/WicklowArchery.App -f net9.0
dotnet new xunit -n WicklowArchery.Tests -o tests/WicklowArchery.Tests -f net9.0
dotnet sln add src/WicklowArchery.Core src/WicklowArchery.App tests/WicklowArchery.Tests
dotnet add src/WicklowArchery.App reference src/WicklowArchery.Core
dotnet add tests/WicklowArchery.Tests reference src/WicklowArchery.Core src/WicklowArchery.App
dotnet add src/WicklowArchery.Core package System.Security.Cryptography.ProtectedData --version 9.0.0
dotnet add src/WicklowArchery.App package CommunityToolkit.Mvvm --version 8.4.0
```
Delete the template `Class1.cs` and `UnitTest1.cs`. In the test csproj, set `<TargetFramework>net9.0-windows</TargetFramework>` and `<UseWPF>true</UseWPF>`, and link the snapshots:
```xml
<None Include="..\..\docs\reference\form-snapshots\*.html" LinkBase="Fixtures" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 3: Fluent theme.** In `App.csproj` add `<NoWarn>$(NoWarn);WPF0001</NoWarn>` (ThemeMode is experimental in .NET 9). In `App.xaml` set `ThemeMode="System"` on `<Application>`. Set the `MainWindow` title to `Wicklow Archers`, `MinWidth="720" MinHeight="480"`, and add a placeholder `TextBlock` "Wicklow Archers". If `ThemeMode` won't compile, merge `pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml` into `Application.Resources` instead, and note this in the commit.

- [ ] **Step 4: Write the smoke test** `FixturesTests.Snapshots_AreCopied`: `Assert.Contains("FB_PUBLIC_LOAD_DATA_", Fixtures.Read("club-form-2026-11-09.html"))`, and the same for `test-form-2026-11-09.html`.

- [ ] **Step 5: Verify**
Run: `dotnet build` then `dotnet test --filter "Category!=Live"`
Expected: build succeeds with 0 errors, and 1 test passes.
Run: `dotnet run --project src/WicklowArchery.App`
Expected: a window titled "Wicklow Archers" with Fluent (Windows 11) styling. Switch Windows to dark mode and relaunch: the window is dark. Take a screenshot as evidence.

- [ ] **Step 6: Commit** `chore: solution skeleton with Fluent-themed WPF shell`

---

### Task 2: Measure what Google returns on submit (test form only)

No code. This measures the open question from the design ("what Google returns on a successful POST vs a failed one") and saves fixtures for Task 6.

**Files:**
- Create: `docs/reference/form-snapshots/test-form-response-ok.html`, `docs/reference/form-snapshots/test-form-response-error.html`, `docs/reference/form-snapshots/submit-findings.md`

Test-form entry ids (from `parse_form.py`): name `entry.353097525`, club `entry.859175428`, number `entry.168390220`, venue `entry.2011193388`, payment `entry.1751963076`, loan `entry.1412580528`.

- [ ] **Step 1: Guard.** Check that every URL used below contains `1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw`. Stop if one doesn't.

- [ ] **Step 2: Fetch a fresh copy of the test form**, run `parse_form.py` on it, and take the current venue option texts and the hidden `fbzx` value from it.

- [ ] **Step 3: POST A, full request** (hidden fields `fvv=1`, `pageHistory=0`, `fbzx`, `partialResponse`, `submissionTimestamp=-1`, plus all entries and `entry.<id>_sentinel=` for venue, payment and loan). Use name `TEST spike A`, club `Wicklow Archery`, number `TEST-0000`, one venue option and payment `WICKLOW ARCHERY CLUB MEMBER - FREE NO PAYMENT`. Use `curl -s -o test-form-response-ok.html -w "%{http_code}" --data-urlencode ... <test>/formResponse`. Record the HTTP status.

- [ ] **Step 4: POST B, entries only** (no hidden fields, no sentinels, name `TEST spike B`). Record the status.

- [ ] **Step 5: POST C, missing Full Name** (otherwise like A). Save the body as `test-form-response-error.html`. Record the status.

- [ ] **Step 6: Check the Responses tab** of the test form (editor URL in the design doc; James by eye or Claude via Chrome). Record which of A, B and C appear. Expected: A appears and C does not. B is the open question.

- [ ] **Step 7: Find markers.** Compare the ok and error bodies and record in `submit-findings.md`: the status codes, a string present only in the ok body (the success marker), and how the error body differs (for example, whether it contains `FB_PUBLIC_LOAD_DATA_`). Also record whether POST B was accepted. **Decision rule:** Task 6's payload includes the hidden fields and sentinels unless POST A failed.

- [ ] **Step 8: Commit** `docs: measured Google Forms submit responses on test form`

---

### Task 3: Form parser

**Files:**
- Create: `src/WicklowArchery.Core/Forms/FormDefinition.cs`, `src/WicklowArchery.Core/Forms/FormParser.cs`
- Test: `tests/WicklowArchery.Tests/Core/FormParserTests.cs`

**Interfaces:**
- Produces:
```csharp
public enum QuestionType { Unknown = -1, ShortText = 0, Paragraph = 1, Radio = 2, Dropdown = 3, Checkbox = 4,
  Scale = 5, TextBlock = 6, Grid = 7, Section = 8, Date = 9, Time = 10, Image = 11, Video = 12, FileUpload = 13 }
public sealed record FormQuestion(long ItemId, string Title, string? Description, QuestionType Type,
  long? EntryId, bool Required, IReadOnlyList<string> Options);
public sealed record FormDefinition(string Title, Uri ResponseUrl, IReadOnlyList<FormQuestion> Questions,
  IReadOnlyList<KeyValuePair<string, string>> HiddenFields);
public static class FormParser { public static FormDefinition Parse(string html); }
public sealed class FormParseException(string message) : Exception(message);
```

- [ ] **Step 1: Write the failing tests**
  - `Parse_ClubSnapshot_ReadsTitleAndSevenQuestions`: `Title == "Wicklow Archers Venue Registration form 2026"`, `Questions.Count == 7`, `ResponseUrl == https://docs.google.com/forms/d/e/1FAIpQLSd9iDEkYbY5u7s3Eg0oDgHhk5RTTkPLQpri3-r6eR6_bYCiDw/formResponse`.
  - `Parse_ClubSnapshot_ReadsVenueQuestion`: the question whose title starts "Venue options" has `Type == Checkbox`, `EntryId == 1741681513`, `Required == true`, and `Options` equal to the three exact strings `"Monday 9th - St Gerards Hall - Training - 7pm - Two senior members must be in attendance"`, `"Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm"` and `"Sunday 15th - Dargle Valley - Gates open 9.30am."`.
  - `Parse_ClubSnapshot_LoanIsOptionalRadio`: the LOAN question has `Type == Radio`, `Required == false` and 4 options.
  - `Parse_ClubSnapshot_PaymentLinkIsImageWithUrl`: `Type == Image`, `Description == "https://wicklow-archers.sumup.link"`, `EntryId == null`.
  - `Parse_TestSnapshot_HasOwnEntryIds`: Full Name `EntryId == 353097525`; Payment link `Type == TextBlock`.
  - `Parse_ClubSnapshot_ReadsHiddenFields`: contains `("fbzx","8688713072168764879")`, `("fvv","1")`, `("pageHistory","0")`, and `("partialResponse","[null,null,\"8688713072168764879\"]")` (HTML entities decoded). There are no `_sentinel` keys.
  - `Parse_PageWithoutLoadData_Throws`: `Assert.Throws<FormParseException>(() => FormParser.Parse("<html></html>"))`.

- [ ] **Step 2: Run** `dotnet test --filter FormParserTests`. Expected: FAIL (types not defined).

- [ ] **Step 3: Implement** `FormParser.Parse`. Extract the JSON with the regex `FB_PUBLIC_LOAD_DATA_ = (.*?);</script>` (Singleline) and walk it with `JsonDocument`, using the same indexes as `parse_form.py`: `d[1][8]` is the title, `d[1][1]` the items, and for each item `q[0]` id, `q[1]` title, `q[2]` description, `q[3]` type, `q[4][0]` entry (`e[0]` id, `e[1][*][0]` options, `e[2]==1` required). An unknown type number maps to `Unknown`. Take ResponseUrl from the `action="...formResponse"` attribute. Hidden fields are the `<input type="hidden" name="..." value="...">` tags whose names don't end in `_sentinel`, decoded with `WebUtility.HtmlDecode`. Any structural mismatch throws `FormParseException`.

- [ ] **Step 4: Run** `dotnet test --filter FormParserTests`. Expected: PASS.

- [ ] **Step 5: Commit** `feat(core): parse Google Form structure from page HTML`

---

### Task 4: Form check (map questions by title)

**Files:**
- Create: `src/WicklowArchery.Core/Forms/RegistrationForm.cs`, `src/WicklowArchery.Core/Forms/FormMapper.cs`
- Test: `tests/WicklowArchery.Tests/Core/FormMapperTests.cs`

**Interfaces:**
- Consumes: `FormDefinition`, `FormQuestion`, `QuestionType` (Task 3).
- Produces:
```csharp
public sealed record RegistrationForm(Uri ResponseUrl,
  long FullNameEntry, long ClubNameEntry, long ClubNumberEntry, long VenueEntry, long PaymentEntry, long? LoanEntry,
  IReadOnlyList<string> Sessions, IReadOnlyList<string> PaymentOptions, IReadOnlyList<string> LoanOptions,
  Uri? SumUpUrl, IReadOnlyList<KeyValuePair<string, string>> HiddenFields);
public sealed record FormCheck(RegistrationForm? Form, IReadOnlyList<string> Problems)
{ public bool IsUsable => Form is not null && Problems.Count == 0; }
public static class FormMapper { public static FormCheck Map(FormDefinition definition); }
```

Matching uses the normalised title (trimmed, lower-case invariant, whitespace collapsed):

| Key | Title contains | Required type | Label in messages |
|---|---|---|---|
| FullName | `full name` | ShortText | `Full Name` |
| ClubName | `club name` | ShortText | `Club name` |
| ClubNumber | `club number` | ShortText | `Club Number` |
| Venue | `venue` | Checkbox | `Venue options` |
| Payment | `payment method` | Radio | `Payment Method` |
| Loan (optional) | `loan` | Radio | `LOAN EQUIPMENT` |
| SumUp (optional) | `payment link` | any; first `https://` URL in Description | — |

Problem copy (exact):
- missing: `Can't find the "{label}" question on the form.`
- wrong type: `The "{label}" question has changed type.`
- two matches: `More than one question matches "{label}".`
- required question not in the table: `The form has a new required question: "{title}".` (title trimmed)
- loan required: `"LOAN EQUIPMENT" is now required.`

- [ ] **Step 1: Write the failing tests**
  - `Map_ClubSnapshot_IsUsable`: `IsUsable`, `Sessions.Count == 3`, `PaymentOptions.Count == 4`, `LoanOptions.Count == 4`, `SumUpUrl!.Host == "wicklow-archers.sumup.link"`, `VenueEntry == 1741681513`.
  - `Map_TestSnapshot_IsUsable`: `IsUsable`, `VenueEntry == 2011193388`, `SumUpUrl` not null (it comes from a TextBlock).
  - `Map_FullNameRemoved_ReportsMissing`: remove the question from the parsed club definition (`definition with { Questions = ... }`). Expect `!IsUsable` and Problems containing `Can't find the "Full Name" question on the form.`
  - `Map_NewRequiredQuestion_Reported`: add `new FormQuestion(1, "Date of birth", null, QuestionType.ShortText, 99, true, [])`. Expect the Problem `The form has a new required question: "Date of birth".`
  - `Map_NewOptionalQuestion_Ignored`: the same with `Required: false`. Expect `IsUsable`.
  - `Map_VenueBecameRadio_ReportsChangedType`.
  - `Map_LoanBecameRequired_Reported`.
  - `Map_NoPaymentLink_StillUsableWithNullSumUp`.

- [ ] **Step 2: Run** `dotnet test --filter FormMapperTests`. Expected: FAIL.
- [ ] **Step 3: Implement** `FormMapper.Map` following the table. Check `payment link` before `payment method` so the link block is never taken as the payment question. `Form` is null whenever a required key is missing or has the wrong type.
- [ ] **Step 4: Run** `dotnet test --filter FormMapperTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): check form by question titles and report changes`

---

### Task 5: Session text (day matching and header split)

**Files:**
- Create: `src/WicklowArchery.Core/Forms/SessionText.cs`
- Test: `tests/WicklowArchery.Tests/Core/SessionTextTests.cs`

**Interfaces:**
- Produces: `public static class SessionText { public static DayOfWeek? DayOf(string sessionText); public static (string Headline, string Detail) Split(string sessionText); }`

- [ ] **Step 1: Write the failing tests** (`[Theory]` where natural)
  - `DayOf`: the three club options give Monday, Wednesday and Sunday. `"Wed 11th - Hall"` gives Wednesday. `"Thurs 12th"` gives Thursday. `"Sunday's league"` gives Sunday. `"Monday or Tuesday"` gives Monday (the first wins). `"Club BBQ at the hall"` gives null. `"Wedding fair"` gives null.
  - `Split("Monday 9th - St Gerards Hall - Training - 7pm - Two senior members must be in attendance")` gives `("Monday 9th", "St Gerards Hall - Training - 7pm - Two senior members must be in attendance")`.
  - `Split("Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm").Headline == "Wednesday 11th"`.
  - `Split("Club BBQ") == ("Club BBQ", "")`.
- [ ] **Step 2: Run** `dotnet test --filter SessionTextTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** `DayOf` takes the first match of `\b(mon(day)?|tue(s(day)?)?|wed(nesday)?|thu(r(s(day)?)?)?|fri(day)?|sat(urday)?|sun(day)?)\b` (IgnoreCase). `Split` splits at the first `" - "`, trimming both parts.
- [ ] **Step 4: Run** `dotnet test --filter SessionTextTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): day-of-week matching and session header split`

---

### Task 6: Payload builder and success detection

**Files:**
- Create: `src/WicklowArchery.Core/Forms/PayloadBuilder.cs`, `src/WicklowArchery.Core/Forms/SubmissionResultDetector.cs`
- Test: `tests/WicklowArchery.Tests/Core/PayloadBuilderTests.cs`, `tests/WicklowArchery.Tests/Core/SubmissionResultDetectorTests.cs`

**Interfaces:**
- Consumes: `RegistrationForm` (Task 4); fixtures `test-form-response-ok.html` and `test-form-response-error.html` and the markers in `submit-findings.md` (Task 2).
- Produces:
```csharp
public sealed record PersonSubmission(Guid PersonId, string FullName, string ClubName, string ClubNumber,
  IReadOnlyList<string> Sessions, string PaymentMethod, string? LoanBow);
public static class PayloadBuilder
{ public static IReadOnlyList<KeyValuePair<string, string>> Build(RegistrationForm form, PersonSubmission submission); }
public enum SubmissionOutcome { Success, Rejected, Unexpected }
public static class SubmissionResultDetector { public static SubmissionOutcome Classify(int statusCode, string body); }
```

- [ ] **Step 1: Write the failing tests.** Build the form with `FormMapper.Map(FormParser.Parse(Fixtures.Read("test-form-2026-11-09.html"))).Form!`.
  - `Build_IncludesEachEntryOnce`: `entry.353097525=James Test`, `entry.859175428=Wicklow Archery`, `entry.168390220=TEST-1`, `entry.1751963076=<payment>`.
  - `Build_OneVenuePairPerSession`: two sessions give two `entry.2011193388` pairs, in tick order.
  - `Build_TrimsNameClubAndNumber`: `"  James  "` gives `"James"`.
  - `Build_NoLoan_OmitsLoanValue`: no `entry.1412580528` pair with a non-empty value.
  - `Build_WithLoan_IncludesLoan`.
  - `Build_IncludesHiddenFieldsAndSentinels`: contains the `fbzx` pair from the fixture, plus `entry.2011193388_sentinel` and `entry.1751963076_sentinel` with value `""`. (Drop this test if Task 2's decision rule removed them.)
  - `Classify_OkFixture_IsSuccess`: `Classify(<status from submit-findings.md>, Fixtures.Read("test-form-response-ok.html")) == Success`.
  - `Classify_ErrorFixture_IsRejected`.
  - `Classify_FormPageReturned_IsNotSuccess`: `Classify(200, Fixtures.Read("test-form-2026-11-09.html")) != Success`.
  - `Classify_ServerError_IsUnexpected`: `(500, "")`. `Classify_EmptyOk_IsUnexpected`: `(200, "")`.
- [ ] **Step 2: Run** `dotnet test --filter "PayloadBuilderTests|SubmissionResultDetectorTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** The order is hidden fields, then name, club, number, venue pairs, venue sentinel, payment, payment sentinel, loan (if set), loan sentinel (if `LoanEntry` is set). `Classify` returns `Success` only for 200 with the success marker recorded in `submit-findings.md`; `Rejected` when the body matches the recorded error signature; otherwise `Unexpected`.
- [ ] **Step 4: Run** the same filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): build submission payload and classify Google's reply`

---

### Task 7: Saved data and encrypted store

**Files:**
- Create: `src/WicklowArchery.Core/Data/Models.cs`, `FormUrls.cs`, `IDataProtector.cs`, `DpapiProtector.cs`, `AppDataStore.cs`; `tests/WicklowArchery.Tests/Fakes/PassThroughProtector.cs`
- Test: `tests/WicklowArchery.Tests/Core/AppDataStoreTests.cs`

**Interfaces:**
- Produces:
```csharp
public sealed class Club { public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = ""; public bool IsDefault { get; set; } }
public sealed class Person { public Guid Id { get; set; } = Guid.NewGuid(); public string FullName { get; set; } = "";
  public Guid ClubId { get; set; } public string ClubNumber { get; set; } = ""; public string PaymentMethod { get; set; } = "";
  public string? LoanBow { get; set; } public List<DayOfWeek> UsualDays { get; set; } = []; }
public sealed class HistoryEntry { public Guid PersonId { get; set; } public string SessionText { get; set; } = "";
  public DateTimeOffset At { get; set; } public bool Succeeded { get; set; } public string? Error { get; set; } }
public sealed class WindowPlacement { public double Left { get; set; } public double Top { get; set; }
  public double Width { get; set; } public double Height { get; set; } public bool Maximized { get; set; } }
public sealed class AppSettings { public string FormUrl { get; set; } = FormUrls.ClubForm; public WindowPlacement? Window { get; set; } }
public sealed class AppData { public int Version { get; set; } = 1; public List<Club> Clubs { get; set; } = [];
  public List<Person> People { get; set; } = []; public AppSettings Settings { get; set; } = new();
  public List<HistoryEntry> History { get; set; } = []; public static AppData CreateDefault(); }
public static class FormUrls { public const string ClubForm = "<club URL>"; public const string TestForm = "<test URL>";
  public static bool IsClubForm(string url); /* contains the club form id */ }
public interface IDataProtector { byte[] Protect(byte[] data); byte[] Unprotect(byte[] data); }
public sealed class DpapiProtector : IDataProtector  // DataProtectionScope.CurrentUser, entropy = UTF8("WicklowArcheryApp.v1")
public sealed record LoadResult(AppData Data, string? Notice);
public sealed class AppDataStore
{ public AppDataStore(string directory, IDataProtector protector);
  public static string DefaultDirectory { get; } // %APPDATA%\WicklowArcheryApp (Debug builds: WicklowArcheryApp-Dev)
  public LoadResult Load(); public void Save(AppData data); }
```
`CreateDefault()` returns one club `Wicklow Archery` with `IsDefault = true`, no people, `FormUrl = FormUrls.ClubForm`. The file is `data.bin`.

- [ ] **Step 1: Write the failing tests** (each uses a fresh temp directory)
  - `Load_NoFile_ReturnsSeed`: one club `Wicklow Archery` with IsDefault, `FormUrl == FormUrls.ClubForm`, `Notice == null`.
  - `SaveThenLoad_RoundTrips`: a person `Seán Ó Briain` with `UsualDays = [Wednesday, Sunday]` and `LoanBow = "Left handed adults bow"`, plus one history entry. Everything comes back equal.
  - `Save_WithDpapi_FileIsNotPlainText`: the bytes of `data.bin` don't contain UTF-8 `"Wicklow Archery"`. Loading through a new `AppDataStore` with `DpapiProtector` round-trips.
  - `Load_UndecryptableFile_MovesAsideAndReturnsSeedWithNotice`: write random bytes to `data.bin`. Load returns the seed, `Notice` starts with `Your saved details couldn't be read`, a file matching `data.unreadable-*.bin` exists, and `data.bin` no longer exists.
  - `FormUrls_IsClubForm`: true for `ClubForm`, false for `TestForm`.
- [ ] **Step 2: Run** `dotnet test --filter AppDataStoreTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Serialise to JSON (System.Text.Json, camelCase), then `Protect`. `Save` writes `data.bin.tmp`, then `File.Move(tmp, data.bin, overwrite: true)`, creating the directory if needed. On any Unprotect or JSON failure, `Load` renames the file to `data.unreadable-yyyyMMdd-HHmmss.bin` and returns the seed with the Notice `Your saved details couldn't be read on this PC, so the app has started fresh. The old file was kept as {fileName}.`
- [ ] **Step 4: Run** `dotnet test --filter AppDataStoreTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): encrypted per-user data store`

---

### Task 8: Week planner and confirmation builder

**Files:**
- Create: `src/WicklowArchery.Core/Registration/WeekPlanner.cs`, `ConfirmationBuilder.cs`
- Test: `tests/WicklowArchery.Tests/Core/WeekPlannerTests.cs`, `ConfirmationBuilderTests.cs`

**Interfaces:**
- Consumes: `SessionText.DayOf` (Task 5), `RegistrationForm` (Task 4), `PersonSubmission` (Task 6), `Person`/`Club`/`HistoryEntry` (Task 7).
- Produces:
```csharp
public sealed record WeekCell(Guid PersonId, string SessionText, bool PreTicked, HistoryEntry? AlreadyRegistered);
public static class WeekPlanner
{ public static IReadOnlyList<WeekCell> Build(IReadOnlyList<string> sessions, IReadOnlyList<Person> people, IReadOnlyList<HistoryEntry> history); }
public sealed record Selection(Person Person, IReadOnlyList<string> Sessions);
public sealed record ConfirmItem(Person Person, PersonSubmission? Submission, IReadOnlyList<string> RepeatSessions, string? BlockedReason)
{ public bool IsBlocked => BlockedReason is not null; }
public static class ConfirmationBuilder
{ public static IReadOnlyList<ConfirmItem> Build(RegistrationForm form, IReadOnlyList<Selection> selections,
    IReadOnlyList<Club> clubs, IReadOnlyList<HistoryEntry> history); }
```
A cell is already registered when history has an entry for that person with `Succeeded` and exactly that `SessionText`. `PreTicked` means the day matches a usual day and the person is not already registered. Cells are ordered by person, then session.

BlockedReason copy (exact, first that applies):
`Missing full name.` / `No club set.` / `Missing club number.` / `Payment method "{x}" is no longer on the form. Edit this person to pick one.` (also used when the payment is empty, with `{x}` empty) / `Loan bow "{x}" is no longer on the form. Edit this person to change it.`

- [ ] **Step 1: Write the failing tests**
  - `Build_UsualWednesday_PreTicksWednesdayOnly` (using the three club sessions).
  - `Build_SessionWithoutDayName_NeverPreTicked`.
  - `Build_AlreadyRegistered_MarkedAndNotPreTicked`.
  - `Build_FailedHistory_DoesNotCountAsRegistered`.
  - `Build_NoSessions_ReturnsEmpty`.
  - `Build_PaymentNoLongerOnForm_BlocksOnlyThatPerson`: two people; one has payment `"NON-MEMBER"`. That person is blocked with the exact copy, and the other has a `Submission`.
  - `Build_LoanNoLongerOnForm_Blocks`.
  - `Build_SelectionWithNoSessions_Omitted`.
  - `Build_RepeatSession_Flagged`: history shows the person submitted Wednesday, so `RepeatSessions == ["Wednesday 11th - …"]` and the item is not blocked.
  - `Build_ResolvesClubName`: `Submission.ClubName == "Wicklow Archery"`.
- [ ] **Step 2: Run** `dotnet test --filter "WeekPlannerTests|ConfirmationBuilderTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** as specified above.
- [ ] **Step 4: Run** the same filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(core): weekly pre-ticks, duplicate flags and pre-send checks`

---

### Task 9: HTTP client, submit service and live test

**Files:**
- Create: `src/WicklowArchery.Core/Net/IFormClient.cs`, `HttpFormClient.cs`, `ClubFormGuardClient.cs`, `src/WicklowArchery.Core/Registration/RegistrationService.cs`; `tests/WicklowArchery.Tests/Fakes/FakeFormClient.cs`
- Test: `tests/WicklowArchery.Tests/Core/HttpFormClientTests.cs`, `RegistrationServiceTests.cs`, `tests/WicklowArchery.Tests/Live/LiveSubmitTests.cs`

**Interfaces:**
- Consumes: `FormParser`, `FormMapper`, `PayloadBuilder`, `SubmissionResultDetector`, `ConfirmItem`, `FormUrls`.
- Produces:
```csharp
public sealed record HttpPostResult(int StatusCode, string Body);
public interface IFormClient
{ Task<string> GetAsync(Uri url, CancellationToken ct);
  Task<HttpPostResult> PostAsync(Uri url, IReadOnlyList<KeyValuePair<string, string>> fields, CancellationToken ct); }
public sealed class HttpFormClient(HttpClient http) : IFormClient
{ public static string Encode(IReadOnlyList<KeyValuePair<string, string>> fields); } // application/x-www-form-urlencoded, UTF-8
public sealed class ClubFormGuardClient(IFormClient inner) : IFormClient; // PostAsync to a club-form URL throws InvalidOperationException
public sealed record FormLoadResult(FormCheck? Check, string? Error);
public enum PersonResultState { Pending, Sending, Submitted, Failed, Skipped }
public sealed record PersonResult(ConfirmItem Item, PersonResultState State, string? Error, DateTimeOffset At);
public sealed class RegistrationService(IFormClient client, TimeProvider clock)
{ public Task<FormLoadResult> LoadFormAsync(string formUrl, CancellationToken ct);
  public Task<IReadOnlyList<PersonResult>> SubmitAsync(RegistrationForm form, IReadOnlyList<ConfirmItem> items,
    IProgress<PersonResult>? progress, CancellationToken ct); }
```
`FakeFormClient` returns a configurable GET body and a queue (or function) of POST results or exceptions, and records every POST.

Error copy (exact):
- load, network: `Couldn't reach the form. Check your internet connection.`
- load, parse: `The form page couldn't be read. Google may have changed its format.`
- submit, network or timeout: `Couldn't reach Google: {message}`
- submit, Rejected: `Google didn't accept the response. The form may have changed.`
- submit, Unexpected: `Unexpected reply from Google (HTTP {code}).`

- [ ] **Step 1: Write the failing tests**
  - `Encode_IrishNamesRoundTrip`: encode `[("entry.1","Seán Ó Briain"),("entry.2","O'Brien & Co"),("entry.3","a=b+c")]`, split on `&` and `=`, `WebUtility.UrlDecode` each part, and expect the original values.
  - `GuardClient_PostToClubForm_Throws`; `GuardClient_PostToTestForm_PassesThrough`.
  - `Load_NetworkError_ReturnsError` (the fake GET throws `HttpRequestException`).
  - `Load_TestSnapshot_ReturnsUsableCheck`.
  - `Load_GarbagePage_ReturnsParseError`.
  - `Submit_SendsOnePostPerUnblockedPerson_InOrder`.
  - `Submit_BlockedItem_SkippedWithoutPost`.
  - `Submit_NetworkErrorForOnePerson_ContinuesWithOthers`: three people where the 2nd POST throws. The results are Submitted, Failed (Error starting `Couldn't reach Google:`), Submitted.
  - `Submit_RejectedReply_Failed`.
  - `Submit_ReportsSendingThenFinalState` (via `IProgress`).
- [ ] **Step 2: Run** `dotnet test --filter "HttpFormClientTests|RegistrationServiceTests"`. Expected: FAIL.
- [ ] **Step 3: Implement.** `HttpFormClient.Encode` uses `FormUrlEncodedContent`. The caller sets a 30 s timeout on the `HttpClient` and the User-Agent `WicklowArcheryApp/1.0`. `SubmitAsync` POSTs to `form.ResponseUrl` one person at a time, never in parallel.
- [ ] **Step 4: Run** the same filter. Expected: PASS.
- [ ] **Step 5: Live test.** `LiveSubmitTests.Submit_ToTestForm_Succeeds`, marked `[Trait("Category","Live")]`. First assert `!FormUrls.IsClubForm(FormUrls.TestForm)`. Then load `FormUrls.TestForm` through a real `HttpFormClient` wrapped in `ClubFormGuardClient`, and submit one person named `TEST live {yyyy-MM-dd HH:mm}` for the first session. Expect `Submitted`.
Run: `dotnet test --filter "Category=Live"`. Expected: PASS, and the response is visible in the test form's Responses tab (check by eye or through Chrome).
- [ ] **Step 6: Commit** `feat(core): HTTP form client, submit service, live test against test form`

---

### Task 10: Main screen with sample data (**UI checkpoint**)

**Files:**
- Create: `src/WicklowArchery.App/Theme/Styles.xaml`, `ViewModels/WeekViewModel.cs`, `Views/WeekView.xaml(.cs)`, `Services/SampleData.cs`
- Modify: `App.xaml` (merge Styles.xaml), `App.xaml.cs`, `MainWindow.xaml(.cs)`
- Test: `tests/WicklowArchery.Tests/App/WeekViewModelTests.cs`

**Interfaces:**
- Consumes: `WeekPlanner`, `SessionText.Split`, `Selection`, `AppData`, `RegistrationForm`.
- Produces:
```csharp
public partial class SessionCellViewModel : ObservableObject
{ public Guid PersonId { get; } public string SessionText { get; } public HistoryEntry? AlreadyRegistered { get; }
  [ObservableProperty] bool isTicked; public bool IsRepeat => AlreadyRegistered is not null && IsTicked; }
public sealed class SessionHeaderViewModel(string fullText) { public string Headline { get; } public string Detail { get; } public string FullText { get; } }
public sealed class PersonRowViewModel { public Person Person { get; } public string Name { get; } public ObservableCollection<SessionCellViewModel> Cells { get; } }
public partial class WeekViewModel : ObservableObject
{ public ObservableCollection<SessionHeaderViewModel> Headers { get; } public ObservableCollection<PersonRowViewModel> Rows { get; }
  public int PeopleCount { get; } public int RegistrationCount { get; } public string SummaryText { get; }
  public bool HasSessions { get; }
  public void Load(RegistrationForm form, AppData data); public IReadOnlyList<Selection> GetSelections(); }
```
`SummaryText` reads `3 people · 5 registrations`, with singulars `1 person · 1 registration`. With nothing ticked it reads `Nothing ticked yet`. The counts update whenever any cell is toggled.

Layout follows the spec's "Main screen" sketch:
- The header has a title, a status pill, and Refresh, People and Settings icon buttons.
- The grid is one card. Each session column is 200 px wide, with the headline in SemiBold above wrapped detail text in `TextFillColorSecondaryBrush`. Each person row has the name on the left and a large `SessionToggle` (a ToggleButton style with a check glyph, 44 px minimum height) in each cell. An already-registered cell shows a green `Registered ✓` badge (`SystemFillColorSuccessBrush`); clicking it ticks the cell, which is then shown as a repeat with a caution outline (`SystemFillColorCautionBrush`).
- The footer has the summary on the left and the primary **Complete Form** button (accent) on the right.
- Theme brushes come from Fluent (`CardBackgroundFillColorDefaultBrush`, `ControlStrokeColorDefaultBrush`, `AccentFillColorDefaultBrush`). Icons come from `Segoe Fluent Icons`. Keyboard: `KeyboardNavigation.DirectionalNavigation="Contained"` on the grid, so arrow keys move between cells and Space toggles.

- [ ] **Step 1: Write the failing tests**
  - `Load_PreTicksUsualDaysAndCountsThem`: with 2 people and the test snapshot form, check the counts and `SummaryText`.
  - `ToggleCell_UpdatesSummary`.
  - `Summary_Singular`.
  - `Summary_NothingTicked`.
  - `RegisteredCell_TickedBecomesRepeat`.
  - `GetSelections_OnlyPeopleWithTicks_InSessionOrder`.
  - `NoSessions_HasSessionsFalse`.
- [ ] **Step 2: Run** `dotnet test --filter WeekViewModelTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the ViewModels and WeekView. `SampleData` (inside `#if DEBUG`) builds the test-form snapshot's `RegistrationForm` from `test-form-2026-11-09.html`, linked into the App project as an `EmbeddedResource` from `docs/reference/form-snapshots/` in Debug builds only (`Condition="'$(Configuration)'=='Debug'"`), and four sample people (`Sample Person 1` to `4`, mixed usual days, one with a Wednesday history entry). Launching with `--sample` shows this screen without network access or saving.
- [ ] **Step 4: Run** `dotnet test --filter WeekViewModelTests`. Expected: PASS.
- [ ] **Step 5: Checkpoint.** Run `dotnet run --project src/WicklowArchery.App -- --sample` and take screenshots in light and dark mode, at 100% and 150% display scaling. **Stop and show them to James.** Continue only after he approves the look. Make his requested changes here, re-screenshot, and ask again. If the built-in Fluent theme falls short, raise switching to WPF-UI with him now (spec fallback).
- [ ] **Step 6: Commit** `feat(app): main weekly grid with Fluent styling (sample mode)`

---

### Task 11: Live form loading, status, test badge, first run, window placement

**Files:**
- Create: `src/WicklowArchery.App/ViewModels/MainViewModel.cs`, `Services/IUrlOpener.cs` (interface + `ShellUrlOpener`, see Task 12 Interfaces)
- Modify: `App.xaml.cs` (composition), `MainWindow.xaml(.cs)`
- Test: `tests/WicklowArchery.Tests/App/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `RegistrationService`, `AppDataStore`, `WeekViewModel`, `FormUrls`, `ClubFormGuardClient`.
- Produces:
```csharp
public enum FormStatus { Checking, UpToDate, Changed, Offline }
public enum Page { Week, People, Settings }
public partial class MainViewModel : ObservableObject
{ public MainViewModel(RegistrationService service, AppDataStore store, IUrlOpener urlOpener);
  public AppData Data { get; }  public RegistrationForm? Form { get; }
  public FormStatus Status { get; } public string StatusText { get; } public IReadOnlyList<string> Problems { get; }
  public bool IsTestForm { get; } public bool ShowEmptyState { get; } public string? Notice { get; }
  public WeekViewModel Week { get; } public Page CurrentPage { get; set; } public object? Overlay { get; set; }
  public string? CompleteFormDisabledReason { get; }
  public IAsyncRelayCommand RefreshCommand { get; } public IRelayCommand CompleteFormCommand { get; }
  public Task InitializeAsync(); public void SaveData(); }
```
Copy (exact):
- `StatusText`: `Checking this week's form…` / `This week's form ✓ · {n} sessions` / `Form changed: Complete Form disabled` / `Can't reach the form`.
- `CompleteFormDisabledReason`, in priority order: `Checking the form…`, `Can't reach the form.`, `The form has changed. Click the status for details.`, `Add a person first.`, `No sessions on this week's form.`, `Tick at least one session.`; null when enabled.

In `App.xaml.cs`, Debug builds wrap the `HttpFormClient` in `ClubFormGuardClient`. Clicking the status pill when it's `Changed` shows `Problems` in a flyout or popup. The `Test form` badge (caution colour) shows when `!FormUrls.IsClubForm(Data.Settings.FormUrl)`. The empty state, with one **Add your first person** button that goes to the People page, replaces the grid when `Data.People` is empty. A store `Notice` shows as a dismissible info bar. Ctrl+Enter runs `CompleteFormCommand`. Window placement is restored on start (if it's on a visible screen) and saved on close.

- [ ] **Step 1: Write the failing tests**, using `FakeFormClient`, a temp-dir `AppDataStore` with `PassThroughProtector`, and a fake `IUrlOpener`:
  - `Init_TestSnapshot_UpToDateWithSessionCount`.
  - `Init_NetworkError_OfflineAndDisabled`.
  - `Init_ChangedForm_ChangedWithProblemsAndDisabled`.
  - `Refresh_AfterOffline_RecoversToUpToDate`.
  - `IsTestForm_TrueForTestUrl_FalseForClubUrl`.
  - `NoPeople_ShowEmptyState_AndReasonAddPersonFirst`.
  - `NothingTicked_ReasonTickAtLeastOne`.
- [ ] **Step 2: Run** `dotnet test --filter MainViewModelTests`. Expected: FAIL.
- [ ] **Step 3: Implement** as specified.
- [ ] **Step 4: Run** `dotnet test --filter MainViewModelTests`. Expected: PASS. Then run the app normally (a Debug build, which uses the `-Dev` data folder) and check by eye: the status reaches "up to date" against the club form (a GET only), and the empty state shows.
- [ ] **Step 5: Commit** `feat(app): live form check, status pill, test-form badge, first-run state`

---

### Task 12: Confirm panel, sending, results and SumUp

**Files:**
- Create: `src/WicklowArchery.App/ViewModels/ConfirmViewModel.cs`, `ResultsViewModel.cs`, `Views/ConfirmPanel.xaml(.cs)`, `Views/ResultsPanel.xaml(.cs)`
- Modify: `MainViewModel.cs` (CompleteForm opens Confirm; results are recorded in history)
- Test: `tests/WicklowArchery.Tests/App/ConfirmViewModelTests.cs`

**Interfaces:**
- Consumes: `ConfirmationBuilder`, `RegistrationService.SubmitAsync`, `PersonResult`, `MainViewModel.Data/SaveData/Overlay`.
- Produces:
```csharp
public interface IUrlOpener { void Open(Uri url); }   // created in Task 11. ShellUrlOpener: Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true })
public partial class ConfirmViewModel : ObservableObject
{ public IReadOnlyList<ConfirmItem> Items { get; } public int SendableCount { get; }
  public IAsyncRelayCommand SubmitCommand { get; } public IRelayCommand CancelCommand { get; } }
public partial class ResultRowViewModel : ObservableObject
{ public string Name { get; } public string SessionsText { get; } [ObservableProperty] PersonResultState state; [ObservableProperty] string? error; }
public partial class ResultsViewModel : ObservableObject
{ public ObservableCollection<ResultRowViewModel> Rows { get; } public bool ShowSumUp { get; }
  public IRelayCommand PaySumUpCommand { get; } public IRelayCommand DoneCommand { get; } }
```
- The confirm panel is an in-window overlay card. It lists each person → their sessions (headlines). Repeat sessions get a caution line: `Already registered on {date:ddd d MMM}; will be sent again`. Blocked people are greyed out with their reason. The buttons are **Submit ({n})** and Cancel; Submit is disabled when n = 0.
- On Submit, the overlay switches to Results. Each row shows a spinner while sending, then ✔ `Submitted` or ✘ with the error.
- After the run, a `HistoryEntry` is appended for every session of every Submitted or Failed person (`Succeeded` set to match), `SaveData()` runs, and the week grid reloads so the new registrations show as Registered.
- `ShowSumUp` is true when any Submitted person's `PaymentMethod == "NON CLUB MEMBER PAYMENT"` and `Form.SumUpUrl` is not null. The button reads **Pay with SumUp**.

- [ ] **Step 1: Write the failing tests**
  - `Confirm_ShowsRepeatsAndBlocked`.
  - `Submit_InvokedTwiceWhileRunning_PostsOncePerPerson`: the fake POST waits on a `TaskCompletionSource`. Call `SubmitCommand.ExecuteAsync` twice, then release. The POST count equals the number of sendable people.
  - `Submit_RecordsHistoryAndSaves`: reloading the store shows Succeeded entries.
  - `Submit_FailedPerson_HistoryNotSucceeded`.
  - `Results_ShowSumUp_OnlyForNonMemberSubmitted`.
  - `PaySumUp_OpensFormSumUpUrl`.
  - `AfterSubmit_GridShowsRegistered`.
- [ ] **Step 2: Run** `dotnet test --filter ConfirmViewModelTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Use `AsyncRelayCommand` with its default `AllowConcurrentExecutions = false`, and also disable Cancel while running.
- [ ] **Step 4: Run** `dotnet test --filter ConfirmViewModelTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(app): confirm panel, per-person results, SumUp link, history`

---

### Task 13: People page

**Files:**
- Create: `src/WicklowArchery.App/ViewModels/PeopleViewModel.cs`, `Views/PeopleView.xaml(.cs)`
- Test: `tests/WicklowArchery.Tests/App/PeopleViewModelTests.cs`

**Interfaces:**
- Consumes: `MainViewModel.Data/SaveData/Form`, `Person`, `Club`.
- Produces:
```csharp
public partial class PersonEditorViewModel : ObservableObject
{ public Person Person { get; } [ObservableProperty] string fullName; [ObservableProperty] Club? club;
  [ObservableProperty] string clubNumber; [ObservableProperty] string? paymentMethod; [ObservableProperty] string? loanBow;
  public IReadOnlyList<DayChipViewModel> Days { get; }               // Mon..Sun, each with IsSelected
  public IReadOnlyList<string> PaymentOptions { get; } public IReadOnlyList<string?> LoanOptions { get; } } // LoanOptions[0] == null shown as "No loan bow"
public partial class PeopleViewModel : ObservableObject
{ public ObservableCollection<PersonEditorViewModel> People { get; } [ObservableProperty] PersonEditorViewModel? selected;
  public IRelayCommand AddCommand { get; } public IRelayCommand RemoveCommand { get; } public bool ShowSaved { get; } }
```
- Every edit writes through to `Person` and calls `SaveData()`. A `Saved` indicator shows for 2 s.
- Payment and loan option lists come from `Form` when it's loaded. If a person's saved value isn't in the list, it stays selected and gets a caution note: `Not on this week's form`.
- A new person gets the default club, no usual days, and the payment unset.
- Remove asks inline (`Remove {name}?` with **Remove** / Cancel) rather than in a dialog.
- Layout: list on the left (name, plus club and number in secondary text); edit card on the right; day chips as seven toggle pills.

- [ ] **Step 1: Write the failing tests**
  - `Add_CreatesPersonWithDefaultClub_AndSelectsIt`.
  - `EditName_SavesToStore` (reload to check).
  - `ToggleDayChip_UpdatesUsualDays`.
  - `Remove_DeletesAndSaves`.
  - `SavedPaymentNotOnForm_StillShownWithWarning`.
- [ ] **Step 2: Run** `dotnet test --filter PeopleViewModelTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Returning to the Week page reloads the grid from `Data`.
- [ ] **Step 4: Run** `dotnet test --filter PeopleViewModelTests`. Expected: PASS. Check by eye in the app as well.
- [ ] **Step 5: Commit** `feat(app): people page with auto-save`

---

### Task 14: Settings page (clubs and form URL)

**Files:**
- Create: `src/WicklowArchery.App/ViewModels/SettingsViewModel.cs`, `Views/SettingsView.xaml(.cs)`
- Test: `tests/WicklowArchery.Tests/App/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `MainViewModel` (`Data`, `SaveData`, `RefreshCommand`), `FormUrls`.
- Produces: `SettingsViewModel` with `ObservableCollection<Club> Clubs`, `AddClubCommand`, `RemoveClubCommand(Club)`, `SetDefaultClubCommand(Club)`, `string FormUrl`, `UseClubFormCommand`, `UseTestFormCommand`, and `string? ClubMessage`.

Rules:
- Exactly one default club at all times.
- A club in use can't be removed: `In use by {n} people.` (`1 person` when singular).
- The last club can't be removed.
- Renaming saves immediately.
- Changing `FormUrl` (by typing and losing focus, or with either button) saves and runs `RefreshCommand`.

- [ ] **Step 1: Write the failing tests**
  - `SetDefault_LeavesExactlyOneDefault`.
  - `RemoveClubInUse_BlockedWithMessage`.
  - `RemoveLastClub_Blocked`.
  - `UseTestForm_SetsUrlSavesAndRefreshes` (then `MainViewModel.IsTestForm` is true).
  - `UseClubForm_ClearsTestBadge`.
- [ ] **Step 2: Run** `dotnet test --filter SettingsViewModelTests`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `dotnet test --filter SettingsViewModelTests`. Expected: PASS.
- [ ] **Step 5: Commit** `feat(app): settings page for clubs and form URL`

---

### Task 15: Single-file publish and end-to-end check on the test form

**Files:**
- Modify: `src/WicklowArchery.App/WicklowArchery.App.csproj` (`<AssemblyName>WicklowArchers</AssemblyName>`, `<ApplicationIcon>` optional), `CLAUDE.md` (Status), `docs/session-notes/<date>-build.md`

- [ ] **Step 1: Full test run.** `dotnet test --filter "Category!=Live"`. Expected: all pass, 0 failed.
- [ ] **Step 2: Publish**
```bash
dotnet publish src/WicklowArchery.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
Expected: `publish/WicklowArchers.exe` exists and is the only file needed. Record its size.
- [ ] **Step 3: Run from elsewhere.** Copy the exe alone to a new empty folder and run it. It opens and the status reaches "up to date" (GET of the club form). No files appear beside the exe.
- [ ] **Step 4: End-to-end on the test form** (Release build; there is no guard client, so **switch to the test form in Settings first** and confirm the `Test form` badge is showing):
  1. Add two people named `TEST E2E 1` and `TEST E2E 2`, one with payment `NON CLUB MEMBER PAYMENT` and a loan bow.
  2. Tick sessions, Complete Form, Submit. Both rows show ✔. **Pay with SumUp** opens `https://wicklow-archers.sumup.link` in the default browser.
  3. Check the test form's Responses tab: both responses are there with the right sessions, payment and loan.
  4. Reopen the app: those cells show **Registered ✓** and are not pre-ticked.
  5. Switch back to the club form in Settings. The badge disappears. Don't submit.
- [ ] **Step 5: Notes.** Write the session note and update the CLAUDE.md Status: what was measured, the exe size, and anything left open.
- [ ] **Step 6: Commit** `build: single-file publish; e2e verified on test form`
