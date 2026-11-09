# Google Forms submit: measured responses (test form only)

Run: 2026-11-09, started 18:15:54 UTC. Three POSTs, one each of A, B, C, no retries.
Test form only: `T=https://docs.google.com/forms/d/e/1FAIpQLSdP3j6gNAZbVACWKqW-evjYMjcMXvA1R2XUjJpJzJmC6M-Inw`
(guard checked that `$T` contained that id before each curl). Fresh form fetched with `curl -s "$T/viewform"` (HTTP 200) and parsed with `parse_form.py`;
`fbzx` taken from it: `-4539945746472240085`. Venue used: `Wednesday 11th - St Gerards Hall - Training- doors open at 6.45pm`.
Payment: `WICKLOW ARCHERY CLUB MEMBER - FREE NO PAYMENT`. `partialResponse` was sent as `[null,null,"<fbzx>"]` (inferred shape, not copied from a browser).

## Results

| POST | What | HTTP status | Body saved as |
|---|---|---|---|
| A | full request (hidden fields + sentinels) | **200** | `test-form-response-ok.html` (75375 bytes) |
| B | entries only, no hidden fields, no sentinels | **200** | scratch only (75377 bytes, same page as A except the nonce) |
| C | like A, `entry.353097525` (Full Name) missing | **400** | `test-form-response-error.html` (111350 bytes) |

Measured: B's body also contains "Your response has been recorded." So Google returned the success page for B.
Whether B was actually stored is not known from the reply alone (see Responses tab below).

## Commands (A shown in full; B and C differ as stated)

```
D=docs/reference/form-snapshots; FB=-4539945746472240085
curl -s -o $D/test-form-response-ok.html -w "%{http_code}\n" \
 --data-urlencode "fvv=1" --data-urlencode "pageHistory=0" --data-urlencode "fbzx=$FB" \
 --data-urlencode "partialResponse=[null,null,\"$FB\"]" --data-urlencode "submissionTimestamp=-1" \
 --data-urlencode "entry.353097525=TEST spike A" --data-urlencode "entry.859175428=Wicklow Archery" \
 --data-urlencode "entry.168390220=TEST-0000" \
 --data-urlencode "entry.2011193388=$V" --data-urlencode "entry.2011193388_sentinel=" \
 --data-urlencode "entry.1751963076=$P" --data-urlencode "entry.1751963076_sentinel=" \
 --data-urlencode "entry.1412580528_sentinel=" "$T/formResponse"
```
- B: only `entry.353097525=TEST spike B`, `entry.859175428`, `entry.168390220`, `entry.2011193388`, `entry.1751963076` (same values); no hidden fields, no sentinels.
- C: as A but without the `entry.353097525` argument.
(Fields: name `entry.353097525`, club `entry.859175428`, number `entry.168390220`, venue `entry.2011193388`, payment `entry.1751963076`, loan `entry.1412580528` (left empty, sentinel only).)

## Markers

Success marker (ok body only; the error body has 0 occurrences): the text `Your response has been recorded.`
Excerpt (ok body, line is one long line, trimmed):
```
...TEST - Wicklow Archers Venue Registration form 2026</div><div class="vHW8K">Your response has been recorded.</div><div class="c2gzEf"><a href="http...
```
A second ok-only string is `Submit another response` (1 in ok, 0 in error). The CSS class `vHW8K` is obfuscated and may change; prefer the text.

Error marker (error body only): `This is a required question` (1 in error, 0 in ok).
Excerpt (error body):
```
...e-i5vt6e-oq6NAc" aria-hidden="true">&nbsp;</div></div></div></div><span class="RHiWt">This is a required question</span></div></div></div>...
```

`FB_PUBLIC_LOAD_DATA_`: present in BOTH bodies (1 each), so it cannot tell them apart. In the ok body it is a stub:
`var FB_PUBLIC_LOAD_DATA_ = [null,[],"/forms","Untitled form",null,...`
In the error body it holds the full form: `var FB_PUBLIC_LOAD_DATA_ = [null,[null,[[1719010129,"Full Name: ",null,0,[[353097525...`
The error body also contains `name="fbzx"` and a `<form` (the form is re-rendered); the ok body has neither.

## Decision for Task 6

POST A was accepted, so the payload includes the hidden fields and sentinels. Decide success on: HTTP 200 AND body contains `Your response has been recorded`.
A 400 (or a 200 without that text) is a failure; for a 400 the body names the problem, e.g. `This is a required question`.
Caveat: B also returned 200 with the success text, so the success text alone does not prove the hidden fields were needed.

## Responses tab: checked

Checked 2026-11-09 by Claude via Chrome on the test form's Responses tab.

Measured:
- 3 responses: `TEST live 2026-11-09 18:30`, `TEST spike A`, `TEST spike B`. `TEST spike C` is absent.
- Venue counts: Monday 1, Wednesday 2, Sunday 0.
- Payment: all three `WICKLOW ARCHERY CLUB MEMBER - FREE NO PAYMENT`.
- Club: `Wicklow Archery` x3. Number: `TEST-0000` x3. Loan: 0.

Conclusions:
- The success marker means the response was stored.
- A 400 means the response was not stored (C).
- An entries-only POST (B: no hidden fields, no sentinels) is also stored.
