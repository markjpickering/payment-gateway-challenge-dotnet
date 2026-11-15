# Payment Gateway

An API that lets a merchant take card payments: it validates the request, forwards it to an acquiring bank
(simulated) and stores the result so it can be retrieved later. Built for the Checkout.com payment gateway
challenge on .NET 10 (ASP.NET Core Minimal APIs).

> **This is the `lean` branch:** what the brief asks for, plus authentication and some hardening. Idempotency, an audit trail and
> reversals are not built, and metrics is a no-op placeholder; see [Beyond the brief](#beyond-the-brief).

## Quick start

```bash
docker-compose up -d                                # bank simulator on :8080 (UI on :2525)
dotnet run --project src/PaymentGateway.Api         # https://localhost:7092, docs at /scalar/v1
pwsh scripts/dev-token.ps1                          # prints a development bearer token
```

Every payment endpoint needs a bearer token (see [Authentication](#authentication)):

```bash
TOKEN=$(pwsh scripts/dev-token.ps1)

curl -k -X POST https://localhost:7092/api/payments \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'

curl -k https://localhost:7092/api/payments/<id> -H "Authorization: Bearer $TOKEN"
```

Interactive documentation (Scalar) is served at `/scalar/v1` in the Development environment only; use its
Authorize box to paste the token.

Run the tests with `dotnet test` (no Docker needed: the bank is substituted). With the simulator running,
`dotnet test --filter-trait "Category=Simulator"` runs the tests against the real bank simulator.
`pwsh scripts/smoke-test.ps1` starts the real app, checks a few requests against it and always stops it again.

## Reading guide

The brief itself (two endpoints, validation, the bank call, storage) is five small files, about 260 lines. Read them
in this order:

| # | File | What it shows |
|---|---|---|
| 1 | `src/PaymentGateway.Api/Api/Endpoints/PaymentEndpoints.cs` (34 lines) | The two endpoints: bind, call the service, map the result to HTTP |
| 2 | `.../BusinessLogic/Services/PaymentService.cs` (59) | The flow: validate, call the bank, store; ownership on read |
| 3 | `.../BusinessLogic/Validation/PostPaymentRequestValidator.cs` (71) | The brief's validation rules |
| 4 | `.../DataLayer/Bank/BankClient.cs` (71) | The call to the bank simulator and how each failure is handled |
| 5 | `.../DataLayer/Repositories/PaymentsRepository.cs` (25) | The in-memory store |

A class diagram, a sequence diagram of a payment and a summary of how the software works are in [docs/how-it-works.md](docs/how-it-works.md) (also as a standalone page, `docs/how-it-works.html`, that opens in any browser).

**Tests worth reading:** `Integration/Payments/` (the whole flow through the real app), `Integration/Contract/ApiContractTests.cs`
(the brief's exact JSON field names and statuses) and `Unit/BusinessLogic/Validation/` (every validation rule).

**The code is in five layers plus a tiny shared folder** (under `src/PaymentGateway.Api/`):

| Folder | Holds |
|---|---|
| `Api/` | The endpoints, plus security, error handling and the OpenAPI docs |
| `ApiInterface/` | The API's contract: the request and response DTOs |
| `BusinessLogic/` | Services, validation, the mappings and the service interfaces |
| `DataInterface/` | The interfaces the business logic needs from outside (store, bank, ...), the entities and the bank's DTOs |
| `DataLayer/` | The implementations: the in-memory repository, the bank client and the metrics placeholder |
| `Shared/` | `Result` and `Error`, used by every layer |

Each of these folders also has its own short `README.md` (what it is, what it may depend on, where to start).

**You can skip:** `PaymentGatewayDi.cs` (all the wiring, in one class), `DataLayer/Placeholders/` (the metrics no-op) and `Unit/Architecture/` (tests that keep the layers, names and folders consistent).

## API

| Method | Path | Scope | Result |
|---|---|---|---|
| `POST` | `/api/payments` | `payments:create` | `200` with the payment (`Authorized` or `Declined`); `400` `Rejected`; `502` if the bank could not be used |
| `GET` | `/api/payments/{id}` | `payments:read` | `200` with the payment, or `404` |

Request: `cardNumber` (14–19 digits), `expiryMonth` (1–12), `expiryYear`, `currency` (`USD`, `GBP` or `EUR`),
`amount` (integer greater than zero, minor units, e.g. pence), `cvv` (3–4 digits). The expiry date must be in the future.

Response: `id`, `status`, `cardNumberLastFour`, `expiryMonth`, `expiryYear`, `currency`, `amount`. The full
card number and the CVV are never stored, returned or logged.

- **`Rejected`:** any invalid request, including a missing, malformed or wrongly typed body, gets `400` with
  `{ "status": "Rejected", "errors": [{ "field", "message" }] }`. The bank is not called and nothing is stored.
  Messages never repeat what was submitted.
- **`Declined`:** the bank said no. This is a normal `200` result and is stored.
- **Bank unavailable:** a bank `503`, a timeout or an unreadable reply is an error to the merchant (`502`
  `ProblemDetails` with a `traceId`), not a decline. Nothing is stored, and there are no automatic retries.
- **Anything unexpected:** a generic `500` `ProblemDetails` with a `traceId` and no internals.

## Authentication

The brief does not ask for authentication, but an API that takes card payments should not be open, so every
payment endpoint requires a JWT bearer token with a scope: `payments:create` to take a payment,
`payments:read` to retrieve one.

- **Development:** `scripts/dev-token.ps1` wraps `dotnet user-jwts`. The signing key lives in your user
  secrets, not in the repository, and the API never issues tokens itself. Options:
  `-Merchant merchant-2`, `-Scopes payments:read`.
- **Whose payments:** each payment belongs to the merchant in the token's `merchant_id` claim (falling back to
  `client_id`, `azp` and then `sub`). Another merchant gets `404` for it. The token's `sub` (who made the
  call) and the merchant (who it was made for) are separate identities and may differ.
- **No keys configured means no access:** with nothing under `Authentication:Schemes:Bearer` every token is
  rejected. There is deliberately no switch to turn authentication off.
- **Production design (not built):** OAuth 2.0 client credentials from an identity provider, with the
  gateway validating the issuer, audience, signature and expiry exactly as it does now, and mTLS or signed
  requests for high-value merchants. The merchant's token is never forwarded to the bank.

## Design

Reasoning for every decision, with the alternatives rejected, is in [`docs/architecture-decisions.md`](docs/architecture-decisions.md)
(it starts with an index of the ones that matter, and is grouped by theme; [`docs/architecture-summary.md`](docs/architecture-summary.md) is a one-page digest). [`.claude/claude.md`](.claude/claude.md) holds the short working rules the
code was written to; a reviewer can ignore it. In brief:

- **Simple on purpose:** one project, five layers by folder, no Clean Architecture, CQRS or mediator. Expected failures
  are `Result` values, not exceptions. Architecture tests keep the layers, the type names and the JSON contract consistent.
- **Storage:** an in-memory, thread-safe repository that stores its own `PaymentEntity` (never the response DTO) and holds
  no business logic: the service decides who may see a payment.
- **Safe by default:** the full card number and CVV are never stored, returned or logged; HTTPS redirection and HSTS
  outside Development; bodies capped at 4 KB; Serilog JSON logs with the request's trace id.
- **Tests:** unit tests mirror the source folders; integration tests use the real app with only the bank substituted;
  every test follows Arrange-Act-Assert.

## Beyond the brief

A real payment gateway needs more than the brief describes. These concerns are **required for production** but are
**not built on this branch**, so a reviewer does not have to read them. Each has a documented design
([`docs/architecture-decisions.md`](docs/architecture-decisions.md)); there is no interface or stub for them in the code,
because nothing running would call it.

| Concern | Why a gateway needs it | Design |
|---|---|---|
| **Idempotency** | A merchant retrying a timed-out `POST` must not take the payment twice. An `Idempotency-Key` header makes a repeat replay the first response. | ADR-21 |
| **Audit trail** | Disputes, fraud investigation and compliance need an append-only record of who did what, for which merchant, with what result (never card data). | ADR-22 |
| **Reversals** | Money taken in error must be returned without editing history: a reversal is a new balancing ledger entry, once, in full. | ADR-23 |
| **Metrics** | Operators watch payment, decline and bank-error rates and bank latency. | ADR-15 |

Metrics is the one with a seam: the bank client already calls `IPaymentMetrics`, which is a **no-op placeholder**
(`DataLayer/Placeholders/NoOpPaymentMetrics`, registered by `AddPaymentMetrics()`). A real implementation is a one-line change there.

## Where this departs from, or interprets, the brief

| Topic | What the code does | Why |
|---|---|---|
| **Authentication** | Every endpoint needs a JWT bearer token with a scope. `scripts/dev-token.ps1` makes one. | The brief does not mention it, but an open payments API is unsafe. See [Authentication](#authentication). |
| **Amount** | Must be an integer **greater than zero**. | The brief only says "integer"; zero or a negative amount is never a real payment. |
| **Bank unavailable** | A bank `503`, a timeout or an unreadable reply gives `502` with a `ProblemDetails` body, not `Declined`. Nothing is stored. | The brief's three outcomes (Authorized, Declined, Rejected) do not cover a bank that could not answer. |
| **Expiry** | A card is valid through the last day of its expiry month. | "In the future" is ambiguous for the current month; this is how card expiry works. |
| **Currencies** | `USD`, `GBP`, `EUR` only. | The brief allows at most three. |
| **Rejected** | HTTP `400` with `{ "status": "Rejected", "errors": [...] }`, including for malformed JSON. | The brief does not give a status code or a body for Rejected. |
| **Idempotency, audit trail, reversals, metrics** | Not built (designed in the ADRs); metrics is a no-op placeholder. | Needed in production, not asked for. See [Beyond the brief](#beyond-the-brief). |

**Bank simulator:** a card number ending in an odd digit is authorized, an even digit is declined, and `0` makes it answer `503`.

## Known gaps

- **The concerns under [Beyond the brief](#beyond-the-brief)** are not built. In particular, without idempotency a
  client that retries a timed-out `POST` can take a payment twice.
- **Everything is in memory** and lost on restart; a real store must also encrypt card-related data at rest.
- **Single project on purpose.** For a larger system the contracts (`ApiInterface`, `DataInterface`) would be separated from the code that implements and consumes them, as projects and then possibly services; see ADR-27 ("Possible evolution").
- **No rate limiting** (throttling would be done at the edge), and **`AllowedHosts` is `*`**: a deployment should restrict it.
