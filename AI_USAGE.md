# AI Usage & Senior Engineering Audit — FirstBank NovaPay

# AI Usage Report — NovaWallet Ledger Service

This document fulfills the mandatory AI usage requirement specified in Section 2.2 and Section 2.3 of `INSTRUCTIONS.md`.
This report documents how AI was utilized as a pair-programming accelerator during the development of the NovaWallet service, in compliance with Section 2.2 and Section 2.3 of `INSTRUCTIONS.md`.

## 1. My Approach to AI on this Project

---

It details:
I come from an experienced Node.js and NestJS background. While I understand distributed systems, relational databases, double-entry accounting, and concurrency deeply, C# and .NET 8 syntax and idioms were relatively new territory for me.

## 1. My Approach & Philosophy

1. Which tools were used and for what purpose.
2. Concrete prompts given and outputs produced.
3. Specific instances where AI output was wrong, unsafe, or naive for a financial system, and how human engineering judgment caught and fixed them.
   Instead of writing everything from scratch in an unfamiliar runtime or letting AI blindly generate code, I used AI as an interactive pair programmer. My strategy was simple:
   I have deep backend engineering experience in Node.js and NestJS, where I regularly build distributed APIs, handle database concurrency, and enforce security. However, .NET 8, C#, and Entity Framework Core were newer syntax for me.
   I have extensive backend engineering experience in Node.js and NestJS, where I regularly build distributed APIs, handle database concurrency, and enforce data security. However, .NET 8, C#, and Entity Framework Core were newer syntax for me.

- **I owned the architecture, security invariants, and business decisions.**
- **I used AI to translate my NestJS mental model into idiomatic .NET 8** (e.g., mapping TypeORM patterns to EF Core, NestJS Interceptors to ASP.NET Middleware, and Jest tests to xUnit).
- **I actively interrogated and challenged AI suggestions** whenever they felt naive, bloated, or dangerous for financial software.
  Rather than letting an AI generate unverified code, I used it strictly as an interactive pair programmer under my direct architectural supervision:
- **I set the system architecture, business rules, and security constraints.**
- **I used the AI to translate my NestJS knowledge into idiomatic .NET 8** (e.g. mapping TypeORM concepts to EF Core, NestJS Interceptors to ASP.NET Core Middleware, and writing xUnit tests).
- **I critically reviewed every output**, pushing back whenever the AI proposed naive designs, over-engineered solutions, or unsafe financial patterns.
  Rather than letting an AI blindly generate code, I used it strictly as an interactive pair programmer under my direct architectural supervision:

---

- **I owned the architecture, business logic, and security constraints.**
- **I used AI to translate my NestJS mental model into idiomatic .NET 8** (mapping TypeORM patterns to EF Core, NestJS Interceptors to ASP.NET Core Middleware, and Jest tests to xUnit).
- **I critically reviewed every suggestion and pushed back** whenever the AI proposed naive designs, over-engineered abstractions, or patterns unsafe for financial software.

## 1. Tools Used

### Tools Used

- **Google Antigravity (Gemini 3.8 Flash)**: Interactive pair programmer for syntax mapping, boilerplate generation, EF Core fluent configs, and xUnit test scaffolding.
- **Human Architectural Oversight**: Every schema decision, concurrency mechanism, and validation check was directed, verified, and audited by me.

## 2. Concrete Prompts Given & Outcomes

---

## 2. Concrete Prompts I Gave & What Came Back

## 2. Key Prompts & Engineering Direction

### Prompt 1: Translating NestJS Architecture to .NET 8

### Prompt 1: Translating my NestJS mental model to .NET 8

> _"see what I'm build; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"_

> _"see what I'm building; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"_

- **Google Antigravity / Gemini 3.8 Flash (Medium)**: Used as an interactive pair-programming partner to scaffold .NET 8 idiomatic code, write EF Core fluent configurations, generate migration scripts, construct RFC 7807 problem details middleware, and scaffold xUnit concurrency load tests.
- **Human-in-the-Loop Architectural Direction**: Every design decision, data model invariant, security consideration, and concurrency strategy was directed, questioned, and verified by the developer, using senior architectural experience from Node.js/NestJS and financial domain requirements.

