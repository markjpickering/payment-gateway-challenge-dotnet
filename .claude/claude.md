# CLAUDE.md – Payment Gateway Challenge (.NET)

Checkout.com interview exercise: an API that validates card payments, forwards them to a bank simulator and stores/retrieves results. `origin` is the candidate's own copy; never open PRs against `cko-recruitment` repos.

**Reasoning, alternatives and reference detail live in [`docs/architecture-decisions.md`](../docs/architecture-decisions.md) (ADR-n references below). This file holds short rules only. Where there is a conflict, the challenge brief wins (authentication, metrics, idempotency, auditing and reversals are the deliberate exceptions: all are required although the brief asks for none of them; so is the amount > 0 rule, which the brief does not state).**

**This repository holds the reduced ("lean") implementation, made for review; the full implementation (a `dev` branch) is not part of it.** Idempotency, the audit trail, reversals/ledger and metrics are designed (ADR-15, 21, 22, 23, 25) but NOT implemented here, and their interfaces were removed too, because nothing in the running code calls them (an interface nothing calls is dead code). The one exception is `IPaymentMetrics`: the bank client calls it, so it stays, backed by a PLACEHOLDER no-op (`NoOpPaymentMetrics`, registered by `AddPaymentMetrics()`). The rules below about idempotency, auditing, reversals and the ledger (the Ownership/idempotency/auditing/reversals rules, their decorators, interfaces, tests and scenarios) are a design only: NOT implemented in this repository, so they do not apply here; do not re-add them, or their interfaces, without being asked. Everything else (authentication, ownership, validation, logging, error handling, request limits, HSTS) is implemented as specified. Wherever a document mentions a feature that is not built, it must say so.

The rules below are grouped under the same six themes as [`docs/architecture-decisions.md`](../docs/architecture-decisions.md) and [`docs/architecture-summary.md`](../docs/architecture-summary.md). Each group starts with an HTML comment naming it.

<!-- GROUP 1. The shape of the solution -->

## Stack (ADR-4, 5, 6)
- .NET 10 LTS, C# 14 (do not set `LangVersion`); keep dependencies at latest stable
- ASP.NET Core Minimal APIs (ADR-2); FluentValidation; .NET DI for everything
- Scalar + `Microsoft.AspNetCore.OpenApi`, dev only; no Swashbuckle (done; wiring in `Documentation/ApiDocumentationExtensions.cs`, `AddApiDocumentation()` / `MapApiDocumentation()`)
- xUnit v3 (`xunit.v3` 4.x on Microsoft Testing Platform; ADR-6) + AwesomeAssertions; never FluentAssertions (paid from v8). The test project is an executable (`OutputType` `Exe`), and the root `global.json` enables MTP mode for `dotnet test` on the .NET 10 SDK: keep both
- No database, no EF Core: in-memory repository only (ADR-3)

## Principles (ADR-1)
- **KISS wins** over any other principle; no Clean Architecture, CQRS or Mediator
- Single API project, layered by folder into `Api/`, `BusinessLogic/`, `DataInterface/` and `DataLayer/` (see "Layers and naming")
- DRY; SOLID only where it keeps code simple and testable
- Declarative/functional style where possible (immutable records, pure functions), mindful of performance and memory; no premature optimisation
- Use established design patterns where they fit and, where one is used, name the pattern and say why in a comment on the type; never force a pattern in
- Folder layout is in the decisions doc ("Reference"); namespaces follow folders

<!-- GROUP 2. Behaviour of the payment flow -->

## Rules: behaviour
- Endpoints are thin: bind, call `IPaymentService`, map the result to HTTP
- `PaymentService` owns the flow: validate -> bank -> store
- Invalid input of any kind -> `Rejected` (HTTP 400); never call the bank; never store (ADR-8)
- Bank 503/timeout is an error to the merchant, not `Declined`; no retries (ADR-9)
- Validation (ADR-8): FluentValidation validators auto-registered by assembly scanning, called explicitly from `PaymentService` before any bank call; rules are the brief's (currency allowlist of at most 3 ISO codes) plus one deliberate addition: amount must be greater than zero (0 and negatives are `Rejected`)
- Card number, CVV and last-four are strings; amounts are integers in minor units
- Never store or return the CVV or full card number; never log either; redact `ToString()` on request types
- Repository storage must be thread-safe
- Use an injected `TimeProvider` for expiry checks

