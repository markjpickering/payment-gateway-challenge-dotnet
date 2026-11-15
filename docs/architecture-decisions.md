# Architecture Decisions

Decisions made while planning the Payment Gateway challenge, with the reasoning and the alternatives rejected. Recorded 2026-11-14. **Status: decided. The core is implemented; the features marked NOT IMPLEMENTED (idempotency, audit trail, reversals and the real metrics) are designs only** (see "Implementation status" at the end).

**Start here.** Most of the file is reference. The decisions that shape the code a reviewer reads are: ADR-1 (keep it simple, project layout), ADR-8 (validation), ADR-9 (the bank), ADR-17 (errors as values, no exceptions for flow control), ADR-26 (DTOs and entities are separate; storage holds no logic) and ADR-27 (the five layers and naming). ADR-7 is authentication. ADR-15, 21, 22 and 23 (metrics, idempotency, audit, reversals) describe concerns that are NOT implemented in this repository (each carries a status banner). The rest is testing, tooling and conventions.

**How this file is organised.** The decisions are grouped by theme, in the same order as [`architecture-summary.md`](architecture-summary.md) (a one-page digest of this file). Numbers are the order in which the decisions were made and are kept stable, because the code, the tests and `CLAUDE.md` refer to them, so within a theme they are not in numeric order.

## Guiding principle: KISS

The brief asks for code that is "simple and maintainable. We do not want to encourage over-engineering" and an architecture "focused on meeting the functional requirements". The brief's functional scope is two endpoints (create a payment, retrieve a payment), validation, one call to a bank simulator and in-memory storage; the requirements for authentication, metrics, idempotency, auditing and reversals (ADR-7, 15, 21, 22, 23) are deliberate additions to it. Where another principle (SOLID, clean architecture, patterns) would add layers or dependencies, KISS wins.

**Other design principles (also in force, subordinate to KISS).**
- **DRY.** Avoid duplicating logic; extract it once it is genuinely the same thing, not merely similar-looking (which would couple unrelated code).
- **SOLID**, applied where it keeps the code simple and testable: small single-purpose classes, dependencies on interfaces, behaviour extended by adding classes (ADR-13, ADR-18).
- **Declarative and functional style where possible** (expressions over mutable loops, immutable records, pure functions for validation and mapping), while staying mindful of performance and memory use: no needless allocations on hot paths, no premature optimisation.
- **Design patterns where they fit, named where used.** Prefer an established pattern (for example Result, Adapter for the bank client, Repository, Ledger, Options) over an ad-hoc structure, and where a pattern is used, say which pattern and why in a comment on the type. Do not force a pattern in where it adds nothing (KISS).
- **Dependency injection for all dependencies** (ADR-12), **namespaces that follow the folder structure**, **file-scoped namespaces**, and **interfaces kept separate from implementations in their own folder** (ADR-1, ADR-13).
- **Dependencies are kept at the latest stable versions** compatible with .NET 10 (ADR-4).

*Why.* These are the team's stated working principles; recording them here keeps `CLAUDE.md` short while preserving the reasons, and the KISS ordering says which wins when they conflict.

---

# The shape of the solution

## ADR-1: Single API project, layered by folder (no Clean Architecture, CQRS or Mediator)

**Decision.** One API project organised by folder; the layout was refined into five layers (`Api/`, `ApiInterface/`, `BusinessLogic/`, `DataInterface/`, `DataLayer/`) in ADR-27. A `PaymentService` runs the flow (validate -> call bank -> store). Interfaces live in `BusinessLogic/Interfaces/` and `DataInterface/`, separate from implementations.

**Why.**
- Four projects, handlers and a mediator for two endpoints is the over-engineering the brief warns against.
- The usual argument for CQRS (separate read/write models for performance) does not apply: there is one in-memory store and one model.
- A reviewer should be able to read the whole solution quickly and see the payment flow in one place.

**Rejected.**
- *Clean Architecture (Domain/Application/Infrastructure/Api projects).* Strict dependency direction, but heavy for this size. Extract projects later only if the code outgrows the folders.
- *CQRS with Mediator and pipeline behaviours.* Adds a dependency and indirection with no benefit here; a reviewer would reasonably ask why.

---

## ADR-2: Minimal APIs instead of controllers

**Decision.** Replace the template's `PaymentsController` with Minimal API endpoints, grouped in a `MapPaymentEndpoints()` extension method. Endpoints stay thin: bind the request, call `IPaymentService`, map the result to HTTP.

**Why.**
- Less code and ceremony for two endpoints, and it is the current .NET style.
- Fits the KISS principle.
- Validation is done explicitly in the service, which suits a case where an invalid request must return a `Rejected` payment rather than the framework's automatic 400.

**Trade-off.** The template provides a controller and tests written against it, so this deviates from the starting point. The controller tests are rewritten as endpoint tests.

**Rejected.** Keeping controllers: less churn, but no functional benefit.

---

## ADR-3: In-memory repository, no database and no EF Core

**Decision.** Keep the template's in-memory `PaymentsRepository`, put it behind an interface (`IPaymentsRepository`) and register it as a singleton. Do not add Entity Framework Core or any DBMS.

**Why.**
- The brief states: "You do not need to integrate with a real storage engine or database. It is fine to use the test double repository provided in the sample code."
- An ORM, migrations and a database would add setup for the reviewer (and cost) without helping any requirement.
- The interface keeps the service testable and makes a real store a drop-in change later.
- It is a singleton shared by concurrent requests, so its storage is made thread-safe (`ConcurrentDictionary`) instead of the template's plain `List<>`.

**Rejected.** EF Core (even with the in-memory provider): extra dependency and concepts for no gain. An earlier draft of the project notes listed EF Core 10 and migration commands; these were removed.

---

## ADR-4: Target .NET 10 (LTS) with C# 14