* **Outcome:** The AI mapped out the full migration roadmap using familiar NestJS comparisons:

- **What came back:** The AI mapped the architecture directly to my existing knowledge base:
  - `AppDbContext` $\leftrightarrow$ TypeORM `DataSource` / Unit of Work
  - Scoped DI services $\leftrightarrow$ NestJS `@Injectable()` providers
  - C# Records with DataAnnotations $\leftrightarrow$ `class-validator` DTOs
  - Custom Middleware $\leftrightarrow$ NestJS Global Exception Filters & Interceptors
  - Custom ASP.NET Middleware $\leftrightarrow$ NestJS Global Exception Filters & Interceptors
- **My takeaway:** This allowed me to move fast without getting lost in .NET conventions while preserving clean architectural boundaries.

### Prompt 1: Translating my NestJS mental model to .NET

> _"see what I'm building; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"_

---

- **What came back:** The AI mapped the architecture directly to what I already know:
  - `AppDbContext` as the equivalent of a TypeORM `DataSource` / Unit of Work
  - Scoped DI services as the equivalent of NestJS `@Injectable()` providers
  - Record DTOs with DataAnnotations as `class-validator` DTOs
  - ASP.NET middleware as global NestJS Exception Filters and Interceptors
- **My takeaway:** This allowed me to move fast without getting bogged down in .NET syntax while maintaining the Clean Architecture structure.

## 2. Concrete Prompts & Outputs

### Prompt 2: Challenging the AI on Concurrency Strategy

### Prompt 2: Challenging the "Pessimistic Locking is the Gold Standard" Claim

> _"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."_

> _"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."_

- **What came back:** The AI originally threw around "pessimistic locking is the gold standard" without context. I challenged this: high-scale e-commerce often uses optimistic locking with retries, while modern ledger engines (like TigerBeetle or Stripe) use append-only event sourcing to avoid mutable row locking altogether.
- **My takeaway:** For NovaWallet's specific requirement—ensuring balances never go below zero during burst concurrent transfers against a single wallet—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` with sorted wallet IDs) was the cleanest and most reliable pattern to prevent deadlocks and avoid retry storms under load.

- **Outcome:** The AI initially made a blanket claim that pessimistic locking is the universal gold standard. I challenged this: high-scale e-commerce often uses optimistic locking with retries, and modern ledger engines (like Stripe or TigerBeetle) use append-only event sourcing to avoid mutable row locking altogether.
- **Resolution:** For NovaWallet's specific requirement—ensuring balances never drop below zero under concurrent burst transfers against a single wallet—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` with ordered IDs) was the cleanest and most reliable pattern to prevent deadlocks and avoid retry storms under load.

### Prompt 3: Questioning the Exposure of Customer/Account Identifiers in Errors

### Prompt 3: Questioning the Exposure of Identifiers in Error Messages

> _"i think it is wrong to expose customer or what do you think?"_