## Async (ADR-16)
- Async all the way for I/O (bank `HttpClient`, services that call it, endpoint handlers, repository interface); plain synchronous code for pure computation (validators, mapping classes, metrics recording): do not wrap those in `Task`
- Never block on async code: no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()`
- Never `async void` (except framework-mandated event handlers); return `Task`
- Always accept and forward a `CancellationToken` to every awaited call; Minimal API handlers take it as a parameter; the bank client sets an explicit timeout, and a timeout is a bank error, not a decline
- Async methods end in `Async`; default to `Task<T>` (no `ValueTask` without evidence); no `ConfigureAwait(false)`; no `Task.Run` in the request path; no fire-and-forget; never hold a `lock` across an `await`
- `IPaymentsRepository` methods return `Task<T>` even though the in-memory implementation uses `Task.FromResult` (the abstraction models storage, which is I/O)
- Tests: `async Task` methods, never `async void` or blocking; pass `TestContext.Current.CancellationToken` to every awaited call that accepts a token (the xUnit1051 analyzer enforces it; zero warnings); only cancellation tests use their own `CancellationTokenSource` (include one cancellation test for the bank client)

## Errors and exceptions (ADR-17)
- Expected outcomes are values, not exceptions: invalid input -> `Result` failure -> `Rejected` (400); bank declined -> normal `Declined` result; bank unavailable -> `Result` failure -> 502/503 (nothing stored); unknown payment id -> 404. Never throw-and-catch for flow control
- Catch specific exceptions only at the boundary that can fail (the bank client: `HttpRequestException`, timeout, `JsonException` from a malformed reply) and convert them to a bank-error `Result`; never `catch (Exception)` in business code; no swallowed exceptions; services and endpoints do not wrap work in `try/catch`
- Our timeout `OperationCanceledException` is a bank error; caller cancellation is not: let it propagate and do not log it as an error
- Global handler: `AddExceptionHandler`/`IExceptionHandler` + `AddProblemDetails` + `UseExceptionHandler`; unhandled errors return a generic RFC 9457 `ProblemDetails` 500 with the `traceId` and no stack trace, exception message or internals; log the exception once, in the handler (no catch-log-rethrow in other layers)
- Never put card data or request values in an exception message; `Rejected` responses list field names and messages but never echo submitted values
- Rethrow with `throw;` (never `throw ex;`); pass the original as the inner exception when wrapping; use `ArgumentNullException.ThrowIfNull` style guards for programmer errors only, not user input
- `Result<T>` is hand-written and simple (success value or a failure with a small error type); no third-party result library
- Misconfiguration fails fast at startup (`ValidateOnStart`); no automatic retries (ADR-9)
- Tests: assert on `Result` values, not thrown exceptions; one bank-client test per mapped failure (503, timeout, network failure, malformed JSON, caller cancellation); one integration test where an endpoint throws, expecting a 500 `ProblemDetails` with `traceId` and no stack trace or message

<!-- GROUP 3. Security and operations -->

## Rules: security and operations
- HTTPS everywhere (redirection + HSTS); nothing sensitive is persisted, so nothing needs encrypting at rest yet; keep every store (payments, transactions, audit, idempotency) behind an interface so an encrypting implementation can be substituted (ADR-7)
- Authentication: JWT bearer + scope policies (`payments:create`, `payments:read`, `payments:reverse`); scopes arrive in the space-separated `scope` claim, so use a custom requirement, not `RequireClaim`; dev tokens via `dotnet user-jwts`; never write token-issuing code, add an auth-disable switch or hard-code tokens; never forward the merchant's token to the bank (ADR-7)
- Logging: **Serilog** (`Serilog.AspNetCore`) behind `ILogger<T>`; application code only ever depends on `ILogger<T>`, never on Serilog types; configure it in an `Add…` extension method / `appsettings`, console sink with JSON output; structured message templates (named placeholders, never string concatenation or interpolation); log only payment id, status and bank outcome; sensitive data (card number, CVV, tokens) must never reach a log event; the default `traceId` (from `Activity`/`TraceIdentifier`) is the correlation id and is included in error responses; no custom correlation middleware (ADR-11)
- **Metrics are required (ADR-15):** OpenTelemetry (metrics) over `System.Diagnostics.Metrics`; one `Meter` named `PaymentGateway.Api`, wrapped in `IPaymentMetrics` (interface in `DataInterface/`, implementation in `DataLayer/`). Instruments: `payments.processed` (counter, tag `status` = authorized|declined|rejected), `bank.requests` (counter, tag `outcome` = authorized|declined|error), `bank.request.duration` (histogram, seconds), `payments.reversed` (counter), `idempotency.requests` (counter, tag `outcome` = replayed|conflict). Tags must be low-cardinality and never contain card data, ids, merchants or tokens. Also export the built-in ASP.NET Core and HttpClient metrics. Console exporter by default, OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Set up in an `Add…` method; metrics only (no traces/logs via OpenTelemetry; logging stays Serilog)
- Limit request body size (Kestrel `MaxRequestBodySize`, a few KB); rate limiting is a documented gap, not built (ADR-11)
- Do not change `imposters/` or `.editorconfig`

<!-- GROUP 4. Organisation, layers and naming -->

## Layers and naming (ADR-27)
- **One project, five layers plus a small shared folder, by top-level folder; namespaces follow the folders.** The project's root namespace is `PaymentGateway` (not `PaymentGateway.Api`), so a layer is a namespace of its own: `PaymentGateway.Api.Endpoints`, `PaymentGateway.BusinessLogic.Services`, `PaymentGateway.DataLayer.Bank`. Never let a name repeat (no `Api.Api`). **A file's namespace is exactly the root namespace plus its folder path** (API project: `PaymentGateway` + folder; test project: `PaymentGateway.Api.Tests` + folder); when a file moves, its namespace moves with it. `Unit/Architecture/NamespaceFolderTests` checks every file in both projects (only `Program.cs` and `Usings.cs`, which declare none, are exempt):
  - **`Api/`** – the HTTP surface: `Api/Endpoints/` (thin: bind, call a service, map the result to HTTP), plus the HTTP-only concerns `Api/Security/` (scopes, `CallerIdentity`), `Api/ErrorHandling/`, `Api/Documentation/`, `Api/Options/`. Endpoints stay thin
  - **`ApiInterface/`** – the API's contract, the counterpart of `DataInterface/`: the request and response DTOs in `ApiInterface/Dtos/Requests|Responses/`. Records only. It uses just `Shared` and the `DataInterface` enums
  - **`BusinessLogic/`** – the rules: `Services/`, `Validation/`, `Mapping/`, `Models/` (in-flight types such as `ProcessedPaymentModel`), `Interfaces/` (`IPaymentService`, `ICallerIdentity`, `IMapping<,>`)
  - **`DataInterface/`** – everything the business logic needs from outside itself, as interfaces (`IPaymentsRepository`, `IBankClient`, `IPaymentMetrics`) plus the types in their signatures: `Entities/`, `Enums/`, **`Dtos/Requests|Responses/` (the bank's request and response records)**, `Telemetry/`. It is the lowest layer and depends on no other layer
  - **`DataLayer/`** – the implementations of `DataInterface`: `Repositories/`, `Bank/` (`BankClient`, `BankOptions`), `Placeholders/` (`NoOpPaymentMetrics`)
  - **`Shared/`** – the small cross-layer vocabulary: `Shared/Outcomes/` (`Result`, `Error`, `ErrorKind`, `FieldError`). It depends on nothing of ours, so every layer may use it without breaking the one-way rule. Add to it only types that several layers genuinely need and that need no other layer
  - **`PaymentGatewayDi.cs` (a single static class at the project root, namespace `PaymentGateway`) and `Program.cs`** are the composition root: the only places that wire the layers together
- **DTOs belong to an interface layer, in a `Dtos/` folder of it (ADR-27).** The API's request and response DTOs are the API's interface, so they live in `ApiInterface/Dtos/`, just as the bank's messages, part of `IBankClient`, live in `DataInterface/Dtos/`. They are not in `Api/` or `BusinessLogic/`: the endpoints and the services both use them and neither owns them. **Requests and responses are DTOs** (the `Request`/`Response` suffix is the DTO marker; there is no `Dto` suffix), so every `…Request` lives in a `Dtos/Requests/` folder and every `…Response` in a `Dtos/Responses/` folder, the same way in both interface layers, and nothing else lives there; `NamingConventionTests` enforces it. Entities are not DTOs: they live in `DataInterface/Entities/` and are never returned to a caller
- **Dependencies point one way:** `Api` -> `BusinessLogic` and `ApiInterface` (and the `DataInterface` enums); `BusinessLogic` -> `ApiInterface` and `DataInterface`, never `DataLayer` or `Api`; `ApiInterface` -> the `DataInterface` enums only; `DataLayer` -> `DataInterface` only; `DataInterface` -> nothing above it; any layer may use `Shared`, and `Shared` uses no layer. **There are no exceptions** (an interface in a lower layer must never return a type from a higher one). `Unit/Architecture/LayerDependencyTests` enforces this: a new dependency that breaks it fails the build, so fix the code, not the test
- **Keep the contracts separable (ADR-27, "Possible evolution").** `ApiInterface` and `DataInterface` are written so they could become their own projects or services: they depend on no implementation and nothing reaches up. Consumers use a contract, implementers implement one, and neither depends on the other; whether to actually split depends on project complexity (one team, one deployable: keep one project)
- **Entities are named `[Thing]Entity` (ADR-26, ADR-27):** records in `DataInterface/Entities/` (`PaymentEntity`, `TransactionEntity`, `AuditEventEntity`) and nothing else lives in that folder (enums go in `DataInterface/Enums/`); a DTO is never called `…Entity` and an entity is never called `…Request`/`…Response`
- **A type's name shows its role (ADR-27):** DTOs end in `Request` or `Response` (the API's in `ApiInterface/Dtos/Requests|Responses/`, the bank's in `DataInterface/Dtos/Requests|Responses/`); entities end in `Entity` (`DataInterface/Entities/`); in-flight business models end in `Model` (`BusinessLogic/Models/`, e.g. `ProcessedPaymentModel`). **No `Dto` suffix**: `Request`/`Response` already marks a DTO, and `PostPaymentResponseDto` would say it twice (use `Dto` only for a shared shape that is neither a request nor a response, and then add it to this rule and to `NamingConventionTests`). `Unit/Architecture/NamingConventionTests` enforces all of this

## Writing for the reviewer (ADR-28)
A reviewer reads this cold and quickly. These principles apply to every change, and to the README and the docs as much as to the code:
- **Make the core path obvious and short.** The few files that implement the brief (endpoints, `PaymentService`, the validator, the bank client, the repository) stay small, flat and readable top to bottom; the README lists them in reading order with their sizes, and that list is updated when they change
- **Label what can be skipped.** Anything not needed to understand the brief (placeholders, composition, architecture tests) is named as skippable in the README, and placeholders carry a `PLACEHOLDER` comment
- **Say where the work departs from the brief, in one place.** The README has a single "Where this departs from, or interprets, the brief" table (what, why); any new departure or interpretation is added to it in the same commit. Never leave it only in an ADR or a code comment
- **Layer the documentation by reader.** README: what it is, how to run it, what to read, what differs (about 150 lines at most). ADRs: the reasoning, with a "start here" index at the top of the file. `CLAUDE.md`: working rules for the assistant, which a reviewer can ignore. Do not repeat the same explanation in all three; link instead
- **Keep the docs true.** A change that alters behaviour, structure, a rule or a number updates the README, `CLAUDE.md` and the ADR checklist in the same commit; a stale statement is a defect. Prefer a test that proves a claim over a sentence that asserts it (the contract, architecture and naming tests do this)
- **Every top-level folder explains itself.** Each layer folder (`Api`, `ApiInterface`, `BusinessLogic`, `DataInterface`, `DataLayer`, `Shared`) and the test project have a short `README.md` (what it is, what it may depend on, where to start) that GitHub shows when browsing; keep them to about ten lines. `Unit/Architecture/FolderReadmeTests` fails if one is missing, so a new top-level folder needs one
- **No unused code.** Every type and member is used, or is a labelled placeholder contract (`PLACEHOLDER`, or a comment saying which interface contract it belongs to). Delete dead code when you find it: a reviewer will ask what it is for
- **Group by role, not by history.** A folder is named for the role of what is in it; related code sits together; code a reviewer can skip (placeholders, wiring, architecture tests) is in clearly named folders, never mixed in with the core path
- **Keep the core path small.** The core files stay short (the README states their sizes); when one grows past about 80 lines, split it by role rather than letting the first file a reviewer opens be the longest
- **Names and comments carry the why.** A type name shows its role; a type comment says in one sentence why it exists and cites the ADR where there is one; no comment restates the code. Prefer a self-describing name to an explanatory comment
- **Show the evidence, not just the claim.** The README states how to run the tests and the smoke test, and what they cover, so a reviewer can verify rather than trust

## Dependency injection (ADR-12)
- **`PaymentGatewayDi` is the only composition root (ADR-12):** one static class, `PaymentGatewayDi.cs` at the project root, knows every layer and wires them together. It has **exactly one public method, `InitialiseDi`, and it is the first method in the class**: it is the root DI initialisation, and `Program.cs` calls it once. Constructor injection of interfaces everywhere else; never a service locator, static access or `new HttpClient()`
- **Every mapping from an interface to its concrete implementation is registered in `PaymentGatewayDi`**, in one `internal static` extension method per concern (`internal static IServiceCollection Add…(this IServiceCollection services, …)`) written below `InitialiseDi`, which chains them in order. No `services.AddX<I, T>()` inline in `Program.cs`, in endpoints or in tests; `Program.cs` only calls `InitialiseDi` and the `Use…`/`Map…` methods. The helpers are internal but visible to the test project, so tests reuse them and override only the edges
- Choose lifetimes deliberately (repository = singleton and thread-safe; never inject scoped into singleton); keep `ValidateOnBuild`/`ValidateScopes` on
- Bank client = typed `HttpClient` (`AddHttpClient<IBankClient, BankClient>()`); config via options with `ValidateOnStart`; inject `IOptions<T>`, not `IConfiguration`
- Every non-trivial class (anything with logic) has an interface (services and mappings in `BusinessLogic/Interfaces/`, anything that reaches outside the business logic in `DataInterface/`); classes, records and structs with no business logic (DTOs, entities, database objects, options) need none (ADR-13). Never wrap `ILogger`, `TimeProvider` or `IOptions` in our own interfaces
- Mocking: **use NSubstitute** (`Substitute.For<T>()`) by default. Exception: where mocking would cause undue complexity (e.g. elaborate multi-step setup, callbacks or state that a mock expresses badly) use a small hand-written test double instead, provided it is clearly more readable; keep it in `TestSupport/` and say in a comment why a mock was not used. Any collaborator may be substituted when that makes a unit test clearer (there is no "edges only" restriction); use `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) for the clock. Verify interactions only where the interaction is the requirement (e.g. `DidNotReceive()` on the bank for a rejected payment); otherwise assert on outcomes
- Unit tests construct classes directly with an NSubstitute `IBankClient` (no container)
- Integration tests use `WebApplicationFactory<Program>` and override only `IBankClient` (an NSubstitute substitute), `TimeProvider` (`FakeTimeProvider`) and authentication in `ConfigureTestServices`; never re-register app internals by hand; fresh factory/repository per test class
- `Program` must be visible to tests: `Microsoft.AspNetCore.Mvc.Testing` exposes the internal top-level `Program` to the test assembly automatically (verified); add `public partial class Program;` only if that stops working

