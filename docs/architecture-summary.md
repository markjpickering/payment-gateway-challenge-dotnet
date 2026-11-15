# Architecture decisions: summary

A one-page digest of [`architecture-decisions.md`](architecture-decisions.md), which has the full reasoning and the
alternatives rejected (ADR numbers below point into it). For how the code fits together see
[`how-it-works.md`](how-it-works.md).

**The rule that settles conflicts:** the brief asks for simple, maintainable code ("we do not want to encourage
over-engineering"), so **KISS wins** over every other principle, and where this project differs from the brief it says so
in the README's departures table. The deliberate additions are authentication and `amount > 0`. Idempotency, an audit
trail, reversals and metrics are designed (ADR-15, 21, 22, 23) but are not built on this branch.

## Shape of the solution

| Decision | Why | ADR |
|---|---|---|
| **One project, five layers by folder** (`Api`, `ApiInterface`, `BusinessLogic`, `DataInterface`, `DataLayer`, plus a small `Shared`). No Clean Architecture, CQRS or mediator. | Four projects and a mediator for two endpoints is the over-engineering the brief warns about. Folders give the same separation; architecture tests enforce the one-way dependencies. | 1, 27 |
| **Minimal APIs**, thin endpoints (bind, call the service, map to HTTP). | Less ceremony for two endpoints; validation is explicit in the service because an invalid request must return `Rejected`, not the framework's automatic 400. | 2 |
| **In-memory, thread-safe repository** behind an interface. No database, no EF Core. | The brief says a test double is fine. The interface makes a real store a drop-in later. | 3 |
| **.NET 10 (LTS), C# 14**, latest stable packages; `LangVersion` not set. | .NET 8 is out of support; `latest` could demand a newer SDK than a reviewer has. | 4 |
| **The contracts are kept separable**: `ApiInterface` and `DataInterface` depend on no implementation. | They could become projects, then services, if the system grew. Not worth it for one team and two endpoints. | 27 |

## Behaviour

| Decision | Why | ADR |
|---|---|---|
| **Invalid input of any kind is `Rejected` (400)**: the bank is never called and nothing is stored. FluentValidation, called explicitly from `PaymentService`. | It is the brief's rule, and calling the validator from the service keeps the flow visible without a pipeline behaviour. | 8 |
| **Amount must be greater than zero** (our addition). | Zero or negative would be sent to the bank and stored as `Authorized`; it is never a real payment. | 8 |
| **Bank 503, timeout or bad reply is an error (502), not `Declined`.** No automatic retries. | Telling a merchant a card was declined when the bank was simply down is wrong. Retrying a payment is only safe with idempotency. | 9, 17 |
| **Only the last four digits are stored or returned; never the CVV.** Card fields are strings; request records redact `ToString()`. | PCI and the brief. Strings keep leading zeros. Records print every property by default, so redaction is explicit. | 9, 20 |
| **Expected failures are `Result` values, not exceptions.** Specific exceptions are caught only in the bank client; one global handler returns a generic 500 `ProblemDetails` with a `traceId`. | Declines and outages are routine, so the flow stays visible and testable. No stack traces or card data leak. | 17 |
| **Async all the way for I/O**, `CancellationToken` everywhere, no blocking calls. | Blocking ties up request threads. The repository returns `Task` so a real store changes no callers. | 16 |

## Security and operations

| Decision | Why | ADR |
|---|---|---|
| **JWT bearer with scope policies** (`payments:create`, `payments:read`); dev tokens from `dotnet user-jwts`. No token-issuing code, no off switch, no hard-coded token. | A payments API should not be open. Real validation in code, one command for a reviewer to get a token. | 7 |
| **Per-merchant ownership**: payments carry the token's merchant; another merchant gets 404. Checked in the service, not the store. | Prevents broken object-level authorisation, the top OWASP API risk. | 7, 26 |
| **Two identities per call** (who made it, who it was for), taken only from the token. | They can differ (support user, platform). | 7 |
| **Serilog behind `ILogger<T>`**, JSON console, structured templates, no sensitive data; the framework `traceId` is the correlation id. | Searchable logs without wrapping `ILogger` or adding correlation middleware. | 11 |
| **HTTPS plus HSTS, 4 KB body limit.** Rate limiting is a documented gap. | A cheap abuse guard now; rate limits need real traffic to size. | 7, 11 |
| **Metrics seam only**: the bank client calls `IPaymentMetrics`, backed by a no-op placeholder. | Keeps the real design (OpenTelemetry, low-cardinality tags) documented without building it. | 15 |

## Organisation and naming

| Decision | Why | ADR |
|---|---|---|
| **DTOs, bank messages and stored entities are separate types**, joined by hand-written mappings (request, `ProcessedPaymentModel`, `PaymentEntity`, response). | The API contract and the storage shape change for different reasons; sharing a type invites leaking stored fields. | 26 |
| **Storage holds no business logic.** The repository only adds and reads by key. | A store with no rules is trivial to test and swap, and ownership cannot be lost by swapping it. | 26 |
| **Names show the role**: `…Request`/`…Response` (no `Dto` suffix), `…Entity`, `…Model`. | The suffix already marks a DTO. A test fails if a type is in the wrong place. | 27 |
| **Hand-written `IMapping<TSource, TDestination>`**, no AutoMapper. | Field-by-field mapping is visible, a rename is a compile error, and the name avoids `IMapper`. | 14 |
| **One composition root**, the static `PaymentGatewayDi` whose first method, `InitialiseDi`, wires everything. | One place to see what implements each interface; `Program.cs` stays tiny and tests reuse the registrations. | 12 |
| **Every non-trivial class has an interface; data holders do not.** Records for DTOs and entities. | A uniform rule is easier to follow than judging each class. | 13, 20 |

## Testing and code quality

| Decision | Why | ADR |
|---|---|---|
| **xUnit v3 (Microsoft Testing Platform) with AwesomeAssertions and NSubstitute.** | FluentAssertions 8+ needs a paid licence. NSubstitute states "the bank was not called" in one line. | 6 |
| **Integration tests through the real app** (only the bank, clock and auth are replaced); a few tagged tests against the real simulator. | Only an end-to-end test proves wiring, binding and the HTTP status mapping. No Docker needed for the normal run. | 6 |
| **Test-first for anything with logic**; Arrange-Act-Assert comments; at most 10 tests per class, split by functional area into `…TestClass` files. | Keeps tests uniform and each failure pointing at one area. | 6, 13, 19 |
| **Size limits** (methods 20 lines, classes 200) and **one type per file.** | Small units are readable and fit the test-first approach; a limit is a prompt to extract, not a target. | 18, 19 |
| **Global usings per project**, minimal boilerplate, rule of three. | Many small files would otherwise start with the same block of usings. | 24 |
| **Written for the reviewer**: a reading guide, a folder README per layer, one departures table, docs that tests keep true. | A reviewer reads cold and quickly and judges judgement as well as correctness. | 28 |

## Known gaps (documented, not built)

Rate limiting; persistent encrypted storage; idempotency, audit and reversals on this branch (without idempotency, a
client that retries a timed-out `POST` can take a payment twice); `AllowedHosts` is `*`.