- **What came back:** The AI originally threw around "pessimistic locking is the gold standard." I pushed back with the nuanced reality: in massive e-commerce systems, optimistic locking or append-only event sourcing (like TigerBeetle or Stripe) is often preferred for high throughput.
- **My takeaway:** For this specific FirstBank assignment—where we have a strict requirement that concurrent transfers against a single wallet must never go negative under load—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` ordered by ID) was the right fit because it avoids the retry storms of optimistic concurrency.
- **What came back:** The AI originally included raw identifiers in error strings (e.g., `A wallet already exists for customer '{customerId}'`).
- **My takeaway:** In real fintech systems, companies do not use sensitive PII like BVN or phone numbers as database IDs because that is far too risky; they use database-generated IDs (UUIDs or synthetic keys). However, **internal database IDs still do not belong in error messages that an end user can potentially see.** Exposing raw database IDs in client toasts or alert banners is confusing, unpolished, and leaks internal system references to anyone inspecting network traffic. I directed the AI to sanitize all user-facing messages and retain identifiers strictly on internal exception properties for backend logging.

* **Outcome:** The AI originally included raw identifiers in error strings (e.g. `A wallet already exists for customer '{customerId}'`).
* **Resolution:** I flagged this immediately. I do not believe fintechs use sensitive personal information as primary IDs because that is far too risky; they use database-generated IDs (UUIDs or synthetic keys). However, **internal database IDs simply do not belong in client-facing error messages that an end user sees on a mobile or web screen.** Exposing them leaks internal system details and creates a terrible user experience. I had the AI sanitize all client messages to clean, friendly text while keeping internal IDs strictly in server-side logs.

* **Developer Prompt:**
  > _"see what I'm build; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"_
* **AI Output:**  
  Mapped .NET 8 concepts directly to the NestJS mental model:
  - Entities $\rightarrow$ TypeORM/Prisma entities
  - `AppDbContext` $\rightarrow$ TypeORM `DataSource` / `EntityManager`
  - `IWalletService` / `WalletService` $\rightarrow$ `@Injectable() WalletService`
  - `ExceptionHandlingMiddleware` $\rightarrow$ NestJS Global `@Catch() ExceptionFilter`
  - Record DTOs $\rightarrow$ `class-validator` DTOs
  - Program.cs DI $\rightarrow$ `app.module.ts` providers and middleware pipeline.

### Prompt 3: Flagging Data Privacy (NDPA 2023) & Account Enumeration

> _"i think it is wrong to expose customer or what do you think?"_

### Prompt 2: Challenging the "Pessimistic Locking is Gold Standard" Generalization

- **What came back:** The AI had generated error messages that echoed the `customerId` directly (e.g., `"No wallet found for customer '08012345678'"`).
- **My takeaway:** I flagged this because in Nigerian fintech, `customerId` is frequently a phone number, email, BVN, or NIN. Leaking that in public API errors allows attackers to enumerate who banks with FirstBank and violates NDPA 2023. I had the AI sanitize all client-facing messages.

* **Developer Prompt:**
  > _"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."_
* **AI Output & Resolution:**  
  Acknowledged the nuance:
  - **Optimistic Locking (`Version`)** is superior for high-volume, low-contention scenarios (100k distinct users making independent grocery purchases).
  - **Pessimistic Locking (`SELECT ... FOR UPDATE`)** is essential for high-contention accounts (e.g. 20 concurrent transfers hitting a single account in stress tests) where optimistic locking causes massive retry storms.
  - **Append-Only Event Sourcing (Stripe / TigerBeetle)** avoids both by inserting immutable ledger entries without updating a mutable balance column.

### Prompt 3: Identifying Data Protection & Account Enumeration Vulnerabilities

- **Developer Prompt:**
  > _"i think it is wrong to expose customer or what do you think?"_
- **AI Output & Resolution:**  
  Recognized the severe security and regulatory flaw under the **Nigeria Data Protection Act (NDPA 2023)** and OWASP API security guidelines. Removed all customer ID echoes from client-facing error messages while preserving identifiers in structured internal logs.

---

## 3. Cases Where AI Output Was Wrong, Unsafe, or Naive (and the Fixes)

## 3. Where I Caught the AI Being Wrong, Naive, or Dangerous for Finance

### Case 1: The Conflicting Concurrency Model Trap (Optimistic `Version` vs. Pessimistic Locks)

Here are four specific cases where AI output was flawed, and how I caught and corrected it:

- **The Naive/Unsafe AI Suggestion:**  
  The initial entity schema included `public int Version { get; set; }` on `Wallet` configured with `builder.Property(w => w.Version).IsConcurrencyToken()`.
- **Why it is dangerous in a financial system:**  
  When high-concurrency burst traffic hits a single wallet (such as the assessment's required concurrency load test with 20 parallel requests), optimistic concurrency tokens cause EF Core to append `WHERE Version = @version` on update. As a result, 1 request succeeds and the other 19 fail abruptly with `DbUpdateConcurrencyException`, requiring complex application retry loops and creating high latency.
- **How it was caught and fixed:**  
  The developer asked: _"so maybe we should not add version if we are going to use pessimistic locking then"_. We eliminated the `Version` concurrency token and adopted deterministic, ordered pessimistic row-locking (`SELECT ... FOR UPDATE` ordered by `Wallet.Id` GUID). This guarantees that concurrent requests line up cleanly at the database level and process sequentially without spurious errors or deadlocks.

### Case 1: The Concurrency Token Conflict (`Version` vs. Pessimistic Locking)

- **The AI's Mistake:** The AI initially placed a `public int Version { get; set; }` column on `Wallet` configured with EF Core's `.IsConcurrencyToken()`.
- **Why it's dangerous in finance:** In a stress test with 20 concurrent transfers hitting the same wallet simultaneously, optimistic concurrency tokens append `WHERE Version = @version` on update. Exactly 1 request succeeds and the other 19 immediately crash with `DbUpdateConcurrencyException`. In a banking app, you do not want 19 valid user transfers failing abruptly just because they arrived in the same millisecond window.
- **How I caught and fixed it:** I directed: _"so maybe we should not add version if we are going to use pessimistic locking then"_. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with sorted IDs (`CompareTo`). The database queues concurrent requests cleanly, processing all valid ones sequentially until the balance is depleted.

### 1. The Concurrency Token Conflict (`Version`)

- **The AI's Mistake:** The initial entity model included a `public int Version { get; set; }` column configured with EF Core's `.IsConcurrencyToken()`.
- **Why it's dangerous in finance:** In high-concurrency burst transfers (like the assessment's stress test firing 20 simultaneous transfers against one wallet), optimistic concurrency tokens cause EF Core to append `WHERE Version = @version` on update. 1 request succeeds and 19 immediately crash with `DbUpdateConcurrencyException`. In a banking app, you do not want 19 valid user transfers failing abruptly just because they arrived within the same millisecond.
- **How I fixed it:** I directed: _"so maybe we should not add version if we are going to use pessimistic locking then"_. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with deterministic ID ordering. The database queues concurrent requests cleanly, processing all valid ones sequentially until the funds are exhausted.

* **The AI's mistake:** The initial schema had a `public int Version { get; set; }` column configured with EF Core's `.IsConcurrencyToken()`.
* **Why it's dangerous:** If 20 transfer requests hit Alice's wallet at the same millisecond (the exact scenario tested in the assessment's concurrency load test), optimistic concurrency causes EF Core to append `WHERE Version = @version`. 1 request succeeds and 19 crash with `DbUpdateConcurrencyException`. In a real banking app, you don't want 19 customers getting random errors just because they had active transactions at the same time.
* **How I fixed it:** I told the AI: _"so maybe we should not add version if we are going to use pessimistic locking then"_. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with sorted IDs. The DB queues concurrent transfers sequentially, processing all valid ones and cleanly rejecting the rest only when the balance hits zero.

---

### 2. Leaking PII & Enabling Account Enumeration

- **The AI's mistake:** The AI wrote exceptions like:
  `throw new DuplicateWalletException($"A NGN wallet already exists for customer '{customerId}'.");`
- **Why it's dangerous:** In an open API, echoing identifiers is an OWASP API security vulnerability (Account Enumeration) and violates the Nigeria Data Protection Act (NDPA 2023). Anyone could write a script testing thousands of phone numbers to see which ones are registered FirstBank customers.
- **How I fixed it:** I caught this during review and instructed the AI to strip `customerId` from all user-facing messages. The exceptions now return generic, safe messages like _"A NGN wallet already exists for this account."_ The `customerId` is only retained as an internal property for structured backend logs.

### Case 2: PII Leakage & Account Enumeration under NDPA 2023

### 3. Over-Engineering Scope (Holds/Liens & Fee Engines)

- **The AI's mistake:** The AI suggested building a full `Hold` entity with expiry timers, and writing business logic to route transfer fees into a dedicated bank revenue wallet.
- **Why it's a risk:** The instructions explicitly stated that this is a _simplified_ ledger for P2P transfers and deposits over NIP rails. Introducing complex hold state machines and unrequested fee-routing when transfers settle instantly would have introduced unnecessary complexity and potential reconciliation bugs.
- **How I fixed it:** I asked: _"is Holds / Liens Entity required for any functional requirement or constraints?"_ We confirmed it was completely out of scope. I kept the schema clean, kept `AvailableBalance` and `BookBalance` in sync for immediate settlement, and defaulted fees to 0.

* **The Naive/Unsafe AI Suggestion:**  
  Initial domain exception messages echoed the customer ID directly in the message text:

### 2. Exposing Internal Identifiers in Client-Facing Messages

- **The AI's Mistake:** The AI wrote exception messages that reflected internal identifiers:

### Case 2: Exposing Internal Identifiers in Client-Facing Messages

- **The AI's Mistake:** The AI generated exceptions that echoed internal IDs directly in client-facing messages:
  ```csharp
  // ❌ Unsafe: Leaks customer identifiers to unauthenticated/untrusted callers
  // ❌ Bad UX and data leakage:
  // ❌ Leaks internal IDs into UI error banners:
  throw new DuplicateWalletException(customerId, currency);
  // Returned: "A NGN wallet already exists for customer '08012345678'."
  // Returned: "A NGN wallet already exists for customer 'c0a80101-0000-0000-0000-000000000001'."
  // Message: "A NGN wallet already exists for customer 'c0a80101-0000-0000-0000-000000000001'."
  ```
- **Why it is dangerous in a financial system:**  
  If a customer identifier is a phone number, email, BVN, or NIN, an attacker can use this endpoint to probe and enumerate valid FirstBank customers. This directly violates **NDPA 2023** compliance expectations and OWASP API3:2023.
- **How it was caught and fixed:**  
  The developer flagged: _"i think it is wrong to expose customer or what do you think?"_ The messages were sanitized to generic, customer-friendly copy:
- **Why it's flawed:** End users should never see raw database GUIDs or internal account keys in an alert banner or toast. It looks unpolished, confuses customers, and unnecessarily reveals internal database references to anyone inspecting network traffic.
- **How I fixed it:** I instructed the AI to decouple internal telemetry from user-facing copy. The messages were updated to clean, user-friendly text:
- **Why it's flawed:** End users should never see raw database GUIDs or internal account keys in an alert banner or toast. It looks unpolished, confuses customers, and unnecessarily reveals internal database references.
- **How I caught and fixed it:** I instructed the AI to decouple internal telemetry from user-facing copy. The messages were updated to clean, user-friendly text:
  ```csharp
  // ✅ Safe: Sanitized, no identifier leakage
  public DuplicateWalletException(string customerId, string currency)
  // ✅ Clean, professional UX:
  public DuplicateWalletException(string customerId, string currency)
      : base($"A {currency.ToUpperInvariant()} wallet already exists for this account.")
  ```
  The `CustomerId` was retained as an internal class property strictly for server-side logging and audit trails.
  The `CustomerId` was kept solely as a C# class property for backend structured logs and audit trails.

### 4. Running Migrations on App Startup in Production

### Case 3: Scope Creep & Over-Engineering (Holds/Liens & Fee Engines)

- **The AI's Mistake:** The AI suggested building a full `Hold` entity with expiry timers, and writing business logic to route transfer fees into a dedicated bank revenue wallet.
- **Why it's a risk:** The assessment instructions explicitly stated that this is a _simplified_ ledger for P2P transfers and deposits over instant NIP rails. Introducing complex hold state machines and unrequested fee-routing logic when transfers settle immediately would have introduced unnecessary complexity and potential reconciliation bugs.
- **How I caught and fixed it:** I asked: _"is Holds / Liens Entity required for any functional requirement or constraints?"_ We confirmed it was completely out of scope. I kept the schema clean, kept `AvailableBalance` and `BookBalance` in sync for immediate settlement, and defaulted fees to 0.

### 3. Running Migrations on App Startup in Production

### Case 4: Running Migrations on App Startup in Production

- **The AI's Mistake:** The AI placed `db.Database.Migrate()` directly in `Program.cs` to run unconditionally on every boot.
- **Why it's dangerous in enterprise production:** In multi-pod Kubernetes clusters, multiple replicas booting simultaneously race to run `ALTER TABLE`, risking deadlocks and failed rollouts. Furthermore, it violates the Principle of Least Privilege because the API's database user would need administrative DDL permissions.
- **How I fixed it:** I questioned: _"why? is this allowed in production?"_ We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).
- **How I caught and fixed it:** I questioned: _"why? is this allowed in production?"_ We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).

* **The AI's mistake:** The AI placed `db.Database.Migrate()` directly in `Program.cs` to run unconditionally on every boot.
* **Why it's dangerous:** In real production (e.g. running 5 pods in Kubernetes behind a load balancer), having all 5 containers run `ALTER TABLE` simultaneously on startup causes migration deadlocks and race conditions. Furthermore, it violates the Principle of Least Privilege because the API's database user would need administrative DDL permissions.
* **How I fixed it:** I questioned: _"why? is this allowed in production?"_ We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).

---

### Case 3: Scope Creep & Over-Engineering (Holds/Liens and Fee Wallets)

## 4. Summary

## 4. Final Thoughts

- **The Naive/Unsafe AI Suggestion:**  
  Early drafts suggested building a separate `Hold` / `Lien` entity and building business logic to deduct fees to a system fee wallet.
- **Why it is a risk:**  
  Section 4 of `INSTRUCTIONS.md` states: _"Scope note: this is intentionally more than can be 'gold-plated' in the time given. We would rather see good judgment about what to prioritize than a rushed attempt at everything."_  
  Building complex fee-deduction rules and authorization hold state machines when all transfers in the brief are immediate NIP transfers introduces unnecessary points of failure and risks ledger balance mismatches.
- **How it was caught and fixed:**  
  The developer verified: _"is Holds / Liens Entity required for any functional requirement or constraints?"_  
  We confirmed it was not required. We kept `AvailableBalanceKobo` and `BookBalanceKobo` updated in lockstep for immediate settlement, defaulted fee fields to 0, and focused on rock-solid concurrency safety, idempotency, and daily limits.

---

### Case 4: Running Database Migrations on App Startup in Production

- **The Naive/Unsafe Pattern:**  
  Calling `db.Database.Migrate()` directly in `Program.cs` on every boot.
- **Why it is dangerous in a bank's production infrastructure:**  
  In multi-pod Kubernetes clusters, multiple replicas booting simultaneously race to run `ALTER TABLE`, risking deadlocks, failed rollouts, and requiring elevated DDL permissions for the runtime application identity.
- **How it was caught and fixed:**  
   The developer asked: _"why? is this allowed in production?"_  
   We guarded the startup migration with `if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))`, documented the production alternative (`dotnet ef migrations bundle` via CI/CD pre-deploy jobs), and only enabled it for the `docker-compose.yml` demo per Requirement 2.2.
  Using AI allowed me to build a high-performance, idiomatic .NET 8 service in a fraction of the time it would have taken me to learn the entire ecosystem from scratch. However, building financial software requires domain skepticism. If I had blindly trusted the AI's first drafts, the codebase would have had concurrency crashes under load, PII leaks in error responses, and unsafe startup migrations. Directing the AI with clear architectural constraints was the key to getting a production-grade result.
  Using AI allowed me to translate my existing architectural knowledge into .NET 8 rapidly. However, building financial software requires strict domain skepticism. If I had blindly accepted the AI's first drafts, the codebase would have suffered from concurrency crashes under load, confusing internal IDs exposed to users, over-engineered unrequested features, and unsafe production migrations. Active developer oversight made the difference in delivering a clean, robust service.
  Using AI allowed me to translate my existing architectural knowledge into .NET 8 rapidly. However, building financial software requires strict domain skepticism. If I had blindly accepted the AI's first drafts, the codebase would have suffered from concurrency crashes under load, confusing internal IDs exposed to users, over-engineered unrequested features, and unsafe production migrations. Active developer oversight made the difference in delivering a clean, robust service.