## Code conventions
- File-scoped namespaces matching folders; primary constructors
- **Global usings (ADR-24):** one `Usings.cs` per project (the API project and the test project each have their own; keep the test project's existing name), with `global using` for framework and our own namespaces used by three or more files in that project; keep `ImplicitUsings` enabled and add only what it does not supply; per-file `using`s are only for namespaces needed by one or two files. Group framework, then third-party, then our own, each alphabetised. Do not make a namespace global if it creates ambiguous type names or only one or two files use it; avoid `global using static`; no shared global-usings list across projects
- **Minimise boilerplate (ADR-24):** use language features that remove ceremony (records, primary constructors, expression-bodied members, target-typed `new`, collection expressions, pattern matching); extract repeated code on its third occurrence (rule of three), not on the first or second; in tests move repeated arrange code into builders/helpers in `TestSupport/` and use `[Theory]` for the same check over several inputs; no comments or XML docs that merely restate the code; never remove boilerplate with reflection or convention magic that hides behaviour
- **Favour records over classes for DTOs and entities (ADR-20):** immutable (`init`-only) `record` types; use a plain class only when a type needs identity-based equality, mutable state or behaviour (and then it has logic, so ADR-13 applies); a record holding sensitive data must override `ToString()` to redact it (ADR-9)
- Object mapping (ADR-14): where a mapping between types is needed, write a mapping class by hand in `BusinessLogic/Mapping/` that implements the generic `IMapping<in TSource, out TDestination>` (`TDestination Map(TSource source)`); never use AutoMapper or any `IMapper`/mapping library; one class per mapping, registered in an `Add…` method like any other interface and unit-tested first (TDD). Do not create a mapping class for a trivial one-line conversion
- **DTOs and storage entities are separate types (ADR-26):** request/response DTOs and bank message types are never stored; each store persists its own entity record (payments: `PaymentEntity`, holding the owning `MerchantId` and the last four card digits only, never the full card number or CVV). Convert between DTO, in-flight and entity types with `IMapping` classes (flow: request -> `ProcessedPaymentModel` -> `PaymentEntity` -> response). Never return or accept an entity at the API boundary
- **Storage holds no business logic (ADR-26):** stores (`IPaymentsRepository`, and, if they were built, the ledger, audit trail and idempotency store) only add and read by key, plus atomic primitives that only a store can guarantee (check-and-append, reserve); they take no caller identity, apply no ownership or status rules, decide no retention and build no responses. The service decides, e.g. `PaymentService.GetAsync` compares the entity's `MerchantId` with `ICallerIdentity` and answers "not found" for another merchant's payment. Store tests cover storage only; business rules are tested at the service
- No stored procedures
- Naming: `[Thing]Service`, `[Thing]Client`, `[Request]Validator`, `[Entity]Response`, `[Action][Entity]Request`

## Service interfaces and decorators (ADR-25; the decorators and `IReversalService` are designed only, NOT implemented)
- Interfaces (in `BusinessLogic/Interfaces/` and `DataInterface/`): `IPaymentService`, `IReversalService`, `ICallerIdentity` (gives `InitiatedBy` / `OnBehalfOf` from the token), alongside `IPaymentsRepository`, `IBankClient`, `IMapping<,>`, `IPaymentMetrics`, `IIdempotencyStore`, `IAuditTrail`, `ITransactionLedger`
- Idempotency and audit are **decorators** implementing the same service interface (`IdempotentPaymentService`, `AuditedPaymentService`; same for reversals), composed explicitly in an `Add…` method: audit outermost, then idempotency, then the core service; no decoration library; name the Decorator pattern in a comment. Never fold them into `PaymentService`
- Validators use FluentValidation's own `IValidator<PostPaymentRequest>` (no custom validator interface); the validator gets `TimeProvider` by injection; numeric request fields are nullable so "required" works; messages never contain submitted values

<!-- GROUP 5. Testing, code quality and workflow -->

## Size limits (ADR-18)
- **Methods:** at most 20 lines (30 hard, excluding blank lines and braces); cognitive complexity at most 10; nesting at most 2 levels (3 hard, use guard clauses and early returns); at most 3 parameters (4 hard)
- **Classes / files:** at most 200 lines (300 hard); test classes at most 250 (400 hard)
- **One type per file (ADR-19):** every class, record, struct, interface and enum has its own file named after the type; no nested or companion types sharing a file
- **`Program.cs`:** at most 50 lines (80 hard); it only calls the `Add…` and `Map…` methods
- **Endpoint extension classes:** at most 150 lines (250 hard); split by feature. **`PaymentGatewayDi`** is the one exception: a single class holding every `Add…` registration, at most 200 lines (250 hard), one method per concern; if it outgrows that, split it by layer into partial files named `PaymentGatewayDi.[Layer].cs` rather than back into a folder
- Hitting a limit means extract a collaborator or a named step, not compress code or join lines; do not split a small cohesive class just to get under a number
- Exceptions: Arrange-Act-Assert tests may exceed the method limit while staying readable (move heavy Arrange into helpers/builders); flat registrations and flat one-field-per-line mappings may exceed it; generated code is not counted

## TDD and interfaces (ADR-13)
- **TDD for anything with logic:** write the failing test first, make it pass, then refactor
- **No TDD required** for a class, record or struct that contains no logic (plain data holders: DTOs, entities, database objects, options); do not write tests that only assert property getters/setters
- **Interfaces:** all other non-trivial classes have an interface; a DTO/entity/database object with no business logic does not
- Logic added to a data type later (validation, calculation, state changes) makes it non-trivial: from then on it needs TDD and an interface

## Testing (ADR-6)
- **Keep unit and integration tests apart by directory** inside the single test project: `Unit/` and `Integration/` (including `Integration/Simulator/`); never mix them in one folder or class
- `Unit/` mirrors the implementation's directory structure: tests for `BusinessLogic/Services/PaymentService.cs` live in `Unit/BusinessLogic/Services/PaymentServiceTests.cs`, for `DataLayer/Repositories/` in `Unit/DataLayer/Repositories/`, and so on; one test class per class under test, named `[ClassUnderTest]Tests` (the `Tests` suffix keeps test names from colliding with implementation names)
- **Test count limit (ADR-19):** at most 10 test methods per test class and file (15 hard); a `[Theory]` with several `InlineData` rows counts as one. When a class would exceed it, split it by functional area instead of growing it:
  - replace `PaymentServiceTests.cs` with a directory `PaymentServiceTests/` (named after the class under test, `Tests` postfix) holding one class and file per functional area, each named `[FunctionalArea]TestClass` (singular: the file holds a single test class, never `TestClasses`; `NamingConventionTests` enforces it) (e.g. `PaymentServiceTests/BankFailureTestClass.cs`)
  - namespaces follow the folders; a class under test has either a single `[Name]Tests.cs` file or a `[Name]Tests/` directory, never both (a type and a namespace of the same name collide)
  - the same limit and naming apply to integration tests, split within their feature folder
- `Integration/` is organised by feature/endpoint (e.g. `Integration/Payments/…`), not by implementation class
- Namespaces follow the folders; shared helpers (test-token factory, web app factory setup) go in `TestSupport/`
- Unit: validators and `PaymentService` with a substitute `IBankClient`
- **Integration tests are required** for every endpoint and run in plain `dotnet test` with no Docker (`WebApplicationFactory`, substituted bank client, test tokens). A change to an endpoint is not done until these pass. Minimum scenarios (ADR-6):
  - POST authorized (card ends odd), declined (ends even), rejected (each invalid-field class and malformed JSON: bank not called, nothing stored)
  - POST bank error (503/timeout): error response, nothing stored
  - GET existing payment (round trip: POST then GET returns the same data), GET unknown id -> 404
  - 401 with no/invalid token, 403 with the wrong scope, on both endpoints
  - No response body ever contains the full card number or CVV
  - Contract (`Integration/Contract/ApiContractTests`): the brief's exact JSON field names and the `Authorized`/`Declined`/`Rejected` status strings, posted and read as raw JSON (never our DTO types), so renaming a C# type or property cannot silently change the contract
  - Idempotency: replay returns the identical response and the bank substitute got exactly one call; same key with a different body -> 422; absent key -> processed normally, empty/over-long key -> 400; concurrent same-key requests reach the bank once; a rejected request does not consume the key
  - Ownership: another merchant's payment -> 404 on GET, transactions and reversal
  - Reversal: original payment unchanged and ledger sums to zero; second reversal -> 409; declined payment -> 409; unknown payment -> 404; replayed reversal returns the original response; concurrent reversals create exactly one transaction
  - Audit: each listed action produces exactly one matching event with the right `InitiatedBy`, `OnBehalfOf` and `traceId`, including a case where the two differ (a token whose `sub` is not the merchant); events never contain card data
- Simulator: a few tests against the real Mountebank simulator (needs `docker-compose up -d`), tagged `[Trait("Category", "Simulator")]` and skipped when it is unreachable (ADR-6)
- Names: `[Method]_[Scenario]_[ExpectedResult]`
- **Arrange-Act-Assert (ADR-6):** every test follows AAA and marks each section with an explicit comment: `// Arrange`, `// Act`, `// Assert`, in that order, with one blank line between sections; exactly one Act per test (use `[Theory]` for more inputs, not more Acts); when a section is empty or done elsewhere keep the comment and say where (e.g. `// Arrange: done in the constructor`); no assertions in Arrange and no setup in Assert
- **Shared setup is abstracted, not copied (ADR-6, ADR-24):** setup shared by several tests goes into the constructor (xUnit makes a new instance per test) with `IDisposable.Dispose` for teardown, or `IAsyncLifetime` when it is async; expensive setup shared within a class (e.g. a `WebApplicationFactory`) uses `IClassFixture<T>`, and across classes `ICollectionFixture<T>`; fixtures and builders live in `TestSupport/`. Shared fixtures must hold no mutable state that one test could leak into another

## Commands
```
docker-compose up -d                          # bank simulator :8080 (UI :2525)
dotnet build PaymentGateway.sln
dotnet test                                   # MTP mode via global.json; also: dotnet test PaymentGateway.sln
dotnet test --filter-namespace "PaymentGateway.Api.Tests.Unit.*"   # unit tests only (VSTest --filter does not apply)
dotnet test --filter-trait "Category=Simulator"                    # needs docker-compose up -d; skipped otherwise
dotnet test --coverage --coverage-output-format cobertura          # Microsoft.Testing.Extensions.CodeCoverage; output in TestResults/
dotnet run --project src/PaymentGateway.Api   # https://localhost:7092, Scalar at /scalar/v1 (dev)
dotnet format
pwsh scripts/build-how-it-works-html.ps1             # regenerate docs/how-it-works.html after editing docs/how-it-works.md
```

## Running the app locally
- Never leave the API running: a leftover `dotnet` process locks the build output (MSB3021/MSB3027). To check the real app use `pwsh scripts/smoke-test.ps1`, which always stops the process tree it started. For any other manual run, start it with a tracked PID, stop it in the same command (`taskkill /PID <pid> /T /F`), and never kill by guessing a PID or by process name
- Before a build that fails with a file-lock error, list `dotnet` processes (command line and port) and stop only ours

## Git (ADR-10)
- The repository now has one branch, `main`; the earlier `dev` and `lean` branches were squashed into it and removed from `origin`. Small commits in `type: description` form (feat, fix, refactor, test, docs)
- Commit only when build and tests pass; tag `checkpoint-N` before risky changes; never open PRs against `upstream` (`cko-recruitment`)

<!-- GROUP 6. Designed but NOT IMPLEMENTED in this repository -->

## Ownership, idempotency, auditing and reversals (ADR-7, 21, 22, 23; ownership is implemented, the rest is designed only, NOT implemented)
- **Two identities on every request (ADR-7), taken only from the validated token, never the body:** originator `InitiatedBy` = the `sub` claim (the principal who made the call); `OnBehalfOf` = the `merchant_id` claim (fallback: client id), the merchant account the operation is performed for. They are equal when a merchant acts for itself and may differ (support user, platform). Record both everywhere an actor is recorded
- **Ownership:** store `OnBehalfOf` as `MerchantId` on every payment and transaction; every endpoint that reads or changes a payment returns 404 for another merchant's payment
- **Idempotency:** `Idempotency-Key` header is **optional** on `POST /api/payments` and `POST /api/payments/{id}/reversals` so requests written to the brief still work (absent -> process normally, no replay protection; present but empty/over 255 chars -> 400); keys are scoped to merchant + operation; store a keyed HMAC-SHA256 fingerprint of the request (never the card number, never a plain hash) with the outcome, expiring after 24h via `TimeProvider`; atomic reservation of the key
  - same key + same request, completed -> replay the stored response and status with `Idempotent-Replayed: true`; the bank is not called again
  - same key + different request -> 422 `ProblemDetails`; same key while in progress -> 409 `ProblemDetails`
  - record only completed outcomes (bank authorized/declined, successful reversal); `Rejected`, bank errors, 404 and 409 release the key
  - after a bank timeout the outcome is unknown: no automatic retries, document the double-charge risk as a known gap
- **Auditing:** every security- and money-relevant action appends an immutable `AuditEvent` record to an append-only `IAuditTrail` (append and read only, no update or delete): payment attempted (written before the bank call), authorized/declined/rejected/bank error, retrieved, reversal requested/completed/refused, idempotent replay and conflict, 401 and 403. Fields: event id, UTC time (`TimeProvider`), **originator `InitiatedBy` and on-behalf-of `OnBehalfOf`** (both always recorded, even when equal; both empty for a 401), action, resource type and id, outcome, `traceId`, idempotency key. **Never** the card number (not even the last four), CVV, tokens or bodies. The audit trail is not the log
- **Reversals:** a completed payment is never changed or deleted; a reversal is a new immutable balancing `Transaction` (signed amount, linked to the original, `Reversal` type, carrying `InitiatedBy` and `OnBehalfOf`) in an append-only `ITransactionLedger` with atomic check-and-append. Only `Authorized` payments, once, full amount only; payment status stays `Authorized`; declined/rejected payments write no transaction
  - `POST /api/payments/{id}/reversals` (scope `payments:reverse`, 201) and `GET /api/payments/{id}/transactions` (scope `payments:read`, entries + net balance)
  - 404 unknown/foreign payment; 409 `ProblemDetails` for declined or already reversed
  - the simulator has no reversal route, so no bank call is made: document it as a known gap
- All of the above: records (ADR-20), interfaces + `Add…` registration (ADR-12/13), async (ADR-16), tests first with the scenarios in the Testing section

<!-- STATUS: where the work stands -->

## Status
Done so far: net10.0 retarget; string card fields, thread-safe async `IPaymentsRepository`, redacted request `ToString()`, records, `Add…` registration; Scalar/OpenAPI; `PostPaymentRequestValidator` with tests; `Result<T>`, `IBankClient`/`BankClient` (options validated on start), `IMapping<,>` with the request-to-bank mapping, API-project `Usings.cs`. `IPaymentService`/`PaymentService` (validate, bank, store) with `ProcessedPaymentModel` and its response mapping; `Error.FieldErrors`. Minimal API endpoints (`POST /api/payments` -> 200 / 400 `Rejected` / 502 `ProblemDetails`, `GET /api/payments/{id}`), `PaymentsController` removed, string enum serialisation, `BadRequestExceptionHandler` (malformed JSON -> `Rejected`), `UnhandledExceptionHandler` (generic 500 `ProblemDetails` with `traceId`, logs once). Serilog (`AddStructuredLogging`, JSON console, `traceId` as `@tr`, `preserveStaticLogger: true` so parallel test hosts do not share the global logger). JWT bearer authentication with scope policies on both endpoints (`scripts/dev-token.ps1` for dev tokens; tests sign their own JWTs). `ICallerIdentity` and per-merchant ownership (payments are stored per merchant; other merchants get 404; `MapInboundClaims = false`). Idempotency, the audit trail, real metrics and reversals are NOT implemented (designed in ADR-15, 21, 22, 23; the metrics seam is the no-op `NoOpPaymentMetrics`). README written and `dotnet format` clean (run `dotnet format --verify-no-changes` before committing). HTTPS redirection plus HSTS (`UseTransportSecurity`), an authorization fallback policy (unlisted endpoints fail closed), request-body size limit (`AddRequestLimits`, 4 KB, 413), tagged Simulator tests (pass against the real Mountebank 2.8.1), and global usings are done. The lean implementation was squashed into `main` and pushed. The checklist under "Implementation status" in the decisions doc says which items are implemented and which are design only. The README documents assumptions, known gaps (including the idempotency timeout risk) and how to obtain a dev token.