**Decision.** Retarget both projects from `net8.0` to `net10.0`. Use the default language version for the target (C# 14); do not set `LangVersion`. Update dependencies to the latest stable versions compatible with .NET 10 (the test project currently has very old packages, e.g. `Microsoft.AspNetCore.Mvc.Testing` 6.0.x).

**Why.**
- .NET 8 reached end of support on 2026-11-10, so staying on it means no security patches.
- .NET 10 is the current LTS (supported to 2028-11-14). .NET 11 is newer but is STS and less likely to be installed on a reviewer's machine.
- Not setting `LangVersion` avoids accidentally requiring a newer SDK; `latest` would resolve to C# 15 on a machine with the .NET 11 SDK and could break a reviewer with only .NET 10.

**Trade-off / risk.** The reviewer needs the .NET 10 SDK (or newer) to build. State this in the README.

**Rejected.** Staying on net8.0 (matches the template, but end-of-life); net11.0 (STS, newest, least likely to be installed).

---

## ADR-5: Scalar for API documentation, replacing Swashbuckle

**Decision.** Use `Microsoft.AspNetCore.OpenApi` to generate the OpenAPI document and `Scalar.AspNetCore` as the UI, in the Development environment only. Remove Swashbuckle. The UI is served at `/scalar/v1`.

**Why.**
- Microsoft's own generator is built into the framework and is the direction of travel; Swashbuckle was dropped from the default templates.
- Scalar has a more modern UI with a good "try it" panel, useful for demonstrating the API.
- The template's Swashbuckle (6.2.3) is the out-of-date part of the existing setup anyway.

**Follow-up.** Declare the bearer security scheme in the OpenAPI document so Scalar can accept a token (see ADR-7); update `launchUrl` in `launchSettings.json`.

**Sample values belong to Scalar, not to the API.** The "Test request" panel is pre-filled with a valid payment, but the API's own document (`/openapi/v1.json`) carries no examples: a second document, `scalar`, adds them (`PaymentRequestExampleTransformer`) and only the Scalar page reads it (`WithOpenApiRoutePattern`). **Debug builds only (`#if DEBUG`), like the development token (ADR-7):** a Release build has neither the second document nor the transformer, and Scalar reads the plain `v1` document. *Why:* the OpenAPI document is the API's published contract, and sample card numbers are a convenience of one UI, not part of it. Tests pin both halves.

---

# Behaviour of the payment flow

## ADR-8: Validation and error handling

**Decision.**
- Validation rules are written as FluentValidation validators, auto-registered by assembly scanning, and called explicitly from `PaymentService` before any bank call.
- Any invalid information produces a `Rejected` response (HTTP 400); the bank is never called and **no payment is created or stored** (the brief: "No payment could be created"). This includes missing fields, wrong-typed values and unparseable JSON, so binding must be lenient (nullable/string request fields) and/or binding failures mapped to `Rejected`, rather than the framework's default 400.
- Rules are the brief's, with **one deliberate addition (decided after the code review, 2026-11-15): the amount must be greater than zero.** The brief only says the amount is an integer in minor units, and this ADR first left it at that. But a zero or negative amount is never a real payment: it would be sent to the bank, stored as `Authorized`, written to the ledger as a negative entry, and a reversal of it would then add money. So `0` and every negative value are `Rejected` (400, field `Amount`, message "Amount must be greater than zero.", never echoing the value) and the bank is not called. A missing amount is still "Amount is required.".
- Numeric request fields (`ExpiryMonth`, `ExpiryYear`, `Amount`) are nullable (`int?`) so that a missing field can be told from a zero and rejected as "required".
- Expiry: a card is valid through the last moment of its expiry month, so the month+year is "in the future" while that moment is after the current time (the current month is valid, last month is not). The year must be 1 to 9999 (the bank format is `YYYY` and `DateTime` cannot represent more).
- Currency must match the allowlist exactly, in upper case (the code is passed on to the bank unchanged).
- Validation messages name the field and the rule and never contain the submitted value, so card data cannot leak through error responses (ADR-17).
- Use a `Result<T>` type for business outcomes; no exceptions for flow control.
- Always pass `CancellationToken` to async methods.

**Rules (from the brief).** Card number 14–19 numeric characters; expiry month 1–12 and the month+year must be in the future; currency 3 characters from an allowlist of at most 3 ISO codes (e.g. USD, GBP, EUR); amount is an integer in minor units and greater than zero (our addition to the brief, see above); CVV 3–4 numeric characters. Expiry checks should use an injected `TimeProvider` so they are testable.

**Why.** Keeping validation in plain, separately testable validators meets the "automated tests" expectation cheaply, and calling them from the service (not a pipeline behaviour) keeps the flow visible without Mediator (ADR-1).

---

## ADR-9: Bank integration and card data handling

**Decision.**
- Call the bank simulator (`POST http://localhost:8080/payments`) through an `IBankClient` interface implemented as a typed `HttpClient` obtained via `IHttpClientFactory`.
- Map the bank's responses: `authorized: true` -> `Authorized`; `authorized: false` -> `Declined`. A 503 (card number ending in 0), timeout or other transport failure is a bank error, **not** a decline: report an error to the merchant (502/503) and do not store a payment.
- Base URL comes from configuration; set an explicit `HttpClient` timeout; no automatic retries of a payment POST (retrying is the merchant's job, made safe by idempotency keys, ADR-21). Send `expiry_date` as zero-padded `MM/YYYY`.
- Store and return only the last four digits of the card number. Never store or return the CVV or the full card number, and keep them out of logs. Card number, CVV and last-four are strings (not ints, as in the template), so leading zeros are preserved, matching the brief's "characters" rules.
- C# records generate a `ToString()` that prints every property, so request types holding the card number/CVV override `ToString()` to redact, and request objects are never logged.
- A global exception handler returns a generic error without internals.
- Amounts are integers in minor units.

**Why.** The interface lets the service be tested with a substitute bank; separating a decline (a valid business outcome) from a bank failure (an operational error) avoids telling a merchant a card was declined when the bank was simply unavailable; the card-data rules are compliance requirements (PCI DSS) and the brief.

---

## ADR-16: Async/await conventions

**Decision.**
- **Async all the way for I/O, synchronous for pure computation.** The bank client's `HttpClient` call, the services that call it, the Minimal API handlers and the repository interface are async. Validators, mapping classes (ADR-14) and metrics recording do no I/O and stay synchronous; they are not wrapped in `Task`.
- **Never block on async code:** no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`.
- **Never `async void`**, except framework-mandated event handlers; return `Task`.
- **Cancellation:** always accept a `CancellationToken` and forward it to every awaited call. Minimal API handlers declare it as a parameter and the framework supplies the request-aborted token. The bank client sets an explicit `HttpClient` timeout; a timeout is a bank error (ADR-9), not a decline.
- **Naming and types:** async methods end in `Async`; default to `Task<T>` and use `ValueTask` only if profiling shows a need; no `ConfigureAwait(false)` (ASP.NET Core has no synchronisation context); no `Task.Run` in the request path; no fire-and-forget work; never hold a `lock` across an `await`.
- **Repository:** `IPaymentsRepository` returns `Task<T>`, and the in-memory implementation completes synchronously with `Task.FromResult`. This is the one deliberate departure from "do not make non-I/O code async".
- **Tests:** test methods are `async Task`, never `async void` and never block; pass `TestContext.Current.CancellationToken` (xUnit v3; the xUnit1051 analyzer enforces it) unless cancellation is the subject, in which case use a dedicated `CancellationTokenSource`, and include one cancellation test for the bank client. Substituted async calls (NSubstitute) return completed tasks.
- **Enforcement:** the built-in .NET analyzers (for example CA2016, forward the cancellation token) and xUnit's analyzers catch most violations at compile time; `.editorconfig` cannot be changed under the challenge rules, so the rest is by convention and review.

**Why.**
- Blocking on async code ties up request threads and risks deadlocks and thread-pool starvation, which for a payment gateway means latency and outages under load; async all the way avoids that, and the bank round trip is the one genuinely slow operation here.
- Keeping CPU-only code synchronous avoids needless state machines and allocations and keeps the code simpler (KISS, ADR-1); making it async would suggest I/O that does not exist.
- Forwarding the cancellation token stops work for clients that have gone away and enforces the bank timeout, and distinguishing a timeout from a decline keeps merchants and metrics honest (ADR-9, ADR-15).
- The repository interface models storage, and any real store is I/O. Making it return `Task<T>` now means swapping in a database later does not change every caller, at the cost of a trivial `Task.FromResult` today.
- `ConfigureAwait(false)`, `ValueTask` and `Task.Run` add complexity and risk with no benefit in an ASP.NET Core service of this size.

**Trade-off.** An async repository interface over an in-memory implementation is slightly more ceremony than a synchronous one; accepted for a stable abstraction.

**Rejected.** Synchronous repository interface (breaks callers when real storage arrives); blocking calls to bridge sync and async; `ConfigureAwait(false)` everywhere; `ValueTask` by default.

---

## ADR-17: Error and exception handling

**Decision.** Expected outcomes are values; exceptions are for the unexpected.

| Situation | Handling |
|---|---|
| Invalid request (any validation failure, malformed input) | `Result` failure -> `Rejected`, HTTP 400, nothing stored |
| Bank declined | A normal result: status `Declined` |
| Bank unavailable (503, timeout, network failure) | `Result` failure -> HTTP 502/503 with `ProblemDetails`, nothing stored |
| Payment id not found | Empty result -> 404 |
| A bug or an unforeseen failure | An exception, caught by the global handler -> 500 |
| Startup misconfiguration | An exception at startup (`ValidateOnStart`), which is the right behaviour |

- **Catching.** Exceptions are caught only at the boundary that can fail, and only specific types: the bank client catches `HttpRequestException`, a timeout and `JsonException` (malformed reply) and converts each into a bank-error `Result`. No `catch (Exception)` in business code, no swallowed exceptions, and no `try/catch` in services or endpoints. An `OperationCanceledException` caused by our timeout is a bank error; one caused by the caller abandoning the request is not, so it propagates and is not logged as an error.
- **Global handler.** `AddExceptionHandler<T>()` with an `IExceptionHandler`, `AddProblemDetails()` and `UseExceptionHandler()`. Unhandled errors return a generic RFC 9457 `ProblemDetails` 500 with the `traceId` (ADR-11) and no stack trace, exception message or internals. The exception is logged once, in the handler, through Serilog; other layers do not catch, log and rethrow. Implemented as `UnhandledExceptionHandler`, registered after `BadRequestExceptionHandler`; it logs through `ILogger<T>` and ignores caller cancellation. The framework default already hides internals, so the test also pins our generic title.
- **No sensitive data in errors.** Card data and request values never go into exception messages. `Rejected` responses list field names and messages but never echo the submitted values, in particular the card number or CVV. (The brief does not specify the `Rejected` body; recorded as an assumption.)
- **Mechanics.** Rethrow with `throw;`, never `throw ex;`; pass the original as the inner exception when wrapping; `ArgumentNullException.ThrowIfNull`-style guards are for programmer errors (constructor arguments), not user input.
- **`Result<T>`.** Hand-written and simple: a success value or a failure carrying a small error type (for example `ValidationFailed(errors)`, `BankUnavailable`). No third-party result library.
- **What merchants see.** `Rejected`: the brief's payload at HTTP 400. Bank error: 502/503 `ProblemDetails`. Unhandled: 500 `ProblemDetails`. 401/403: the framework's defaults.
- **No automatic retries** (ADR-9).
- **Tests.** Unit tests assert on `Result` values, not thrown exceptions. The bank client has one test per mapped failure: 503, timeout, network failure, malformed JSON and caller cancellation. One integration test makes an endpoint throw and checks for a 500 `ProblemDetails` with a `traceId` and no stack trace or message. The Rejected and bank-error paths are covered by the required integration tests (ADR-6).

**Why.**
- Validation failures, declines and bank outages happen routinely in a payment gateway. Modelling them as values makes the flow visible in the code, avoids the cost and obscurity of exception-driven control flow, and keeps the mapping to HTTP statuses explicit and testable.
- Catching only specific exceptions at the one boundary that can fail avoids hiding bugs; a bare `catch (Exception)` in business code turns programming errors into silent wrong behaviour, which is unacceptable when handling money.
- Distinguishing our timeout from caller cancellation keeps merchants, logs and metrics honest (a bank timeout is an operational problem; a client hanging up is not).
- A single global handler gives one consistent, safe error shape and one place where exceptions are logged, with the `traceId` linking the merchant's report to the log entry; hiding stack traces and messages denies attackers information and avoids leaking card data.
- Failing fast on misconfiguration surfaces deployment mistakes at startup instead of at the first payment.

**Rejected.** Exceptions for flow control (throwing a `ValidationException` and catching it in the endpoint); `catch (Exception)` in business code; catch-log-rethrow in every layer; third-party result libraries; automatic retries; echoing submitted values in error responses.

---

# Security and operations

## ADR-7: Authentication – JWT bearer with scope policies, dev tokens via `dotnet user-jwts`

> **Status: IMPLEMENTED, except that the `payments:reverse` scope and the reversal endpoint it protects are NOT implemented (ADR-23), and the production design (identity provider, mTLS) is not built.**

**Decision.** Protect the endpoints with `AddJwtBearer`:
- `Authority` / `Audience` / issuer come from configuration (in production, the identity provider's metadata).
- One authorisation policy per scope: `payments:create` (POST payment), `payments:read` (GET payment and transactions) and `payments:reverse` (POST reversal, ADR-23). No/invalid token -> 401; insufficient scope -> 403.
- For development and review, tokens are produced by the .NET SDK tool `dotnet user-jwts create --scope payments:create --scope payments:read --scope payments:reverse` (Development only; the signing key lives in user secrets).
- Rules: never write custom token-issuing code, no setting that disables authentication, no hard-coded tokens. **One exception, for ease of testing only (amended 2026-11-16):** in the Development environment and **only in a Debug build** (`#if DEBUG`; the file does not exist in a Release build, and a test proves it), the Scalar page is pre-loaded with a development token so "Authorize" is already filled in. It is signed with the key `dotnet user-jwts` already put in the developer's user secrets, with the same claims as `scripts/dev-token.ps1` (8-hour lifetime), so nothing new is trusted; if no key exists yet, nothing is issued. It is `DevelopmentScalarToken` in `Api/Documentation/`. It is never part of a deployed service: a Release build contains none of it, and the documentation endpoints are only mapped in Development. Note that the page, which anyone who can reach a Development instance can open, therefore contains a token; that is acceptable only because the instance is a local development one.
- All endpoints are served over HTTPS (HTTPS redirection and HSTS), so data is encrypted in transit. Nothing sensitive is persisted (only the last four digits are kept, and never the CVV), so there is currently nothing that needs encrypting at rest. To keep the option open, every store (payments, transactions, audit trail, idempotency records) sits behind an interface (ADR-13), so an encrypting implementation (or a decorator) can be substituted without touching callers; a production store must encrypt at rest with separately held keys.
- Inbound authentication is independent of the bank call: the bank client must never forward the merchant's token.

**Production design (documented, not built).** Server-to-server merchants authenticate with OAuth 2.0 client credentials and short-lived JWTs issued by a dedicated identity provider (Entra ID, Auth0, Keycloak, Duende). Optional mTLS with certificate-bound tokens for high-assurance merchants; API keys only as a lighter sandbox alternative (hashed at rest, scoped, rotatable). Around it: TLS 1.2+, per-merchant authorisation (a merchant may only read its own payments), secrets in a vault, rate limiting, audit logging without PAN/CVV, and no CVV storage.

**Why.**
- This is a financial system handling sensitive data; real token validation (signature, issuer, audience, expiry) and least-privilege scopes show how it would be done properly.
- The brief does not require authentication, and a full identity provider would be far more than the exercise needs. `user-jwts` is the compromise: genuine validation in the code, but the reviewer can obtain a token with one command, with no custom issuing code.
- Hand-rolling a token endpoint would look secure without being secure, and an on/off switch for authentication is the wrong thing to show in this domain.

**Authentication is built; metrics (ADR-15), idempotency (ADR-21), auditing (ADR-22) and reversals (ADR-23) are designed but NOT implemented. All five are deliberate departures from the brief** (which asks for none of them); everywhere else the brief wins.

**Two identities per request: who originated it, and on whose behalf.** Every authenticated request carries both, taken from the validated token and never from the request body:
- **Originator (`InitiatedBy`)**: the authenticated principal that made the call: the token's `sub` claim (a human user's id, or a machine client's id).
- **On behalf of (`OnBehalfOf`, the merchant account)**: the merchant the operation is performed for: the `merchant_id` claim, falling back to the client id when the token has none (a merchant client acting for itself). The two are equal when a merchant calls for itself and differ when someone acts for a merchant (for example a support user or a platform). In production the identity provider issues this delegation (for example OAuth 2.0 token exchange, RFC 8693, whose `act` claim records the actor); the gateway trusts a validated token and does not decide who may act for whom. `dotnet user-jwts` can add the claims for dev tokens (`--claim merchant_id=…`).
- Both identities are recorded on every audit event (ADR-22) and on every ledger transaction (ADR-23).

**Per-merchant ownership is built (changed from an earlier "known gap").** The `OnBehalfOf` merchant id is stored as `MerchantId` on each payment and transaction, and every endpoint that reads or changes a payment (GET payment, GET transactions, POST reversal) returns 404 for a payment that belongs to another merchant. *Why the change:* reversals (ADR-23) move money, so letting any caller with a scope reverse or read any payment by id (broken object-level authorisation, the top OWASP API risk) is no longer acceptable. Also note that scopes arrive in the space-separated `scope` claim, so the policies need a small custom requirement rather than `RequireClaim`.

**Rejected.** Omitting auth entirely (documented only); a custom dev token endpoint (the Scalar pre-load above is not an endpoint: nothing can request a token from a running service); a disable switch; a static API key in config; a hard-coded token in the README.

---

## ADR-11: Logging, correlation and request limits

**Decision.**
- Use **Serilog** (`Serilog.AspNetCore`) as the logging framework, plugged in behind the standard `ILogger<T>` abstraction. Application code depends only on `ILogger<T>`, never on Serilog types, so the framework is a composition-root choice that can be swapped without touching business code.
- Serilog is configured from `appsettings` (`Serilog.Settings.Configuration`) with a console sink writing JSON (compact JSON formatter), set up in an `Add…` extension method (ADR-12). Enrich events with the request's trace id (verify the enrichment works when implementing).
- Use structured message templates (named placeholders such as `{PaymentId}`, never string concatenation or interpolation), so each property is captured as a queryable field.
- Sensitive data must never reach a log event: log scalar facts only (ids, status, outcome), never whole request objects; if an object is ever logged, a Serilog destructuring policy must exclude the card number and CVV (a backstop for the redacted `ToString()` rule in ADR-9).
- Log only non-sensitive facts: payment id, resulting status, and the bank outcome (authorized / declined / bank error). Never log request bodies, the card number, CVV or tokens.
- Use ASP.NET Core's default `traceId` (from `Activity` / `HttpContext.TraceIdentifier`) as the correlation id: it is attached to log scopes automatically and included in error (ProblemDetails) responses. No custom correlation middleware or `X-Correlation-Id` header.
- Cap the request body size (Kestrel `MaxRequestBodySize`, a few KB: a payment request is a few hundred bytes). Oversized bodies get the framework's 413, which is outside the brief's `Rejected` rules and is documented as such.
- Rate limiting is not built (documented gap; see below).

**Why Serilog.**
- It is the most popular dedicated structured logging framework in .NET (about 3.6 billion NuGet downloads; `Serilog.AspNetCore` about 869 million, against about 662 million for NLog, its nearest rival, and log4net as the older text-oriented option), so it is the library most .NET engineers and reviewers already know.
- It is built around structured events: message-template properties become named fields, which is what makes payment logs searchable by payment id or status instead of grepped as text.
- Enrichment attaches context (such as the trace id) to every event without repeating it at each call site.
- Destructuring policies give a central place to stop sensitive properties being written, which suits a card-handling service (ADR-9) better than relying only on discipline at each call site.
- Its sink ecosystem (console, file, Seq, Elasticsearch, Application Insights, OpenTelemetry) means that moving from the console sink here to a real log backend in production is a configuration change.
- It plugs in behind `ILogger<T>`, so choosing it costs nothing in the business code and does not conflict with ADR-12 (we never wrap `ILogger`).

**Trade-off.** Compared with the built-in providers alone it adds packages and some configuration, and with no log backend in this exercise its largest advantage (sinks) is not exercised; the structured-event model, enrichment and destructuring policies still apply. This was a deliberate choice for a production-style setup.

**Alternatives considered.** The built-in `Microsoft.Extensions.Logging` providers alone (structured templates and a JSON console formatter, no extra dependency; simplest, and the fallback if the dependency is not wanted); NLog (mature and configurable, less common for new .NET work); log4net (older and mainly text-based, not chosen for new code).

**Why the other decisions here.**
- A payments service must be diagnosable without exposing card data. Structured logs keyed by payment id and status allow tracing a payment without ever recording sensitive fields, and the card-data rules in ADR-9 make the "what not to log" list explicit.
- The framework already provides a per-request trace id, so a correlation id costs nothing; custom middleware would add code and a header contract the brief does not ask for (KISS).
- A body-size limit is a one-line configuration that blocks trivially large payloads from an unauthenticated or abusive client; it is the cheapest of the abuse protections.
- Rate limiting (`AddRateLimiter`) needs policy choices (per client, per scope, limits) that cannot be justified without real traffic, and the brief does not ask for it, so it stays a documented production gap rather than guessed-at code.

**Rejected.** Custom correlation middleware / headers; request/response body logging (leaks card data); building rate limiting now.

---

## ADR-15: Metrics with OpenTelemetry

> **Status: PARTLY IMPLEMENTED. Only the seam is built: `IPaymentMetrics`, which the bank client calls, backed by the no-op placeholder `NoOpPaymentMetrics`. The meter, the instruments, the OpenTelemetry setup and the exporters below are a design only and are NOT implemented.**

**Decision.** Metrics are a requirement. Use `System.Diagnostics.Metrics` to define the instruments and OpenTelemetry (metrics signal only) to collect and export them.
- One `Meter`, named `PaymentGateway.Api`, wrapped in an `IPaymentMetrics` interface (in `DataInterface/`, ADR-13) implemented in `DataLayer/`, so services depend on the abstraction and tests can substitute it (ADR-6).
- Custom instruments:
  - `payments.processed` – counter, tag `status` = `authorized` | `declined` | `rejected`.
  - `bank.requests` – counter, tag `outcome` = `authorized` | `declined` | `error` (bank 503, timeout or other transport failure).
  - `bank.request.duration` – histogram, unit seconds.
  - `payments.reversed` – counter, incremented for each completed reversal (ADR-23).
  - `idempotency.requests` – counter, tag `outcome` = `replayed` | `conflict` (ADR-21).
- Built-in instrumentation is exported too: ASP.NET Core server metrics (request count, duration, active requests, failures) and `HttpClient` metrics for the bank calls, via the OpenTelemetry ASP.NET Core and HTTP instrumentation packages.
- Exporters: console exporter by default (visible to a reviewer with no infrastructure); OTLP exporter enabled when the standard `OTEL_EXPORTER_OTLP_ENDPOINT` setting is present, so pointing at a real collector or backend is configuration only.
- Setup lives in an `Add…` extension method (ADR-12). Only the metrics signal is wired through OpenTelemetry; logging remains Serilog (ADR-11) and tracing is out of scope.
- **Tag rules:** tags must be low-cardinality (a handful of fixed values) and must never contain card data, payment ids, merchant ids or tokens, in line with ADR-9 and ADR-11.
- **Tests:** unit-test `PaymentMetrics` first (ADR-13) and cover the metrics in the integration tests (ADR-6): a POST that is authorized, declined, rejected or hits a bank error increments the right counter with the right tag. Use a `MetricCollector` (`Microsoft.Extensions.Diagnostics.Testing`) or a `MeterListener` to read recorded measurements.

**Why.**
- A payments service has to be observable in production: rate, latency and failure ratio of payments and of the acquiring bank are the first things an operator looks at (for example a rising `bank.requests{outcome=error}` or a falling authorised share). Logs show individual events; metrics show trends and drive alerts.
- `System.Diagnostics.Metrics` is built into .NET, and ASP.NET Core and `HttpClient` already emit metrics through it, so the incremental cost is a few custom instruments.
- OpenTelemetry is the vendor-neutral standard: the code defines instruments once, and the choice of backend (Prometheus, Grafana, Datadog, Azure Monitor and others) is configuration, not code, which mirrors how Serilog sits behind `ILogger`.
- Separating "declined" (a valid business outcome) from bank "error" (an operational failure) in the metrics mirrors ADR-9, so dashboards and alerts are not misled.
- Low-cardinality, non-sensitive tags protect both the metrics backend (unbounded tags are costly) and cardholder data (PCI).

**Trade-off.** The brief does not ask for metrics, and this adds several packages and a small amount of setup, which sits against KISS (ADR-1). It is accepted as a deliberate departure from the brief for the observability a real payments service needs; the scope is kept small (three instruments, metrics only).

**Alternatives considered.** Documenting metrics only as a production gap (simplest, and consistent with "the brief wins", but nothing demonstrable); prometheus-net (simple but tied to Prometheus); the Application Insights SDK (vendor-specific); App.Metrics (no longer maintained); OpenTelemetry for logs and traces as well (more scope than needed here).

---

# Organisation, layers and naming

## ADR-27: Layers: Api, BusinessLogic, DataInterface and DataLayer, and Entity-suffixed entities

**Decision.** Requirements added 2026-11-16 on top of ADR-1 and ADR-26. The single project is organised into five layers by top-level folder (namespaces follow the folders):
- `Api/`: the endpoints (thin), plus the HTTP-only concerns (security, error handling, OpenAPI, request-limit options).
- `ApiInterface/`: the API's contract, the counterpart of `DataInterface/`: the request and response DTOs (`Dtos/Requests`, `Dtos/Responses`).
- `BusinessLogic/`: services, validation, mapping, the shared outcomes (`Result`, `Error`), in-flight models, and the service interfaces.
- `DataInterface/`: the interfaces for everything the business logic needs from outside itself (stores, the bank, the ledger, the audit trail, idempotency, metrics) plus the entities, enums and message records in their signatures.
- `DataLayer/`: the implementations of those interfaces (the in-memory repository, the bank client, and the metrics placeholder).
Entity types are named `[Thing]Entity` (`PaymentEntity`, `TransactionEntity`, `AuditEventEntity`) and live in `DataInterface/Entities/`. **Names show the role:** DTOs end in `Request` or `Response` (including the bank's messages), entities in `Entity`, in-flight business models in `Model` (`ProcessedPaymentModel`); there is deliberately no `Dto` suffix, since `Request`/`Response` already marks a DTO and the doubled name adds nothing (best practice: name the role, not the pattern). `Unit/Architecture/NamingConventionTests` enforces it. The composition root (`PaymentGatewayDi.cs`, called from `Program.cs`) is the only code that sees all layers.

**DTOs are the API's interface (amended 2026-11-16).** The request and response DTOs are the contract between the API and its callers, the counterpart of `DataInterface` on the data side, so they have a layer of their own, `ApiInterface/Dtos/Requests|Responses`; the bank's messages, part of `IBankClient`, are `DataInterface/Dtos/Requests|Responses`. The first version of this ADR kept the API DTOs next to the endpoints (`Api/Models`), which forced `BusinessLogic` to depend on `Api`, the one place a lower layer reached up; an intermediate version put them in `BusinessLogic/Interfaces/Dtos`, which hid the fact that the endpoints and the services both use them and neither owns them. In `ApiInterface` both depend on it and it depends on almost nothing (`Shared` and the `DataInterface` enums). A request or a response is a DTO whose role is marked by the `Request`/`Response` suffix instead of a `Dto` postfix, so every `…Request` lives in a `Dtos/Requests` folder and every `…Response` in a `Dtos/Responses` folder, and nothing else lives in them (`NamingConventionTests`).

**Dependency rules.** `Api` -> `BusinessLogic` and `ApiInterface` (and the `DataInterface` enums); `BusinessLogic` -> `ApiInterface` and `DataInterface`, never `Api` or `DataLayer`; `ApiInterface` -> the `DataInterface` enums and `Shared` only; `DataLayer` -> `DataInterface`; `DataInterface` -> nothing of ours above it. `Shared` (`Result`, `Error`, `ErrorKind`, `FieldError`, in `Shared/Outcomes`) is the small cross-layer vocabulary that `IBankClient` and every service return; it depends on nothing of ours, so every layer may use it and there is no exception to the one-way rule. These are enforced by `Unit/Architecture/LayerDependencyTests` (NetArchTest.Rules), which also checks that everything in `Entities/` ends in `Entity`.

**Why.**
- The layers say where each kind of code lives and what it may know about, so a reader can find things quickly and a change in one layer (a new database, a new endpoint style) does not ripple through the others.
- `DataInterface` as a separate, lowest layer means the business logic depends on abstractions it owns the shape of, and `DataLayer` can be replaced (a real database, an encrypting store, ADR-7) without touching the business logic.
- Keeping the endpoints and their DTOs together makes the public contract one folder to review.
- The `Entity` suffix makes it impossible to confuse a stored type with a DTO in code review (ADR-26).
- An architecture test turns the rules from convention into something that fails the build.

**Namespaces.** The project's `RootNamespace` is `PaymentGateway` (the assembly is still `PaymentGateway.Api`), so the folder `Api/Endpoints` is the namespace `PaymentGateway.Api.Endpoints` and `DataLayer/Bank` is `PaymentGateway.DataLayer.Bank`, with no repeated segment (an earlier layout produced `PaymentGateway.Api.Api.…`). The six top-level namespaces are `PaymentGateway.Api`, `.ApiInterface`, `.BusinessLogic`, `.DataInterface`, `.DataLayer` and `.Shared`. A file's namespace is exactly that root plus its folder path in both projects (the test project's root is `PaymentGateway.Api.Tests`), and `Unit/Architecture/NamespaceFolderTests` checks it for every file, so a moved file cannot keep a stale namespace.

**Trade-off.** More folders; one project instead of four, so the compiler does not enforce the layering, which is why the architecture tests do. The business logic uses the request and response DTOs as its contract instead of having its own copies, to avoid a second set of identical records and mappings (KISS, ADR-1).

**Possible evolution: separate projects, then separate services (depends on project complexity).** Ideally the contract is kept apart from both what implements it and what consumes it. The layers are drawn along exactly that line, so they can be pulled apart later without redesign:
- **Contracts:** `ApiInterface` (the API's DTOs) and `DataInterface` (the store, bank, ledger, audit, idempotency and metrics interfaces, with their entities and bank DTOs) become their own projects or packages. They depend on nothing but `Shared`.
- **Implementers** depend on a contract, never on each other: `DataLayer` implements `DataInterface`; `BusinessLogic` and `Api` implement and expose `ApiInterface`. **Consumers** reference only the contract, never an implementation (a client of the API needs only `ApiInterface`; the business logic needs only `DataInterface`).
- **What that buys:** the compiler enforces the layering (the architecture tests then become a safety net), contracts can be versioned and released independently, and a concern can move behind its interface into a service of its own. The natural first candidates are the bank client, and the ledger with the audit trail and idempotency store, which have different scaling, availability and compliance needs from the request path (isolating card handling also narrows the PCI scope).
- **What it costs:** more projects and references, network calls instead of method calls, and the loss of in-process atomicity. A payment, its ledger entry and its audit event would no longer be one in-memory step, so consistency would need an outbox or a saga, idempotent consumers, and versioned contracts.
- **When:** it is not worth it for this exercise (two endpoints, one team, in-memory stores), which is why it is one project. It becomes worth it with several teams, different release cadences or scaling needs per concern, or contracts shared with other services. The folder rules keep the option open: the interface folders depend on no implementation, no layer reaches up, and the DTOs are not tied to the endpoints.

**Rejected (for now).** Separate projects per layer, and microservices (stronger enforcement and independence but heavier: more project files, references and moving parts than this exercise warrants; the architecture tests give most of the benefit, and see "Possible evolution" above); a separate set of domain models duplicating the DTOs; leaving the layout flat (the status quo before this ADR).

---

## ADR-26: DTOs and storage entities are separate, and storage holds no business logic

**Decision.** Two requirements (added 2026-11-16):
- **DTOs and storage entities are different types.** API request and response types (DTOs) and the bank's message types are never stored. Each store persists its own entity record, mapped to and from the DTOs by hand-written `IMapping` classes (ADR-14). For payments the entity is `PaymentEntity` (id, owning merchant, status, last four card digits, expiry, currency, amount); the flow is `PostPaymentRequest` -> `ProcessedPaymentModel` (in flight, holds the full card number, never stored) -> `PaymentEntity` (stored) -> `PostPaymentResponse` (returned). The entity never holds the full card number or the CVV.
- **Storage holds no business logic.** A store (`IPaymentsRepository`, and equally `ITransactionLedger`, `IAuditTrail`, `IIdempotencyStore`) adds, reads by key and, where only the store can guarantee it, performs an atomic primitive (a uniqueness check-and-append, a reserve). It does not know who is asking, apply ownership or status rules, decide retention or build responses. Its interface takes no caller identity. Those decisions belong to the service: `PaymentService.GetAsync` reads the entity and compares its `MerchantId` with `ICallerIdentity`, answering "not found" for another merchant's payment.

**Why.**
- Decoupled shapes: the public API contract and the storage shape change for different reasons (a new database column is not an API change, and vice versa). Sharing one type ties them together and makes it easy to leak a stored field in a response.
- Testability and replaceability: with no rules inside it, a store is trivial to test and to replace (a database, an encrypting store, ADR-7); the rules stay in one place, the service, where they are tested without any storage.
- Safety: ownership checked in the service cannot be lost by swapping the store, and a store that cannot filter by caller cannot be misused to bypass the check.

**Trade-off.** One more record and one more mapping class (and one more constructor parameter on `PaymentService`, which now has eight, an accepted exception for a DI constructor). Atomic primitives such as the ledger's check-and-append and the idempotency reserve are allowed in a store because only the store can make them atomic; they enforce uniqueness, not business policy.

**Rejected.** Storing the response DTO directly (what the first version did: simple, but couples the contract to storage); a repository method that takes a merchant id and filters (ownership inside storage); returning a response from the repository.

**If the unbuilt stores were added.** The ledger, idempotency store and audit trail (ADR-21, 22, 23: NOT implemented) would have to follow this rule too: ownership and expiry decided by the service, not by the store.

---

## ADR-14: Hand-written mapping classes behind a generic interface

**Decision.**
- Where a mapping between two types is needed, write a mapping class by hand. Do not use AutoMapper or any `IMapper`/convention-based mapping library.
- Every mapping class implements one generic interface, kept in `BusinessLogic/Interfaces/`:
  ```csharp
  public interface IMapping<in TSource, out TDestination>
  {
      TDestination Map(TSource source);
  }
  ```
  The name `IMapping` is deliberate, so it is never confused with AutoMapper's `IMapper`.
- Mapping classes live in `Mapping/`, one class per mapping, named for what they map (e.g. `PaymentRequestToBankRequestMapping : IMapping<PostPaymentRequest, BankRequest>`). Consumers depend on `IMapping<TSource, TDestination>`, and the interface-to-implementation registration is in an `Add…` extension method (ADR-12).
- A mapping class contains logic, so it falls under ADR-13: tests first, in `Unit/Mapping/`.
- Do not create a mapping class for a trivial one-line conversion; the rule applies where a real mapping between distinct types is needed (for this project: request -> bank request, bank response -> payment outcome, stored payment -> response).

**Why.**
- Hand-written mappings are explicit and traceable: every field assignment is visible and a rename is a compiler error, whereas convention-based mapping fails at runtime or silently skips properties. That matters for a payments API, where mapping the wrong field (or leaking the card number or CVV into a response) is a compliance problem.
- No extra dependency or reflection, and no configuration to maintain or validate.
- A single generic interface gives every mapper the same shape, so they are discoverable, injectable and substitutable in tests (ADR-6) without a mapping framework, and the registration pattern stays uniform (ADR-12).
- The `IMapping` name avoids confusion with AutoMapper's `IMapper`, which the project does not use.

**Trade-off.** More hand-written code than a convention mapper, which we accept for explicitness and safety; the "no trivial mapping class" guard keeps it from growing ceremony (KISS).

**Rejected.** AutoMapper or other mapping libraries; mapping inside endpoints or services (it hides the field-by-field logic and is harder to test); extension methods without an interface (not substitutable or uniformly registered).

---

## ADR-12: Dependency injection wiring (production and tests)

**Decision (production).**
- One composition root: `PaymentGatewayDi.cs`, a single static class at the project root, is the only place that knows concrete types (`Program.cs` calls it once). Everything else uses constructor injection of interfaces; no service locator (`IServiceProvider.GetService` in business code) and no static access.
- Every mapping from an interface to its concrete implementation is registered in a static extension method on `IServiceCollection` (`public static IServiceCollection Add…(this IServiceCollection services, …)`) in that one class, as an internal static method per concern, e.g. `AddPaymentServices()`, `AddBankClient(configuration)`, `AddApiAuthentication()`, which `InitialiseDi(configuration)` (the one public method, first in the class: the root DI initialisation) chains in order (amended 2026-11-16: the `DependencyInjection/` folder of eight small classes became one class, so all the wiring reads top to bottom in one file; the per-concern methods are internal, visible to the test project, so a test can register just the part it needs). There are no inline `services.AddX<I, T>()` calls in `Program.cs`, endpoints or tests: `Program.cs` only calls `InitialiseDi`, so it stays short, there is one place to look up what implements an interface, and tests reuse the same registrations. Each method returns the `IServiceCollection` so calls chain, and takes the configuration only when it needs it.
- Lifetimes are chosen deliberately: singleton for thread-safe/stateless things (the in-memory repository, `TimeProvider.System`); scoped for per-request state; transient for cheap stateless services (`PaymentService` may be scoped or transient). Never inject a scoped service into a singleton.
- The bank client is a typed `HttpClient` via `AddHttpClient<IBankClient, BankClient>()`, never `new HttpClient()`; base address and timeout come from configuration.
- Configuration uses the options pattern (`BankOptions` bound with `ValidateDataAnnotations().ValidateOnStart()`); inject `IOptions<T>`, not `IConfiguration`.
- Wiring mistakes fail fast: keep `ValidateOnBuild` / `ValidateScopes` enabled (the Development default).
- Validators are registered by assembly scanning (`AddValidatorsFromAssemblyContaining<…>()`).
- Every non-trivial class has an interface, and data types without logic do not (ADR-13). Do not wrap `ILogger`, `TimeProvider` or `IOptions` in our own interfaces.

**Decision (tests).**
- Unit tests do not use the container: construct the class under test directly, with an NSubstitute substitute for `IBankClient`. A test that needs the container is an integration test, or the class has too many dependencies.
- Integration tests use `WebApplicationFactory<Program>` with the real container and override only the edges in `ConfigureTestServices`: `IBankClient` (an NSubstitute substitute), `TimeProvider` (`FakeTimeProvider`) and authentication (test-signed JWTs or a test authentication handler, so 401/403 are testable). Everything inside the app stays real.
- Test setup reuses the production extension methods and never re-registers the app's internals by hand.
- Isolation: a fresh factory (or repository) per test class so singleton state cannot leak; `IClassFixture<T>` to share one factory within a class.
- `Program` is made visible to tests. `Microsoft.AspNetCore.Mvc.Testing` exposes the internal top-level `Program` to the test assembly automatically (verified when the tests were written); add `public partial class Program;` only if that stops working.
- Use NSubstitute for test doubles in unit tests, for any collaborator where that makes the test clearer (ADR-6); use a hand-written test double only where mocking would be unduly complex and the double is clearly more readable.

**Why.**
- A single composition root and constructor injection keep dependencies explicit and the code testable without a framework; a service locator hides them.
- Static extension methods are the single, discoverable home for interface-to-implementation mappings: when an interface (ADR-13) gains an implementation, the one place to change or look is its `Add…` method, and a missing or duplicate registration shows up in one folder, not scattered across `Program.cs` and test setup. They also keep `Program.cs` readable and give tests one shared definition of the wiring, so the integration tests exercise the real registrations rather than a copy that could drift.
- Correct lifetimes matter because the repository is shared by concurrent requests (ADR-3) and captive dependencies cause subtle bugs; `ValidateOnBuild`/`ValidateScopes` and `ValidateOnStart` turn those mistakes and bad configuration into startup failures instead of runtime ones.
- A typed `HttpClient` lets the factory manage handler lifetime and DNS refresh, avoiding socket exhaustion, and is the natural place for the base URL and timeout (ADR-9).
- Overriding only the edges keeps integration tests meaningful: they prove the real wiring, validators, service and endpoints together, while the substituted bank, fixed clock and test tokens make them deterministic and independent of the network and an identity provider.
- A mocking framework keeps interaction checks ("the bank was not called") and scripted outcomes to one line each, and one technique serves both unit and integration tests (ADR-6).

**Rejected.** Service locator / static access; `new HttpClient()`; our own wrappers around framework abstractions; hand-written fakes as the default (they are allowed only as the readability exception in ADR-6); building a separate test container; re-registering internals in test setup.

---

## ADR-13: Test-driven development, and when a type needs an interface

**Decision.**
- Anything that contains logic is developed test-first: write a failing test, make it pass, then refactor.
- A class, record or struct that contains no logic is exempt from TDD: plain data holders such as request/response DTOs, entities, database objects and options classes. We do not write tests that only assert property getters and setters.
- Every other non-trivial class has an interface, kept in `BusinessLogic/Interfaces/` (services, mappings) or `DataInterface/` (stores, bank, metrics) (ADR-1): `PaymentService`, `BankClient`, `PaymentsRepository`, the validators' abstractions and so on. A DTO, entity or database object with no business logic does not get an interface.
- The line moves with the code: if a data type later gains logic (validation, calculation, state changes), it becomes non-trivial and from then on needs tests-first and an interface.
- This replaces the earlier ADR-12 wording ("an interface needs a reason: a test seam or a second implementation"). Framework abstractions (`ILogger`, `TimeProvider`, `IOptions`) are still never wrapped in our own interfaces.

**Why.**
- Tests-first for logic gives the design pressure and regression safety where defects actually occur (validation rules, status mapping, bank-failure handling), and means the behaviour that matters is testable by construction. Testing data holders adds noise and brittleness for no protection, so exempting them keeps the suite meaningful.
- A uniform, easy-to-apply rule ("logic means interface and tests-first; plain data means neither") is simpler to follow and to review than judging each class's need for an abstraction. Interfaces on every logic-bearing class give consistent seams for substitution in tests (ADR-6, ADR-12) and keep dependencies pointing at abstractions (ADR-1, ADR-12).
- Excluding data types avoids the common ceremony of an interface for every DTO, which would add files without adding seams.

**Trade-off.** An interface for every non-trivial class adds files and some ceremony, which sits in tension with KISS (ADR-1) when a class will only ever have one implementation. We accept that for consistency and testability, and keep the cost down by limiting the rule to classes with real logic and by not wrapping framework abstractions.

---

## ADR-20: Favour records over classes for DTOs and entities

**Decision.** DTOs (request and response types) and entities (stored or domain data) are declared as `record` types with `init`-only properties, not as classes. A plain class is used only when a type genuinely needs identity-based equality, mutable state or behaviour, in which case it is not a plain data holder and ADR-13 (tests first, interface) applies. A record that holds sensitive data overrides `ToString()` to redact it (ADR-9), because a record's generated `ToString()` prints every property. The current DTOs (`PostPaymentRequest`, `PostPaymentResponse`, `GetPaymentResponse`) already follow this.

**Why.**
- Records are immutable by default with `init`, so a payment, once built, cannot be altered by accident anywhere in the pipeline; that suits a payment gateway, where a stored payment must stay exactly as it was processed.
- Value equality is generated, so tests can compare whole objects (`BeEquivalentTo` or `Be`) without hand-written equality code, and `with` expressions create modified copies without mutating the original.
- A record declaration is shorter and makes the data-holder intent explicit, in line with KISS (ADR-1) and with ADR-13's distinction between plain data types and types with logic.
- Records are well supported by `System.Text.Json` serialisation and Minimal API model binding.

**Caveats, and how they are handled.**
- Value equality compares all properties, not identity. For an entity whose identity is its id, compare ids explicitly where that matters, or use a class if identity equality is required.
- The generated `ToString()` leaks property values; hence the redaction rule above.
- Records stored in the repository are shared references; with `init`-only properties they cannot be mutated by callers, so the in-memory store stays safe for concurrent reads (ADR-3).

**Rejected.** Mutable classes with public setters for data holders (as in the original template); records with `set` accessors.

---

## ADR-25: Service interfaces, decorators and validators

> **Status: PARTLY IMPLEMENTED. `IPaymentService`, `ICallerIdentity` and the FluentValidation validator are built. The decorators (`IdempotentPaymentService`, `AuditedPaymentService`) and `IReversalService` are a design only and are NOT implemented.**

**Decision.**
- **Interfaces.** Besides the repository, bank client, mapping, metrics, idempotency, audit and ledger interfaces already named in earlier ADRs, add `IPaymentService` (validate, bank, store), `IReversalService` (the reversal flow and its invariants, ADR-23) and `ICallerIdentity` (gives `InitiatedBy` and `OnBehalfOf` from the validated token, ADR-7). All are in `BusinessLogic/Interfaces/` or `DataInterface/`, async where they do I/O (ADR-16).
- **Decorators for cross-cutting behaviour.** Idempotency (ADR-21) and auditing (ADR-22) wrap the payment flow as decorators that implement the same interface and delegate to the inner one: `IdempotentPaymentService` and `AuditedPaymentService` around `PaymentService`, and the same for reversals. Audit is the outermost layer (so replays and conflicts are recorded), then idempotency, then the core service. The decorators are composed explicitly in an `Add…` extension method (ADR-12), without a decoration library. The Decorator pattern is named in a comment on each class (ADR-1).
- **Validators.** Request validation uses FluentValidation's own `IValidator<PostPaymentRequest>`, implemented by `PostPaymentRequestValidator`, and does not get a second, custom interface. The validator takes an injected `TimeProvider` for the expiry check and is registered by assembly scanning in an `Add…` method (ADR-8, ADR-12).

**Why.**
- Decorators keep `PaymentService` small and focused on the payment flow, so the size limits (ADR-18) hold, and each cross-cutting concern is a separate class that can be tested alone and reordered; folding idempotency and auditing into the service would break the limits and mix concerns.
- `IValidator<T>` is the library's standard interface; a custom duplicate would add a file and no seam.
- `ICallerIdentity` keeps token-claims parsing in one place instead of scattered through endpoints and services, and gives tests one thing to substitute.

**Trade-off.** More small classes and an explicit composition root for the decorator chain; accepted for focused, testable units. A decorator library would shorten the composition but adds a dependency for a handful of registrations.

**Rejected.** Putting idempotency and audit inside `PaymentService`; a custom validator interface; a decoration library.

---

## ADR-28: Writing for the reviewer

**Decision.** The repository is written to be read cold, quickly, by someone who did not build it (principles added 2026-11-16, also in `CLAUDE.md`):
- The few files that implement the brief are small and flat, and the README lists them in reading order with their sizes.
- Every top-level folder has a short `README.md` (what it is, what it may depend on, where to start), enforced by `FolderReadmeTests`; there is no unused code (a type or member is used, or is a labelled placeholder contract); folders are named by role, and what can be skipped is kept in clearly named folders.
- What can be skipped is labelled as skippable; placeholders carry a `PLACEHOLDER` comment.
- Every departure from, or interpretation of, the brief is in one README table with its reason.
- Documentation is layered by reader (README, then ADRs, then the assistant's working rules) without repeating itself, and is updated in the same commit as the change it describes.
- Claims are backed by tests where possible (the contract, architecture and naming tests), and names and comments say why, not what.

**Why.** A reviewer has limited time and judges simplicity and judgement as much as correctness. The brief asks for code that is "simple and maintainable" and for key design considerations and assumptions to be documented; a repository that makes the important parts easy to find, and honest about what it adds, meets both. A stale or buried explanation costs more than a missing one.

**Trade-off.** More documentation to keep true. The README table and reading guide are small, and the tests catch drift for the claims that matter.

**Rejected.** A long README that repeats the ADRs; leaving departures from the brief to be discovered in code comments; relying on the reviewer to work out which files matter.

---

# Testing, code quality and workflow

## ADR-6: xUnit v3 with AwesomeAssertions

**Decision.** Test with xUnit v3 (`xunit.v3` 4.x) and AwesomeAssertions (`.Should()` syntax). Do not use FluentAssertions.

**xUnit v3 on Microsoft Testing Platform (MTP).** Migrated from xUnit 2.9.3 so that the project follows its own rule of using the latest stable dependencies (ADR-4), while the test code was still small (no `IAsyncLifetime`, `Xunit.Abstractions` or `async void` to change). Consequences, all verified by running the suite:
- The test project is an executable (`<OutputType>Exe</OutputType>`), as xUnit v3 requires. The runner packages `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` stay in place, as the xUnit documentation recommends for compatibility.
- `xunit.v3` 4.x uses MTP v2, and the VSTest target is no longer supported, so a root `global.json` with `{ "test": { "runner": "Microsoft.Testing.Platform" } }` turns on MTP mode for `dotnet test` on the .NET 10 SDK. `dotnet test`, `dotnet test PaymentGateway.sln` and `dotnet test --solution PaymentGateway.sln` all work. Without that file `dotnet test` fails. The repository already needs the .NET 10 SDK to build (ADR-4), so this adds no new requirement for a reviewer.
- MTP changes the filter options: VSTest's `--filter` is replaced by switches such as `--filter-namespace "PaymentGateway.Api.Tests.Unit.*"` and `--filter-not-trait "Category=Simulator"`.
- `coverlet.collector` does not work under MTP, so it is replaced by `Microsoft.Testing.Extensions.CodeCoverage`: `dotnet test --coverage --coverage-output-format cobertura` writes to `TestResults/` (git-ignored).
- The xUnit1051 analyzer requires every awaited call that accepts a `CancellationToken` to be given `TestContext.Current.CancellationToken`, so tests use that instead of `CancellationToken.None` (ADR-16). Only tests of cancellation itself use their own source.
- Alternative considered: staying on xUnit 2.9.3 (zero change, but the previous line, so it would have needed a documented exception to ADR-4), or xUnit v3 3.x (keeps VSTest and Coverlet, but is not the latest stable).

**Why.**
- FluentAssertions v8+ requires a paid licence for commercial use; a candidate submission depending on a paid library invites an avoidable question.
- AwesomeAssertions is the community fork of FluentAssertions 7 (Apache 2.0), keeps the familiar `.Should()` syntax and is close to a drop-in replacement.

**Rejected.**
- *Shouldly* – free and the most popular alternative, but a different syntax; the difference is mostly taste.
- *Plain xUnit `Assert`* – no extra dependency and adequate for simple tests; AwesomeAssertions was preferred for readability.
- *FluentAssertions 7.x pinned* – free, but no longer the maintained line.

**Tests against the real simulator.** In addition to the stubbed-bank tests, a small set of tests call the real Mountebank simulator, tagged `[Trait("Category", "Simulator")]` and skipped when it is unreachable. *Why:* a stubbed `IBankClient` only proves our code against our own assumptions about the bank. The real simulator proves the actual contract: snake_case fields, `MM/YYYY` expiry, odd/even/zero card-number behaviour, and the 503 path. *Why optional and tagged:* they need Docker, so they must not make the normal `dotnet test` fail on a machine without it, and a reviewer can still run them with one command.

**Integration tests are a requirement, not an extra.** Every endpoint has integration tests that go through the whole pipeline with `WebApplicationFactory`, a stubbed bank client and test tokens, and they run in a plain `dotnet test` with no Docker. Minimum scenarios:
- POST: authorized (card ends in an odd digit), declined (even digit), and rejected for each class of invalid input plus malformed JSON, asserting the bank was not called and nothing was stored.
- POST when the bank fails (503 or timeout): an error response, and nothing stored.
- GET: an existing payment (a POST-then-GET round trip returns the same data) and an unknown id (404).
- Authentication: 401 with no or an invalid token and 403 with the wrong scope, on both endpoints.
- Security: no response body contains the full card number or CVV.
- Ownership: another merchant's payment gives 404 (ADR-7). Idempotency, reversals and audit (ADR-21, 23, 22) list required scenarios too, but those features are NOT implemented, so they have no tests.

*Why.* Unit tests prove the pieces; only an integration test proves the wiring, JSON binding and mapping (including snake_case/camelCase and the `Rejected` behaviour for malformed input), authentication and authorisation policies, validation, and the HTTP status mapping working together, which is where most real defects in an API like this appear. The brief expects automated tests and leaves the type to us; for a two-endpoint service an end-to-end suite through the real pipeline gives the most confidence per test. Keeping them Docker-free (stubbed bank) makes them deterministic and runnable by any reviewer; contract checks against the real simulator are the separate, tagged `Simulator` tests.

**Unit and integration tests are kept apart by directory.** Both live in the single test project (ADR-1), under separate top-level folders:
- `Unit/` mirrors the implementation's directory structure. The tests for `BusinessLogic/Services/PaymentService.cs` are in `Unit/Services/PaymentServiceTests.cs`, for `Validation/…` in `Unit/Validation/`, and so on: one test class per class under test, named `[ClassUnderTest]Tests`.
- `Integration/` is organised by feature/endpoint (`Integration/Payments/…`), with the real-simulator tests in `Integration/Simulator/`. It does not mirror implementation classes, because an integration test exercises many of them at once.
- `TestSupport/` holds shared helpers (test-token factory, the web application factory setup). Namespaces follow folders.

*Why.* The two kinds of test have different speed, dependencies and purpose, so people (and CI) should be able to run and read them separately, e.g. `dotnet test --filter-namespace "PaymentGateway.Api.Tests.Unit.*"` (MTP syntax, ADR-6). Mirroring the source tree for unit tests makes a test trivially findable from the class it covers (and shows at a glance which classes lack tests), while integration tests are about behaviour across the pipeline, so organising them by feature matches how they are described and reviewed. Keeping a class's tests out of integration folders also stops unit tests quietly growing a `WebApplicationFactory` dependency.

**Mocking: NSubstitute by default; a hand-written test double only when mocking would be unduly complex.** Any collaborator may be substituted with `Substitute.For<T>()` when that makes a unit test clearer; there is deliberately no rule restricting mocking to the system's edges. Examples in use: `IBankClient` in both unit and integration tests, and `IPaymentsRepository` in the unit tests of its consumers (for example `PaymentService`, where "nothing is stored when the request is invalid or the bank fails" is itself a requirement checked as an interaction). The clock is Microsoft's `FakeTimeProvider` (a test double from a package, not hand-written).

*Why.* The brief's key rule, "an invalid request never reaches the bank", is an interaction, and a mock states it in one line (`bank.DidNotReceive().SendAsync(...)`) where a hand-written fake would need its own call counter. Scripting outcomes (authorized, declined, 503, timeout) is a one-liner per test with a substitute but a growing class with a fake, and one technique across unit and integration tests means a single thing to learn and no test-only classes to maintain. NSubstitute was chosen over Moq and FakeItEasy for its minimal syntax (the substitute is called as if it were the real object), active maintenance, permissive licence (BSD-3) and because Moq's 2023 SponsorLink episode makes some teams wary of it. *The exception.* The default is a mocking framework, but readability is the test: where mocking would cause undue complexity (for example an elaborate multi-step setup, callbacks, or state that a mock expresses awkwardly) and a small hand-written test double is clearly more readable, use the hand-written double. Such a double lives in `TestSupport/` and carries a short comment saying why a mock was not used, so the exception stays visible and deliberate and does not become the norm. *Why allow it:* the aim of a test double is a clear, trustworthy test, not allegiance to a tool; forcing a mock into a case it fits badly produces setup noise that hides what the test is about. *Why not the other way round (fakes by default):* in the common cases here (script an outcome, verify the bank was not called) a mock is shorter and needs no extra classes.

*Guard rail:* verify interactions only where the interaction is the requirement (for example the bank is not called for a rejected payment); otherwise assert on outcomes. *Note.* The integration tests are unaffected: they still override only the bank client, the clock and authentication in `ConfigureTestServices` (ADR-12), because their purpose is to exercise the real wiring.

**Arrange-Act-Assert, with explicit section comments.** Every test follows the Arrange-Act-Assert pattern and marks each section with a comment (`// Arrange`, `// Act`, `// Assert`), in that order and separated by a blank line. There is exactly one Act per test; to cover more inputs use a `[Theory]` rather than several Acts. When a section is empty or done elsewhere the comment stays and says where (for example `// Arrange: done in the constructor`), so the structure is always visible. There are no assertions in Arrange and no setup in Assert.

**Shared setup lives in fixtures or the constructor, not in copies.** Setup that several tests share is abstracted away: cheap per-test setup in the constructor (xUnit creates a new instance for every test) with `IDisposable.Dispose` for teardown, or `IAsyncLifetime` when it is asynchronous; expensive setup shared by the tests of one class (for example a `WebApplicationFactory`) in an `IClassFixture<T>`, and across classes in an `ICollectionFixture<T>`. Fixtures, builders and helpers live in `TestSupport/` (ADR-24). A shared fixture must not hold mutable state that one test can leak into another, which is consistent with ADR-12's fresh-factory-per-class rule.

*Why.* The explicit AAA comments make every test readable the same way: a reader finds what is set up, what is exercised and what is expected without parsing the code, and a test with two Acts or assertions mixed into setup is visible at a glance. A single Act keeps each test about one behaviour, so a failure points at one thing. Pulling shared setup into the constructor or fixtures follows the rule of three (ADR-24), removes copy-paste, and keeps each test short enough to meet the size limits (ADR-18); the per-test instance that xUnit creates gives isolation by default, and class and collection fixtures are reserved for setup that is genuinely expensive. *Trade-off.* A few extra comment lines per test and a little indirection to find shared setup; accepted for uniform structure.

**Test approach.** Unit tests for validators and `PaymentService` (with a substitute `IBankClient`); integration tests through `WebApplicationFactory` with a substituted bank client and test tokens. Test names follow `[Method]_[Scenario]_[ExpectedResult]`.

---

## ADR-18: Size limits for methods, classes and files

**Decision.**

| Code entity | Soft limit | Hard limit |
|---|---|---|
| Method (body lines, excluding blank lines and braces) | 20 | 30 |
| Cognitive complexity per method | 10 | 10 |
| Nesting depth | 2 | 3 |
| Parameters | 3 | 4 |
| Class / file (one type per file, ADR-19) | 200 | 300 |
| Test class | 250 | 400 |
| `Program.cs` | 50 | 80 |
| Endpoint extension class | 150 | 250 |
| `PaymentGatewayDi` (all the `Add…` registrations, one class) | 200 | 250 |

- A limit is a smell, not a target. Hitting one means extracting a collaborator (with its own interface, ADR-13) or a named private step, not compressing code or joining lines. Likewise, a small cohesive class is not split just to get under a number.
- Exceptions: Arrange-Act-Assert tests may exceed the method limit while staying readable (heavy Arrange goes into helpers or builders in `TestSupport/`); flat registrations and flat one-field-per-line mappings may exceed it; generated code is not counted.
- Likely pressure points here: `PaymentService.ProcessAsync` (validate -> bank -> map -> store -> metrics; split into named steps such as `ValidateAsync`, `CallBankAsync`, `ToResult`, with validation, mapping and metrics already in their own classes), `BankClient` (keep request building, response parsing and error mapping separate) and test classes that cover many scenarios (split by behaviour).
- Enforcement: `.editorconfig` cannot be changed under the challenge rules, and the default analyzer thresholds (CA1502 cyclomatic complexity 25; SonarSource 75 lines per method, 1000 per file) are far looser than ours, so these limits are applied by convention and in review rather than by tooling.

**Why.**
- Short methods with one level of abstraction are easier to read, test and review. The limits keep each unit small enough to hold in your head, and cognitive complexity and nesting measure that better than line count alone.
- A class that reaches the limit usually has more than one reason to change; the limit prompts the extraction the single-responsibility principle would ask for, and fits ADR-1's small, single-purpose classes and ADR-13's interface-per-non-trivial-class rule.
- Tight, explicit numbers are easy for both a reviewer and Claude to apply consistently; the generous tool defaults would never trigger on a codebase this size.
- A thin `Program.cs` is the composition-root rule (ADR-12) in measurable form.

**Trade-off.** More, smaller methods and classes means more files and some indirection, which sits slightly against KISS (ADR-1); accepted because small units are what make the test-first, interface-per-class approach (ADR-13) workable.

**Rejected.** No explicit limits (leaves it to taste); the tool default thresholds (too loose to catch anything); mandatory analyzer tooling (more setup than a project this size justifies).

---

## ADR-19: One type per file, and splitting large test classes

**Decision.**
- **One type per file.** Every class, record, struct, interface and enum has its own file, named after the type. No nested or companion types sharing a file.
- **Test names end in `Tests`.** A test class is named `[ClassUnderTest]Tests` (for example `PaymentServiceTests` for `PaymentService`). The suffix keeps test class names from colliding with the implementation names they test.
- **Limit the number of tests per class and file:** at most 10 test methods (15 hard). A `[Theory]` with several `InlineData` rows counts as one test. This is in addition to the line limits in ADR-18.
- **Split by functional area when a test class would exceed the limit.** Instead of growing `PaymentServiceTests.cs`, replace it with a directory named after the class under test with the `Tests` postfix, `PaymentServiceTests/`, holding one class and file per functional area. Each is named after its functional area with `TestClass` appended, for example:
  ```
  Unit/BusinessLogic/Services/PaymentServiceTests/
      ValidationRejectionTestClass.cs   // Rejected paths: the bank is never called, nothing stored
      BankFailureTestClass.cs           // 503, timeout, network failure, malformed reply
      SuccessfulPaymentTestClass.cs     // authorized and declined outcomes
  ```
- **Namespaces follow folders** (`PaymentGateway.Api.Tests.Unit.Services.PaymentServiceTests`). A class under test has either a single `[Name]Tests.cs` file or a `[Name]Tests/` directory, never both: a type and a namespace of the same name in the same namespace collide.
- **The same rules apply to integration tests:** the limit and the naming, with the split done inside the feature folder (ADR-6), for example `Integration/Payments/…`.

**Why.**
- One type per file makes a type findable by file name, keeps diffs and merges focused, and keeps files small enough for the ADR-18 limits.
- Test classes grow by accretion; a cap on tests per class stops a single class becoming a dumping ground, and splitting by functional area means each class has a clear subject, so a failure points at an area, and the files stay readable.
- Naming the split directory after the class under test keeps the Unit tree mirroring the implementation tree (ADR-6), so a test is still findable from the class it covers.
- The `Tests` suffix on the class and the `TestClass` suffix on the area classes mean no test type shares a name with an implementation type, which avoids ambiguous references and confusing IDE navigation.

**Trade-off.** More, smaller files and one more directory level for heavily tested classes; accepted for navigability and focused diffs. The cap of 10 is a judgement call and can be revisited.

**Rejected.** Several types per file; unlimited test class size; splitting test classes by arbitrary numbering (`PaymentServiceTests2`) or by test type instead of functional area.

---

## ADR-24: Global usings per project, and minimising boilerplate

**Decision.**
- **One `Usings.cs` per project** (the API project and the test project each have their own; the name is kept from the existing test project file), holding `global using` directives for namespaces that are used throughout that project: both framework namespaces and our own. Per-file `using` directives are then limited to namespaces needed by only one or two specific files.
- **The threshold.** A namespace used by three or more files in a project is made global; one used by one or two files stays a local `using` in those files. Review the list when a namespace crosses the threshold in either direction.
- **Build on `ImplicitUsings`.** Both projects keep `<ImplicitUsings>enable</ImplicitUsings>`, which already supplies the common `System.*` namespaces (and the web SDK's additions), so `Usings.cs` only adds what is not already implicit.
- **Contents.** Typical API-project candidates are our own widely used namespaces (for example `PaymentGateway.Api.Interfaces`, `...Models.Requests`, `...Models.Responses`); typical test-project candidates are the test libraries (`Xunit`, `AwesomeAssertions`, `NSubstitute`) and the same widely used application namespaces. Group framework, then third-party, then our own namespaces, each alphabetised. The test project's existing `Usings.cs` is kept and extended, and the API project gets one of the same name.
- **Keep the list small and unsurprising.** Do not make a namespace global when it would create ambiguous type names, hide where a commonly confusing type comes from, or when only a couple of files use it. Avoid `global using static` unless it clearly improves readability. Global usings are per project; they are not shared across projects through `Directory.Build.props`.
- **Minimise boilerplate, especially when it repeats.**
  - Use the language features that remove ceremony: file-scoped namespaces, primary constructors, records, expression-bodied members, target-typed `new`, collection expressions, pattern matching and top-level statements.
  - Extract repeated code once it appears for the third time (the rule of three), as a helper, extension method, base type or builder; do not abstract speculatively on the first or second occurrence.
  - In tests, put repeated arrange code into builders and helpers in `TestSupport/` (for example a payment builder, the web application factory setup and a test-token factory) and use `[Theory]` for the same check over several inputs, instead of copy-pasted tests.
  - Do not add comments or XML documentation that merely restate the code.
  - Boilerplate removal must not hide behaviour: prefer an explicit helper over reflection, conventions or generated magic (consistent with ADR-14's rejection of AutoMapper).

**Why.**
- ADR-19's one-type-per-file rule produces many small files; without global usings each one begins with the same block of directives, which is noise that buries the one or two usings that say something about that file.
- Showing only the unusual dependencies at the top of a file makes it easier to see what a file really relies on, and removes churn when a widely used namespace moves.
- Repeated boilerplate is a maintenance cost: every copy must be changed together, and mistakes creep in. The rule of three balances DRY (ADR-1) against premature abstraction, which would cut against KISS.
- Language features that remove ceremony make the code shorter without hiding what it does; reflection-style magic would hide behaviour, which is why it is excluded.

**Trade-off.** Global usings make a file's origin of a type less visible in isolation (the type's namespace is not at the top of every file), and a badly chosen global namespace can cause ambiguity project-wide. The threshold, the small-list rule and the ambiguity exclusion are there to keep that in check.

**Rejected.** A full set of per-file usings everywhere (noisy); making everything global (hides dependencies and invites ambiguity); sharing one global-usings list across projects; generating boilerplate through reflection or conventions that obscure behaviour; abstracting on the first duplicate.

---

## ADR-10: Source control workflow

**Decision.** This is a solo exercise, so no feature/bugfix/hotfix branches: work on `main` in small commits (one logical step each, only when build and tests pass) using `type: description` (`feat`, `fix`, `refactor`, `test`, `docs`).

**Why.** Feature branches add ceremony without safety benefit for one person; small commits give the same ability to roll back. Squashing gives the reviewer a tidy result.

---

# Designed but NOT IMPLEMENTED in this repository

## ADR-21: Idempotency keys

> **Status: NOT IMPLEMENTED in this repository. This is a design only. There is no `Idempotency-Key` handling, no idempotency store and no replay protection.**

**Decision.** State-changing requests are idempotent through an `Idempotency-Key` header.
- **Where.** Optional on `POST /api/payments` and `POST /api/payments/{id}/reversals`. Without the header the request is processed normally and is not protected against replay; this keeps requests written to the brief working (revised: it was required at first, which made a plain `curl` from the brief fail). A key that is present but empty or over-long (more than 255 characters) is a 400. Reads (`GET`) need none.
- **Scope.** A key is scoped to the calling merchant and the operation, so two merchants can use the same key value, and one key cannot be used to replay a different operation.
- **Fingerprint.** The key is stored with a fingerprint of the request: an HMAC-SHA256, using a secret from configuration, over a canonical form of the request. The card number is never stored, and never stored as a plain hash either, because card numbers have a small search space and a plain hash can be brute-forced.
- **Behaviour.**
  - First use of a key: process the request and record the outcome (status code and response body, which holds only last-four digits).
  - Same key, same fingerprint, completed: return the stored response with the original status code and an `Idempotent-Replayed: true` header. The bank is **not** called again.
  - Same key, different fingerprint: 422 `ProblemDetails` (the key is being reused for a different request).
  - Same key while the first request is still in progress: 409 `ProblemDetails`. The reservation of a key is atomic (a thread-safe try-add), so two concurrent requests cannot both reach the bank.
- **What is recorded.** Only outcomes where the operation actually completed: payments the bank authorized or declined, and successful reversals. `Rejected` requests (nothing happened) and failures (bank error, 404, 409) release the key, so a corrected request can reuse it.
- **Retention.** Records expire after 24 hours (using `TimeProvider`). Storage is in-memory behind `IIdempotencyStore` (ADR-3, ADR-13), thread-safe and async (ADR-16).
- **Known limit.** If the bank times out, the real outcome is unknown: the payment may have been taken. The key is released and nothing is stored, so a retry with the same key calls the bank again, and could double-charge. A real gateway would pass the idempotency key to the bank (the simulator cannot) and reconcile unknown outcomes. This is documented as a gap, and no automatic retries are made (ADR-9).
- **Metrics.** An `idempotency.requests` counter tagged `outcome` = `replayed` | `conflict` (ADR-15). **Audit.** Replays and conflicts are audit events (ADR-22).
- **Tests.** Required integration scenarios: replay returns the identical response and the bank substitute received exactly one call; same key with a different body gives 422; an empty or over-long key gives 400, and a missing key is processed normally; concurrent requests with the same key reach the bank once; a rejected request does not consume the key.

**Why.**
- Networks fail and clients retry. Without idempotency a retried payment request can charge a card twice, the most damaging failure a payment gateway has; with it, retrying is safe.
- Scoping by merchant and operation stops cross-tenant collisions and replay of the wrong operation. The fingerprint check catches client bugs (reusing a key for a different amount).
- A keyed HMAC fingerprint avoids storing card data in any form, in line with ADR-9.
- Recording only completed outcomes keeps the rule simple and lets merchants fix and resend a rejected request.

**Trade-off.** It adds a header the brief does not define, a store and several error cases; a reviewer's plain `POST` without the header gets a 400 (the README gives the example header). Accepted as a deliberate departure from the brief.

**Rejected.** Optional keys (retries stay unsafe by default); a plain hash of the request; storing the request body; server-generated keys; retrying the bank automatically.

---

## ADR-22: Auditing

> **Status: NOT IMPLEMENTED in this repository. This is a design only. There is no audit trail.**

**Decision.** Every security- and money-relevant action produces an immutable audit event in an append-only audit trail.
- **Interface.** `IAuditTrail` offers `AppendAsync` and read access (`ReadAsync`, for tests and operators). There is deliberately **no update or delete**. Storage is in-memory and thread-safe in this exercise (ADR-3).
- **Event.** `AuditEvent` is a record (ADR-20): event id, UTC timestamp (from `TimeProvider`), **originator** (`InitiatedBy`: the principal that made the call, from the token's `sub`), **on behalf of** (`OnBehalfOf`: the merchant account the operation is performed for, ADR-7), action, resource type and id, outcome, `traceId` (ADR-11), and the idempotency key when there is one. Both identities are always recorded, even when they are the same; for an unauthenticated request (401) both are empty. Identities come only from the validated token, never from the request body.
- **Events recorded.** Payment attempted (before the bank is called); payment authorized, declined, rejected, or bank error; payment retrieved; reversal requested, completed or refused (with the reason); idempotent replay; idempotency conflict; authentication failure (401) and authorisation denial (403).
- **Never in an audit event.** The card number (including the last four digits), CVV, tokens, request or response bodies. Identify a payment by its id.
- **Distinct from logging.** Logs (ADR-11) are diagnostic and may be sampled or rotated; the audit trail is the durable record of who did what to which payment, with a defined schema, and is never reduced. An audit event is also written to the structured log, but the log is not the audit trail.
- **Ordering.** The `attempted` event is written before the bank is called, so a trail exists even if the process dies mid-call. A failure to write an audit event is logged at error level and fails the request before any money moves; for the in-memory store this cannot happen, and a production store would use a transactional write or an outbox.
- **Metrics.** None beyond the existing counters. **Tests.** Required integration scenarios: each action above produces exactly one matching event with the right `InitiatedBy`, `OnBehalfOf` and trace id (including a case where the two differ); events never contain card data; the trail cannot be modified through its interface.

**Why.**
- A payments service has to answer "who did what, when, to which payment, and with what result", for disputes, fraud investigation, reconciliation and compliance (PCI DSS requires tracking and monitoring access to cardholder data).
- Append-only and immutable means the trail can be trusted; separating it from logs means it cannot be lost to log rotation or sampling.
- Recording the attempt before the bank call, and refusals and failed authentication as well as successes, is what makes the trail useful for investigating abuse.
- Keeping card data out of events means the audit trail is not itself a cardholder-data store.

**Trade-off.** Another store, interface and record, and a write on every operation; accepted for a deliberate departure from the brief. In production the trail needs write-once or tamper-evident storage (for example a hash chain), which is out of scope here.

**Rejected.** Using ordinary logs as the audit trail; mutable audit records; logging bodies "for debugging"; recording the last four digits.

---

## ADR-23: Reversals as balancing transactions

> **Status: NOT IMPLEMENTED in this repository. This is a design only. There are no reversal endpoints, no ledger and no `payments:reverse` scope.**

**Decision.** A completed payment is never modified or deleted. Undoing it is a new, separate, immutable **balancing transaction** that offsets the original, so the history stays complete and the net effect is zero.
- **Ledger.** `Transaction` is a record (ADR-20): id, payment id, `OnBehalfOf` merchant id, `InitiatedBy` originator (ADR-7), type (`Payment` | `Reversal`), signed amount in minor units, currency, UTC timestamp (from `TimeProvider`), the id of the transaction it reverses (for reversals) and the idempotency key. An authorized payment writes a `Payment` transaction of `+amount`; its reversal writes a `Reversal` transaction of `-amount` linked to it. The balance of a payment is the sum of its transactions: `amount` while outstanding, `0` once reversed. Declined and rejected payments move no money and write no transaction.
- **Append-only.** `ITransactionLedger` offers append and read only, with no update or delete. Appending a reversal is an atomic check-and-append, so two concurrent reversals cannot both succeed.
- **Invariants (enforced in one place, tested first).** Only an `Authorized` payment can be reversed; it can be reversed at most once; the reversal amount equals the payment's outstanding balance (full reversals only; partial reversals are out of scope); the balance is never negative or greater than the original amount.
- **API.**
  - `POST /api/payments/{id}/reversals` (scope `payments:reverse`, `Idempotency-Key` optional) returns 201 with the reversal transaction.
  - `GET /api/payments/{id}/transactions` (scope `payments:read`) returns the ledger entries and the net balance.
  - `GET /api/payments/{id}` is unchanged: the original payment stays `Authorized` (the brief's statuses are not extended), and the reversal is visible as a transaction.
  - Responses: 404 for an unknown payment or one that belongs to another merchant (ADR-7); 409 `ProblemDetails` for a payment that is declined, or already reversed.
- **Bank.** The simulator has no reversal route (it rejects unknown requests with a 400), so no bank call is made: the reversal is recorded in the gateway's ledger only. In production the acquirer would be notified (a void or refund through `IBankClient`, itself idempotent) before or as part of recording the reversal. This is documented as a gap.
- **Audit and metrics.** Every reversal outcome is an audit event (ADR-22). A `payments.reversed` counter is added (ADR-15). Idempotency applies (ADR-21).
- **Tests.** Required integration scenarios: a reversal leaves the original payment unchanged and the ledger sums to zero; a second reversal gives 409; reversing a declined payment gives 409; an unknown or foreign payment gives 404; a replayed reversal returns the original response; concurrent reversals produce exactly one reversal transaction.

**Why.**
- Financial records are corrected by adding offsetting entries, never by editing or deleting history (the double-entry principle). That keeps an immutable, auditable record of what happened, which is what reconciliation, disputes and regulators need.
- Because stored payments are never changed, there is no lost-update problem, and the original payment, the reversal and the audit trail always agree.
- Making the invariants explicit and enforcing them atomically in one place prevents the worst reversal bugs: reversing twice, reversing something that never took money, or reversing more than was taken.
- Full reversals only, and no bank call, keep the scope proportionate to the exercise.

**Trade-off.** A second resource, endpoints, scope and store that the brief does not mention, and a ledger that real money movement would need to reconcile with the acquirer; accepted as a deliberate departure.

**Rejected.** Changing the payment's status to `Reversed` (loses history and extends the brief's statuses); deleting or editing stored payments; partial reversals; calling the simulator with a negative amount (it would "authorize" it, which is wrong).

---

## Reference

### Project layout (ADR-1, ADR-27)
- `src/PaymentGateway.Api/Api/` – `Endpoints/` (thin Minimal API mapping, `FailureResults`), `Security/` (scopes, `CallerIdentity`), `ErrorHandling/`, `Documentation/` (`AddApiDocumentation()` / `MapApiDocumentation()`, ADR-5), `Options/`
- `.../ApiInterface/` – `Dtos/Requests/` and `Dtos/Responses/`: the request and response DTOs (records) the endpoints accept and return
- `.../BusinessLogic/` – `Services/` (`PaymentService`: validate -> bank -> store), `Validation/` (FluentValidation), `Mapping/` (hand-written `IMapping<TSource, TDestination>`, ADR-14), `Models/` (`ProcessedPaymentModel`), `Interfaces/` (`IPaymentService`, `IReversalService`, `ICallerIdentity`, `IMapping<,>`), `Placeholders/`
- `.../Shared/Outcomes/` – `Result<T>`, `Error`, `ErrorKind`, `FieldError` (ADR-17), the cross-layer vocabulary that depends on nothing
- `.../DataInterface/` – `IPaymentsRepository`, `IBankClient`, `ITransactionLedger`, `IAuditTrail`, `IIdempotencyStore`, `IPaymentMetrics`; `Entities/` (`PaymentEntity`, `TransactionEntity`, `AuditEventEntity`, ADR-26), `Enums/`, `Dtos/Requests|Responses/` (the bank's request and response records, snake_case JSON), `Idempotency/`, `Ledger/`, `Telemetry/`
- `.../DataLayer/` – `Repositories/` (`PaymentsRepository`, in-memory singleton), `Bank/` (`BankClient`, the Adapter to the bank, and `BankOptions`), `Placeholders/` (`NoOpPaymentMetrics`)
- `.../PaymentGatewayDi.cs` – the composition root (ADR-12): one static class whose single public method, `InitialiseDi`, comes first and is called by `Program.cs`; below it, one internal `Add…` registration per concern
- `test/PaymentGateway.Api.Tests` – unit and integration tests in one project, split by folder (ADR-6):
  - `Unit/` – mirrors the implementation folders (`Unit/BusinessLogic/Services/`, `Unit/DataLayer/Repositories/`, …); `Unit/Architecture/` holds the layer-dependency tests
  - `Integration/` – by feature (`Integration/Payments/`, `Integration/Simulator/`)
  - `TestSupport/` – shared helpers
- `imposters/bank_simulator.ejs` and `.editorconfig` are not to be changed.

### Brief: request validation (ADR-8)
Card number required, 14-19 characters, digits only. Expiry month required, 1-12. Expiry year required; month+year in the future. Currency required, 3 characters, allowlist of at most 3 ISO codes. Amount required, integer, minor units (we also require it to be greater than zero). CVV required, 3-4 characters, digits only.

### Brief: response fields
Id (GUID), Status (`Authorized`/`Declined`), last four card digits, expiry month, expiry year, currency, amount. Never the full card number or CVV. GET returns the same fields for a stored payment.

### Bank simulator (`POST http://localhost:8080/payments`, ADR-9)
Request body (snake_case): `card_number`, `expiry_date` (`MM/YYYY`), `currency`, `amount`, `cvv`; response `authorized` + `authorization_code`.
- Any field missing -> 400.
- Card number ends in an odd digit -> 200, `authorized: true`.
- Ends in an even digit (2, 4, 6, 8) -> 200, `authorized: false`.
- Ends in 0 -> 503 (bank unavailable; treated as an error, not a decline).
Start it with `docker-compose up -d` (simulator on :8080, Mountebank UI on :2525).

### Security checklist (ADR-7, ADR-9)
HTTPS redirection and HSTS; JWT bearer with scope policies; nothing sensitive persisted or logged; redacted `ToString()` on request types; global exception handler with no internals; bank token never forwarded. Per-merchant ownership checks (ADR-7), idempotency keys (ADR-21) and an append-only audit trail (ADR-22) are built. Out of scope (README "known gaps"): rate limiting, persistent encrypted storage, bank-side reversals (ADR-23).

---

## Conventions

File-scoped namespaces; namespaces follow folders; primary constructors for DI; records for DTOs and entities (ADR-20); hand-written mapping classes implementing `IMapping<TSource, TDestination>` (no AutoMapper, ADR-14); no stored procedures; `Result<T>` instead of exceptions for business outcomes; `CancellationToken` on every async method; .NET dependency injection for all dependencies; test names `[Method]_[Scenario]_[ExpectedResult]`; mention a design pattern in a comment only where one is genuinely used.

## Assumptions (not specified by the brief)

- Unknown payment id -> 404.
- Rejected requests -> HTTP 400 with status `Rejected`; nothing is stored.
- Known production gaps, deliberately not built: rate limiting, persistent encrypted storage, notifying the acquiring bank of reversals (the simulator has no such route), partial reversals, and resolving an unknown outcome after a bank timeout (ADR-21).
- The `Idempotency-Key` header is optional on `POST /api/payments` and `POST /api/payments/{id}/reversals` (so brief-conforming requests work); a present but empty or over-long key is a 400. Reversals return 201 Created. The brief specifies none of this.
- Bank unavailable (503/timeout) -> an error to the merchant, not `Declined`.
- The currency allowlist is USD, GBP and EUR.

## Implementation status

**What is implemented.** The brief's two endpoints, validation, the bank client, the in-memory repository, authentication with per-merchant ownership, logging, error handling, request limits and HSTS. **What is NOT implemented (design only):** idempotency (ADR-21), the audit trail (ADR-22), reversals and the ledger (ADR-23), the decorators and `IReversalService` (ADR-25) and the real metrics (ADR-15; only the `IPaymentMetrics` seam and its no-op placeholder exist). Their interfaces were removed because nothing in the running code calls them. In the checklist below `[x]` is built, `[~]` is partly built and `[ ]` is not built. Verified: the full test suite passes with zero warnings (a handful of tests are the tagged `Simulator` ones, skipped without the simulator), `dotnet format --verify-no-changes` is clean, and the running app is smoke-tested over real Kestrel by `scripts/smoke-test.ps1` (401 without a token, 502 with the bank unreachable, 400 for an empty key and for malformed JSON, 413 for an oversized body, 404 for an unknown id). Later additions: HTTPS redirection plus HSTS outside Development (`UseTransportSecurity`), and an authorization fallback policy so any endpoint without its own requirement still needs a signed-in user (anonymous callers get 401 for unknown paths too).

- [x] Retarget both projects to `net10.0` and update test packages (done; later moved from xUnit 2.9.3 to xUnit v3 4.x, see ADR-6)
- [x] Reorganise tests into `Unit/` and `Integration/` (done; `PaymentsControllerTests.cs` is now in `Integration/Payments/`; `TestSupport/` is created when the first shared helper is needed)
- [x] Template fixes: card number / CVV / last-four are strings; thread-safe async repository behind `IPaymentsRepository` (registered in `PaymentGatewayDi`); redacted `ToString()` on `PostPaymentRequest`; DTOs are records
- [x] Minimal API endpoints per ADR-2/8/17 (`Endpoints/PaymentEndpoints.cs`, `MapPaymentEndpoints()`): `POST /api/payments` returns 200 with the payment, 400 with `{ "status": "Rejected", "errors": [{ "field", "message" }] }` for an invalid request, or 502 `ProblemDetails` (with `traceId`) for a bank error; `GET /api/payments/{id}` returns 200 or 404. `PaymentsController` and the unused `GetPaymentResponse` are removed. Decisions made here:
  - A bank error is a **502 Bad Gateway** (the upstream failed), not a 503, and not a decline.
  - `PaymentStatus` is serialised as its name (`"Authorized"`) through a `JsonStringEnumConverter` attribute on the enum; the template serialised numbers.
  - A missing, malformed or wrongly typed JSON body is a `Rejected` 400 through `BadRequestExceptionHandler` (an `IExceptionHandler`, registered in `AddErrorHandling()`), because the brief treats invalid information as `Rejected`; other binding failures (for example a non-JSON content type) keep their status, as `ProblemDetails` (415). The exception message is never returned.
  - `RouteHandlerOptions.ThrowOnBadRequest = true` is set explicitly: the framework only throws binding failures by default in Development, so without it Production would answer malformed bodies with the framework's own 400 instead of `Rejected` (a Production-environment test proves it).
  - `app.UseAuthorization()` was removed: it needed services that only `AddControllers()` had registered, and authorization is added properly with authentication (ADR-7).
  - 16 integration tests across `PostPaymentTests`, `RejectedAndFailedPaymentTests`, `MalformedRequestTests` and `GetPaymentTests`, using `PaymentsApiFactory` (real app, substituted bank)
- [x] Request body size limit (ADR-11): `AddRequestLimits` sets Kestrel `MaxRequestBodySize` from `RequestLimits:MaxRequestBodyBytes` (default 4096, validated on start); unit-tested through the resolved `KestrelServerOptions`, and the Kestrel smoke test proves a 10 KB body gets 413 (`TestServer` does not enforce the limit)
- [x] `PaymentService`, `IBankClient`/`BankClient`, `IPaymentsRepository`, validators, `Result<T>` (done; see the entries below)
- [x] Scalar + `Microsoft.AspNetCore.OpenApi`; Swashbuckle removed; `launchUrl` is `scalar/v1` (done: `AddApiDocumentation()` / `MapApiDocumentation()` in `Documentation/`, served in Development only, with integration tests; the OpenAPI bearer security scheme is added with authentication)
- [x] Test packages (AwesomeAssertions, NSubstitute, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.Extensions.Diagnostics.Testing`), the integration suite, and three tagged `Simulator` tests (odd, even and zero card endings against the real `BankClient`), skipped unless the simulator is running. They pass against the real simulator (Mountebank 2.8.1 with the repo's `imposters/bank_simulator.ejs`; run from the npm package because Docker is not installed on the build machine, which is the same software the Docker image runs). The whole app was also exercised end to end against it: authorized, declined, bank-unavailable (502), retrieval, idempotent replay, reversal and a second reversal (409)
- [x] Bank client and `Result<T>` per ADR-9/17: `IBankClient`/`BankClient` (typed `HttpClient`, options validated on start, every transport failure becomes a `BankError` result, caller cancellation propagates), `Result<T>`/`Error`/`ErrorKind`, `IMapping<,>` with the request-to-bank-request mapping; 27 unit tests
- [x] `IPaymentService`/`PaymentService` per ADR-1/9/25: validate, bank, store; invalid requests never reach the bank or the store, bank errors are not stored; `ProcessedPaymentModel`, `PaymentEntity` and the two mappings between request, entity and response (last four digits only; see ADR-26); `Error.FieldErrors`; 20 unit tests (109 in total)
- [x] Global exception handler per ADR-17: `UnhandledExceptionHandler` (generic 500 `ProblemDetails` with `traceId`, logged once) with integration tests
- [ ] Idempotency per ADR-21: **NOT IMPLEMENTED** (design only: optional `Idempotency-Key`, keyed HMAC fingerprint, 24 h retention, replay and conflict responses)
- [ ] Audit trail per ADR-22: **NOT IMPLEMENTED** (design only: append-only `IAuditTrail`, `AuditedPaymentService` decorator)
- [ ] Reversals per ADR-23: **NOT IMPLEMENTED** (design only: balancing ledger transactions, `POST /api/payments/{id}/reversals`, `GET /api/payments/{id}/transactions`, `payments:reverse` scope)
- [x] Per-merchant ownership per ADR-7: `ICallerIdentity`/`CallerIdentity` (InitiatedBy = `sub`; OnBehalfOf = `merchant_id`, else `client_id`, else `azp`, else `sub`), payments stored with their `MerchantId`, `PaymentService.GetAsync` compares it with the caller and another merchant's payment is a 404; `MapInboundClaims = false` so `sub` keeps its name
- [x] Request validation per ADR-8/25: `PostPaymentRequestValidator` (FluentValidation 12.2.0, Apache-2.0) with injected `TimeProvider`, nullable numeric request fields, registered by assembly scanning in `AddPaymentServices()`; 59 unit tests under `Unit/Validation/PostPaymentRequestValidatorTests/` split by functional area, shared data in `TestSupport/`
- [~] Service interfaces and decorators per ADR-25: `IPaymentService`, `ICallerIdentity` and the FluentValidation validator are built; the idempotency and audit decorators and `IReversalService` are **NOT IMPLEMENTED** (design only)
- [x] Global usings per ADR-24: `Usings.cs` in both projects; the API project now has every namespace used by three or more files, and the per-file duplicates were removed
- [x] Existing tests retrofitted to the Arrange-Act-Assert comment convention, with shared setup in constructors and class fixtures (ADR-6); follow it for all new tests
- [x] ADR-19 followed (one type per file; test classes split by functional area, at most 10 tests each)
- [x] ADR-18 (size limits) checked as each piece was written; `PaymentService` has eight constructor dependencies, an accepted exception for a DI constructor
- [x] ADR-26: payments are stored as `PaymentEntity` (own entity, mappings `ProcessedPaymentModel` -> `PaymentEntity` -> response); `IPaymentsRepository` only adds and gets by id and has no merchant parameter; ownership is decided in `PaymentService.GetAsync`
- [x] ADR-27: code organised into Api/ (endpoints + DTOs, security, error handling, docs), BusinessLogic/, DataInterface/ (ports, entities, enums, bank messages) and DataLayer/ (repository, bank client, metrics placeholder); the entity is `PaymentEntity` (`TransactionEntity` and `AuditEventEntity` are part of the NOT IMPLEMENTED ledger and audit designs); Unit/Architecture/LayerDependencyTests (NetArchTest.Rules) enforces the dependency rules and the Entity suffix; tests moved to mirror the new folders. Result/Error/ErrorKind moved to Shared/Outcomes (no layer exception: Shared depends on nothing and every layer may use it); the project root namespace is PaymentGateway so layers are PaymentGateway.Api / .BusinessLogic / .DataInterface / .DataLayer / .Shared; the API DTOs have their own ApiInterface layer (ApiInterface/Dtos) and the bank messages are DataInterface/Dtos (each with Requests/ and Responses/ subfolders; NamingConventionTests checks every Request/Response type is in one) (DTOs are part of the interface they belong to), so BusinessLogic does not depend on Api at all; NamespaceFolderTests enforces that every file's namespace equals the root namespace plus its folder path (src and test)
- [x] ADR-28: README reading guide (five files, about 260 lines, plus what to skip), a single "Where this departs from, or interprets, the brief" table, a "start here" index at the top of this file, and the reviewer-readability principles in `CLAUDE.md`; a `README.md` in each layer folder and the test project (`FolderReadmeTests`); dead code removed (`AuditActions`), the two reversal-only `ErrorKind` members labelled
- [x] API contract tests (`Integration/Contract/ApiContractTests`): exact JSON field names and status strings from the brief, using raw JSON; a changed property name fails them
- [x] ADR-16 (async conventions) followed, including `Task<T>` on `IPaymentsRepository` and a cancellation token on every awaited call
- [~] Metrics per ADR-15: only the seam is built (`IPaymentMetrics`, called by `BankClient`, backed by the no-op `NoOpPaymentMetrics`); the meter, instruments and OpenTelemetry exporters are **NOT IMPLEMENTED** (design only)
- [x] Mapping classes per ADR-14: request-to-bank, processed-payment-to-entity and entity-to-response, each with unit tests
- [x] ADR-13 followed: tests first for logic, interfaces for non-trivial classes, none for plain data
- [x] DI wiring per ADR-12: registrations in one static `PaymentGatewayDi` class at the project root (`InitialiseDi` chaining `AddStructuredLogging`, `AddRequestLimits`, `AddPaymentMetrics`, `AddApiAuthentication`, `AddApiDocumentation`, `AddErrorHandling`, `AddPaymentServices`, `AddBankClient`), options validated on start, typed `HttpClient`
- [x] Serilog (`Serilog.AspNetCore`) behind `ILogger<T>` via `AddStructuredLogging`, levels in `appsettings` (`Serilog` section), JSON console (`RenderedCompactJsonFormatter`), trace id in every event (`@tr`); `ReadFrom.Services` lets tests capture events with an `ILogEventSink`; `preserveStaticLogger: true` stops parallel hosts replacing the global logger. `PaymentService` logs payment id and status only; tests prove no card number or CVV reaches a log. Still open: traceId already in error responses (done); Kestrel request body size limit (deferred, ADR-11)
- [x] JWT bearer + scope policies: `AddApiAuthentication` (config from `Authentication:Schemes:Bearer`, closed by default with no keys), `ScopeRequirement`/`ScopeAuthorizationHandler` (space-separated `scope` claim), one policy per scope, OpenAPI bearer scheme, `scripts/dev-token.ps1` wrapping `dotnet user-jwts`; tests sign real JWTs with a test key (`TestTokens`, `PaymentsApiFactory` post-configures `JwtBearerOptions`) and cover 401 (none, garbage, wrong key, expired) and 403 (wrong scope) on both endpoints. Still open: `user-jwts` instructions in the README; `ICallerIdentity` and per-merchant ownership (next step)
- [x] README: how to run, API, authentication, assumptions, known gaps (kept up to date with each feature)
