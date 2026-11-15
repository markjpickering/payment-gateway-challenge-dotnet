# Testing guide: set up, authenticate, try it

Every payment endpoint needs a bearer token, so **get a token first** (step 2). There is no way to switch authentication
off, on purpose (see [Authentication](../README.md#authentication)). Everything below was run against the real app.

**You need:** the .NET 10 SDK, and Docker only if you want the real bank simulator.

## The one-command way

```bash
pwsh scripts/start.ps1
```

It starts the bank simulator if Docker is installed (and warns if not), makes a development token, **copies it to your
clipboard**, prints a ready-to-paste `curl` example and runs the API; Ctrl+C stops it. In the Scalar UI that opens, click
**Authorize** only if it is empty. Options: `-TokenOnly` (just the token and the example, no server), `-Merchant merchant-2`,
`-Scopes payments:read`, `-NoBrowser`.

**In Visual Studio:** run `pwsh scripts/dev-token.ps1` once (it creates the signing key in your user secrets; the Terminal
window in Visual Studio will do), then just press **F5**. The Scalar page opens already signed in (see step 4). There is no
separate profile for tokens: in a Debug build the page is pre-loaded with one.

The rest of this guide is the same thing step by step.

## 1. Start the bank simulator (optional)

```bash
docker-compose up -d        # Mountebank on :8080 (admin UI on :2525)
```

Without it the API still runs, but a valid payment answers `502` ("the bank could not be reached"). Invalid requests,
`401`, `403` and `404` do not need the bank.

## 2. Get a token

```bash
pwsh scripts/dev-token.ps1                      # merchant-1, scopes payments:create + payments:read
```

- The first run creates the signing key in your **user secrets** (not in the repository), using `dotnet user-jwts`.
  The issuer and audiences are already in `appsettings.Development.json`, so there is nothing to configure.
- The script prints just the token. In bash: `TOKEN=$(pwsh scripts/dev-token.ps1)`; in PowerShell: `$token = pwsh scripts/dev-token.ps1`.
- Variants, to test the security rules:

| Command | Use it to see |
|---|---|
| `pwsh scripts/dev-token.ps1 -Merchant merchant-2` | Another merchant: `GET` of `merchant-1`'s payment gives `404` |
| `pwsh scripts/dev-token.ps1 -Scopes payments:read` | Wrong scope: `POST` gives `403` |
| (no `Authorization` header) | `401` |

The token only works against the app run in the **Development** environment (the default for `dotnet run` and the
launch profile). With no signing key configured, every token is rejected.

## 3. Run the API

```bash
dotnet run --project src/PaymentGateway.Api     # https://localhost:7092, Scalar UI at /scalar/v1
```

Trust the development certificate once if your client complains: `dotnet dev-certs https --trust` (or use `curl -k`).

## 4. Make requests

```bash
TOKEN=$(pwsh scripts/dev-token.ps1)

curl -k -X POST https://localhost:7092/api/payments \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'

curl -k https://localhost:7092/api/payments/<id-from-the-response> -H "Authorization: Bearer $TOKEN"
```

Or open **https://localhost:7092/scalar/v1** and use "Test request" on each endpoint. **The Scalar page is already signed in:** in a Debug build, in Development, it is pre-loaded with a development token (the same one `scripts/dev-token.ps1` would make, signed with your user-secrets key, valid for 8 hours), so **Authorize** is filled in. This is for ease of testing only: the code is compiled out of a Release build (`#if DEBUG`), the documentation is only served in Development, and a deployed service never issues a token. It needs the signing key to exist, so run `pwsh scripts/dev-token.ps1` once; if **Authorize** is empty, paste a token by hand. The `POST` body is **pre-filled with a valid payment** (a card ending in an odd digit, an expiry a few years ahead), so you can press Send straight away; change the card's last digit to see Declined (even) or a bank error (`0`).

**Which card number gives which result** (the simulator looks at the last digit):

| Last digit | Bank says | You get |
|---|---|---|
| odd (1, 3, 5, 7, 9) | authorized | `200`, `"status": "Authorized"` |
| even (2, 4, 6, 8) | declined | `200`, `"status": "Declined"` |
| `0` | `503` | `502` problem details, nothing stored |

**What to expect for bad input** (the bank is never called, nothing is stored):

| Request | Result |
|---|---|
| `"currency":"JPY"` | `400` `{"status":"Rejected","errors":[{"field":"Currency","message":"Currency must be one of USD, GBP, EUR."}]}` |
| `"amount":0` or negative | `400` `Rejected` |
| card number not 14–19 digits, CVV not 3–4 digits, month not 1–12, expiry in the past | `400` `Rejected` |
| malformed JSON, or a missing field | `400` `Rejected` |
| body over 4 KB | `413` |
| `GET` of an unknown id, or another merchant's payment | `404` |

The response never contains the full card number or the CVV: only `cardNumberLastFour`.

## 5. Run the automated tests

```bash
dotnet test                                          # all tests; no Docker, no token needed (the bank is substituted)
dotnet test --filter-namespace "PaymentGateway.Api.Tests.Unit.*"   # unit tests only
dotnet test --filter-trait "Category=Simulator"      # against the real simulator (needs step 1; skipped otherwise)
pwsh scripts/smoke-test.ps1                          # starts the real app, checks 401/502/413/400/404, always stops it
```

The integration tests sign their own tokens, so they never use your user secrets. The tests worth reading first are
`Integration/Payments/` and `Integration/Contract/ApiContractTests.cs`.

## If something goes wrong

| Symptom | Cause and fix |
|---|---|
| `401` with a token | Run `pwsh scripts/dev-token.ps1` once so the key exists, then run the app again; check the app is in the Development environment; a token expires after a while, so make a new one |
| `403` | The token lacks the scope (`payments:create` to `POST`, `payments:read` to `GET`) |
| `404` on a payment you just made | You used a token for a different merchant (`-Merchant`) |
| `502` on a valid payment | The simulator is not running (step 1), or the card number ends in `0` |
| Build error "file is being used by another process" | A leftover `dotnet` process holds the build output; stop the running API (stop the debugger) and build again |
| Browser certificate warning | `dotnet dev-certs https --trust` |
