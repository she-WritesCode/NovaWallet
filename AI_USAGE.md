# AI Usage & Senior Engineering Audit — FirstBank NovaPay

This document fulfills the mandatory AI usage requirement specified in Section 2.2 and Section 2.3 of `INSTRUCTIONS.md`.

It details:
1. Which tools were used and for what purpose.
2. Concrete prompts given and outputs produced.
3. Specific instances where AI output was wrong, unsafe, or naive for a financial system, and how human engineering judgment caught and fixed them.

---

## 1. Tools Used

* **Google Antigravity / Gemini 3.8 Flash (Medium)**: Used as an interactive pair-programming partner to scaffold .NET 8 idiomatic code, write EF Core fluent configurations, generate migration scripts, construct RFC 7807 problem details middleware, and scaffold xUnit concurrency load tests.
* **Human-in-the-Loop Architectural Direction**: Every design decision, data model invariant, security consideration, and concurrency strategy was directed, questioned, and verified by the developer, using senior architectural experience from Node.js/NestJS and financial domain requirements.

---

## 2. Concrete Prompts & Outputs

### Prompt 1: Concept Translation from Node/NestJS to .NET
* **Developer Prompt:**  
  > *"see what I'm build; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"*
* **AI Output:**  
  Mapped .NET 8 concepts directly to the NestJS mental model:
  - Entities $\rightarrow$ TypeORM/Prisma entities
  - `AppDbContext` $\rightarrow$ TypeORM `DataSource` / `EntityManager`
  - `IWalletService` / `WalletService` $\rightarrow$ `@Injectable() WalletService`
  - `ExceptionHandlingMiddleware` $\rightarrow$ NestJS Global `@Catch() ExceptionFilter`
  - Record DTOs $\rightarrow$ `class-validator` DTOs
  - Program.cs DI $\rightarrow$ `app.module.ts` providers and middleware pipeline.

### Prompt 2: Challenging the "Pessimistic Locking is Gold Standard" Generalization
* **Developer Prompt:**  
  > *"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."*
* **AI Output & Resolution:**  
  Acknowledged the nuance:
  - **Optimistic Locking (`Version`)** is superior for high-volume, low-contention scenarios (100k distinct users making independent grocery purchases).
  - **Pessimistic Locking (`SELECT ... FOR UPDATE`)** is essential for high-contention accounts (e.g. 20 concurrent transfers hitting a single account in stress tests) where optimistic locking causes massive retry storms.
  - **Append-Only Event Sourcing (Stripe / TigerBeetle)** avoids both by inserting immutable ledger entries without updating a mutable balance column.

### Prompt 3: Identifying Data Protection & Account Enumeration Vulnerabilities
* **Developer Prompt:**  
  > *"i think it is wrong to expose customer or what do you think?"*
* **AI Output & Resolution:**  
  Recognized the severe security and regulatory flaw under the **Nigeria Data Protection Act (NDPA 2023)** and OWASP API security guidelines. Removed all customer ID echoes from client-facing error messages while preserving identifiers in structured internal logs.

---

## 3. Cases Where AI Output Was Wrong, Unsafe, or Naive (and the Fixes)

### Case 1: The Conflicting Concurrency Model Trap (Optimistic `Version` vs. Pessimistic Locks)
* **The Naive/Unsafe AI Suggestion:**  
  The initial entity schema included `public int Version { get; set; }` on `Wallet` configured with `builder.Property(w => w.Version).IsConcurrencyToken()`.
* **Why it is dangerous in a financial system:**  
  When high-concurrency burst traffic hits a single wallet (such as the assessment's required concurrency load test with 20 parallel requests), optimistic concurrency tokens cause EF Core to append `WHERE Version = @version` on update. As a result, 1 request succeeds and the other 19 fail abruptly with `DbUpdateConcurrencyException`, requiring complex application retry loops and creating high latency.
* **How it was caught and fixed:**  
  The developer asked: *"so maybe we should not add version if we are going to use pessimistic locking then"*. We eliminated the `Version` concurrency token and adopted deterministic, ordered pessimistic row-locking (`SELECT ... FOR UPDATE` ordered by `Wallet.Id` GUID). This guarantees that concurrent requests line up cleanly at the database level and process sequentially without spurious errors or deadlocks.

---

### Case 2: PII Leakage & Account Enumeration under NDPA 2023
* **The Naive/Unsafe AI Suggestion:**  
  Initial domain exception messages echoed the customer ID directly in the message text:
  ```csharp
  // ❌ Unsafe: Leaks customer identifiers to unauthenticated/untrusted callers
  throw new DuplicateWalletException(customerId, currency);
  // Returned: "A NGN wallet already exists for customer '08012345678'."
  ```
* **Why it is dangerous in a financial system:**  
  If a customer identifier is a phone number, email, BVN, or NIN, an attacker can use this endpoint to probe and enumerate valid FirstBank customers. This directly violates **NDPA 2023** compliance expectations and OWASP API3:2023.
* **How it was caught and fixed:**  
  The developer flagged: *"i think it is wrong to expose customer or what do you think?"* The messages were sanitized to generic, customer-friendly copy:
  ```csharp
  // ✅ Safe: Sanitized, no identifier leakage
  public DuplicateWalletException(string customerId, string currency) 
      : base($"A {currency.ToUpperInvariant()} wallet already exists for this account.")
  ```
  The `CustomerId` was retained as an internal class property strictly for server-side logging and audit trails.

---

### Case 3: Scope Creep & Over-Engineering (Holds/Liens and Fee Wallets)
* **The Naive/Unsafe AI Suggestion:**  
  Early drafts suggested building a separate `Hold` / `Lien` entity and building business logic to deduct fees to a system fee wallet.
* **Why it is a risk:**  
  Section 4 of `INSTRUCTIONS.md` states: *"Scope note: this is intentionally more than can be 'gold-plated' in the time given. We would rather see good judgment about what to prioritize than a rushed attempt at everything."*  
  Building complex fee-deduction rules and authorization hold state machines when all transfers in the brief are immediate NIP transfers introduces unnecessary points of failure and risks ledger balance mismatches.
* **How it was caught and fixed:**  
  The developer verified: *"is Holds / Liens Entity required for any functional requirement or constraints?"*  
  We confirmed it was not required. We kept `AvailableBalanceKobo` and `BookBalanceKobo` updated in lockstep for immediate settlement, defaulted fee fields to 0, and focused on rock-solid concurrency safety, idempotency, and daily limits.

---

### Case 4: Running Database Migrations on App Startup in Production
* **The Naive/Unsafe Pattern:**  
  Calling `db.Database.Migrate()` directly in `Program.cs` on every boot.
* **Why it is dangerous in a bank's production infrastructure:**  
  In multi-pod Kubernetes clusters, multiple replicas booting simultaneously race to run `ALTER TABLE`, risking deadlocks, failed rollouts, and requiring elevated DDL permissions for the runtime application identity.
* **How it was caught and fixed:**  
  The developer asked: *"why? is this allowed in production?"*  
  We guarded the startup migration with `if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))`, documented the production alternative (`dotnet ef migrations bundle` via CI/CD pre-deploy jobs), and only enabled it for the `docker-compose.yml` demo per Requirement 2.2.
