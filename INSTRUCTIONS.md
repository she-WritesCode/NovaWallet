## 1. Scenario

FirstBank NovaPay is a fictional digital-first financial super-app built for the Nigerian and West African mass market. It brings together five modules under one login:

- NovaWallet: e-wallet, P2P transfers, bill payments, airtime and data top-ups
- NovaSave: goal-based micro-savings, "Safe Lock" fixed savings, and round-up savings
- NovaLend: instant micro-loans / BNPL priced from alternative credit scoring built from wallet transaction history
- NovaBiz: QR/POS-lite payment collection for small merchants
- Diaspora remittance corridor: inbound transfers from the UK, US, and Canada landing directly in NGN wallets

FirstBank NovaPay is meant to be a practical everyday-money product: spend, save, borrow, collect, and send home.

### Operating context

All work should assume a realistic Nigerian fintech operating environment:

- amounts are in Naira (₦) and stored in kobo to avoid floating-point drift
- tiered KYC uses BVN/NIN
- instant settlement runs over NIBSS NIP rails
- a USSD channel (\*894#) supports feature-phone and low-connectivity users
- compliance touchpoints include CBN consumer protection and licensing expectations and the Nigeria Data Protection Act (NDPA 2023)

Candidates are not expected to be regulatory experts, but strong candidates should recognize where these constraints matter.

## 2. The task: NovaWallet Ledger Service

Build a backend service in C# / .NET (8 or 9) that implements a simplified wallet ledger for the NovaWallet module. This is the component that must never lose, duplicate, or miscount a customer’s money. Treat it with the seriousness FirstBank would.

### 2.1 Functional requirements

| Capability    | Requirement                                                                                                                                                                                          |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Create wallet | Create a wallet for a customer ID; starting balance is zero.                                                                                                                                         |
| Get balance   | Return the current balance and currency (NGN), with amounts in kobo.                                                                                                                                 |
| Credit wallet | Deposit funds into a wallet, simulating an inbound NIP transfer.                                                                                                                                     |
| Transfer      | Move funds atomically from one wallet to another. Must be concurrency-safe; concurrent transfer requests against the same wallet must never allow the balance to go negative or permit double-spend. |
| Idempotency   | The transfer endpoint must accept an `Idempotency-Key` header. Replaying the same key must not double-process the transfer, and reusing a key with a different payload must be rejected.             |
| Statement     | Provide paginated transaction history for a wallet, newest first.                                                                                                                                    |
| Daily limit   | Enforce a server-side daily outbound transfer limit (for example, ₦500,000/day) per wallet, resetting at midnight WAT.                                                                               |
| Audit log     | Record every balance mutation in an append-only, immutable audit trail separate from the transaction table for later review.                                                                         |

### 2.2 Hard constraints (non-negotiable)

- All monetary amounts are stored and computed as integers in kobo; no float or double is allowed in the money path.
- Balance must never go negative under any interleaving of concurrent requests.
- Endpoints are protected by a JWT bearer check; a simplified or mock issuer is acceptable as long as the middleware and claims handling are correctly implemented.
- Errors return a consistent, structured format; RFC 7807 Problem Details is recommended.
- The service and datastore must start with a single command: `docker compose up`.

> **AI usage requirement:**
> You are encouraged to use AI tools such as Copilot, ChatGPT, Claude, or Cursor. This is expected and part of the assessment. Include an `AI_USAGE.md` file in the repo that documents: which tools you used and for what; 2–3 concrete prompts you gave and what came back; and at least one case where the AI output was wrong, unsafe, or naive for a financial system (for example, a concurrency bug, a rounding/precision issue, or a missing edge case), and how you caught and fixed it. The goal is to assess judgment in directing AI, not whether you avoided it.

### 2.3 Deliverables

- Link to a Git repository (GitHub/GitLab). If private, grant access to the emails provided in the interview invite.
- `README.md` with architecture, key decisions, trade-offs, and instructions for running and testing.
- `AI_USAGE.md` as described above.
- OpenAPI/Swagger spec reachable when the service is running.
- Automated tests, including at least one that exercises the concurrency edge case under load.

### 2.4 Stretch goals (optional, used to differentiate strong candidates)

- Rate-limiting middleware on the transfer endpoint
- Outbox pattern publishing a `TransferCompleted` event
- Structured logging with correlation/trace IDs across a request
- Health/readiness endpoints suitable for container orchestration

## 3. How you will be assessed

| Criterion                        | What we are looking for                                                                              |
| -------------------------------- | ---------------------------------------------------------------------------------------------------- |
| Correctness & concurrency safety | The hard constraints in section 2.2 actually hold under concurrent load, not just in the happy path. |
| Code quality & architecture      | Clear separation of concerns, sensible use of .NET/C# idioms, and maintainable structure.            |
| Test rigor                       | Tests that would catch a regression, including the concurrency case.                                 |
| Security awareness               | Sensible handling of auth, input validation, and secrets.                                            |
| AI fluency & judgment            | Evidence of using AI to move faster and evidence of catching where it was wrong.                     |
| Communication                    | Clarity of README and ability to explain and defend decisions live.                                  |

## 4. Logistics

- Time window: 48–72 hours from receipt of this document to submission.
- On interview day: bring the repo up on a share screen, walk the panel through your design, and be ready to modify code live if asked.
- Scope note: this is intentionally more than can be “gold-plated” in the time given. We would rather see good judgment about what to prioritize than a rushed attempt at everything.
- Questions about the brief may be sent to your recruiting contact; use your own judgment for anything left ambiguous and document the assumption in your `README.md`.
