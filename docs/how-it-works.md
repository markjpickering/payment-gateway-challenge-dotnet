# How it works

A short tour of the software: what it does, how a request moves through it, and how the classes fit
together. The diagrams are [Mermaid](https://mermaid.js.org/), which GitHub renders. For the reasoning behind each
decision see [`architecture-decisions.md`](architecture-decisions.md). A standalone copy that opens in any browser (no GitHub needed) is `how-it-works.html` in this folder, generated from this file by `scripts/build-how-it-works-html.ps1`.

## In one paragraph

A merchant sends `POST /api/payments` with card details. The gateway checks the caller's token, validates the request
against the brief's rules, forwards a valid one to the acquiring bank (a simulator), stores the outcome and answers
`Authorized` or `Declined`. Anything invalid is answered `Rejected` (400) without calling the bank or storing anything. A
bank that cannot answer is an error to the merchant (502), not a decline. `GET /api/payments/{id}` returns a stored
payment to the merchant that owns it (any other merchant gets 404). The full card number and the CVV go to the bank and
nowhere else: what is stored, returned and logged holds only the last four digits.

## The layers

Dependencies point one way; architecture tests enforce it.

```mermaid
flowchart TB
    Program["Program.cs + PaymentGatewayDi.InitialiseDi<br/>(composition root: wires everything)"]
    Api["Api<br/>endpoints, security, error handling, OpenAPI"]
    ApiInterface["ApiInterface<br/>request and response DTOs"]
    BL["BusinessLogic<br/>PaymentService, validation, mappings"]
    DI["DataInterface<br/>IPaymentsRepository, IBankClient, IPaymentMetrics,<br/>entities, enums, bank DTOs"]
    DL["DataLayer<br/>PaymentsRepository, BankClient, NoOpPaymentMetrics"]
    Shared["Shared<br/>Result, Error"]

    Program -.wires.-> Api
    Program -.wires.-> BL
    Program -.wires.-> DL
    Api --> BL
    Api --> ApiInterface
    BL --> ApiInterface
    BL --> DI
    DL --> DI
    ApiInterface --> DI
    Api --> Shared
    BL --> Shared
    DL --> Shared
    DI --> Shared
```

## What happens on `POST /api/payments`

```mermaid
sequenceDiagram
    autonumber
    actor M as Merchant
    participant K as ASP.NET pipeline
    participant E as PaymentEndpoints
    participant S as PaymentService
    participant V as PostPaymentRequestValidator
    participant B as BankClient
    participant X as Bank simulator
    participant R as PaymentsRepository

    M->>K: POST /api/payments (Bearer token, JSON body)
    K->>K: check the token and the payments:create scope (else 401 / 403)
    K->>E: bound PostPaymentRequest (malformed JSON gives 400 Rejected)
    E->>S: ProcessAsync(request)
    S->>V: validate
    alt invalid
        V-->>S: field errors
        S-->>E: Result failure (ValidationFailed)
        E-->>M: 400 Rejected + the field errors
    else valid
        S->>B: AuthorizeAsync(bank request)
        B->>X: POST /payments (snake_case JSON)
        alt bank unreachable, 503 or timeout
            B-->>S: Result failure (BankError)
            S-->>E: failure
            E-->>M: 502 problem details, nothing stored
        else bank answers
            X-->>B: authorized true or false
            B-->>S: Result success
            S->>R: AddAsync(PaymentEntity: last four digits only)
            S-->>E: PostPaymentResponse
            E-->>M: 200 Authorized or Declined
        end
    end
```

`GET /api/payments/{id}` is shorter: the token needs `payments:read`, `PaymentService.GetAsync` reads the
`PaymentEntity` from the repository and returns it only if its `MerchantId` equals the caller's merchant, otherwise `null`,
which the endpoint turns into 404 (the same answer as an unknown id).

## Classes

### The flow

```mermaid
classDiagram
    direction LR

    class PaymentEndpoints {
        <<static>>
        +MapPaymentEndpoints()
    }
    class FailureResults {
        <<static>>
        +ToHttp(Error)
    }
    class IPaymentService {
        <<interface>>
        +ProcessAsync(PostPaymentRequest) Result
        +GetAsync(Guid) PostPaymentResponse
    }
    class PaymentService
    class PostPaymentRequestValidator
    class IMapping {
        <<interface>>
        +Map(source) destination
    }
    class IBankClient {
        <<interface>>
        +AuthorizeAsync(BankPaymentRequest) Result
    }
    class BankClient
    class IPaymentsRepository {
        <<interface>>
        +AddAsync(PaymentEntity)
        +GetAsync(Guid) PaymentEntity
    }
    class PaymentsRepository
    class ICallerIdentity {
        <<interface>>
        +InitiatedBy string
        +OnBehalfOf string
    }
    class CallerIdentity
    class IPaymentMetrics {
        <<interface>>
        +PaymentProcessed(PaymentStatus)
        +BankRequest(BankOutcome, TimeSpan)
    }
    class NoOpPaymentMetrics

    PaymentEndpoints --> IPaymentService : calls
    PaymentEndpoints ..> FailureResults : maps errors with
    PaymentService ..|> IPaymentService
    PaymentService --> PostPaymentRequestValidator : validates with
    PaymentService --> IMapping : 3 mappings
    PaymentService --> IBankClient
    PaymentService --> IPaymentsRepository
    PaymentService --> ICallerIdentity : whose payment
    BankClient ..|> IBankClient
    BankClient --> IPaymentMetrics : records each call
    PaymentsRepository ..|> IPaymentsRepository
    CallerIdentity ..|> ICallerIdentity
    NoOpPaymentMetrics ..|> IPaymentMetrics
```

The three mappings are hand-written classes: `PostPaymentRequestToBankPaymentRequestMapping`,
`ProcessedPaymentToPaymentEntityMapping` and `PaymentEntityToPostPaymentResponseMapping`.

### The data

Three kinds of data type, kept apart on purpose: the API's DTOs, the bank's DTOs, and the stored entity. A request is
turned into a stored entity and then into a response; the full card number never goes past the bank mapping.

```mermaid
classDiagram
    direction LR

    class PostPaymentRequest {
        <<DTO>>
        CardNumber
        ExpiryMonth
        ExpiryYear
        Currency
        Amount
        Cvv
    }
    class PostPaymentResponse {
        <<DTO>>
        Id
        Status
        CardNumberLastFour
        ExpiryMonth
        ExpiryYear
        Currency
        Amount
    }
    class RejectedPaymentResponse {
        <<DTO>>
        Status = Rejected
        Errors
    }
    class BankPaymentRequest {
        <<BankDTO>>
        card_number
        expiry_date
        currency
        amount
        cvv
    }
    class BankPaymentResponse {
        <<BankDTO>>
        authorized
        authorization_code
    }
    class ProcessedPaymentModel {
        <<model>>
        Id
        MerchantId
        Status
        Request
    }
    class PaymentEntity {
        <<entity>>
        Id
        MerchantId
        Status
        CardNumberLastFour
        ExpiryMonth
        ExpiryYear
        Currency
        Amount
    }
    class Result {
        IsSuccess
        Value
        Error
    }
    class Error {
        Kind
        Message
        FieldErrors
    }
    class PaymentStatus {
        <<enum>>
        Authorized
        Declined
        Rejected
    }

    PostPaymentRequest ..> BankPaymentRequest : mapped to
    BankPaymentResponse ..> ProcessedPaymentModel : decides Status
    ProcessedPaymentModel o-- PostPaymentRequest
    ProcessedPaymentModel ..> PaymentEntity : mapped to
    PaymentEntity ..> PostPaymentResponse : mapped to
    Result o-- Error
    RejectedPaymentResponse ..> PaymentStatus
    PostPaymentResponse ..> PaymentStatus
    PaymentEntity ..> PaymentStatus
```

### Security, errors and wiring

```mermaid
classDiagram
    direction LR

    class PaymentGatewayDi {
        <<static>>
        +InitialiseDi(IConfiguration)
    }
    class ScopeAuthorizationHandler
    class ScopeRequirement
    class PaymentScopes {
        <<static>>
        Create = payments:create
        Read = payments:read
    }
    class BadRequestExceptionHandler
    class UnhandledExceptionHandler
    class Program

    Program --> PaymentGatewayDi : calls once
    PaymentGatewayDi ..> ScopeAuthorizationHandler : registers
    ScopeAuthorizationHandler --> ScopeRequirement : checks the scope claim
    ScopeRequirement --> PaymentScopes
    PaymentGatewayDi ..> BadRequestExceptionHandler : registers
    PaymentGatewayDi ..> UnhandledExceptionHandler : registers
```

## How failures are handled

| Situation | What happens |
|---|---|
| No or invalid token | 401, before the endpoint runs |
| Token without the scope | 403 |
| Invalid, missing or malformed request | 400 `Rejected`; the bank is not called, nothing is stored |
| Body over 4 KB | 413 |
| Bank says no | 200 `Declined`; stored |
| Bank unreachable, 503, timeout or unreadable reply | 502 `ProblemDetails` with a `traceId`; nothing stored |
| Unknown id, or another merchant's payment | 404 |
| Anything unexpected | 500 `ProblemDetails` with a `traceId` and no internals |

Expected outcomes are `Result` values returned up the call chain, not exceptions; only the bank client catches
transport exceptions, and it turns each into a `BankError` result.

## What is not here

Idempotency keys, an audit trail, reversals and real metrics are designed in the ADRs but NOT IMPLEMENTED (see
[Beyond the brief](../README.md#beyond-the-brief)). Storage is in memory and is lost on restart.
