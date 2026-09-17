# AI Usage Report — NovaWallet Ledger Service

This report documents how AI was utilized as a pair-programming accelerator during the development of the NovaWallet service, in compliance with Section 2.2 and Section 2.3 of `INSTRUCTIONS.md`.

---

## 1. My Approach & Philosophy

I have deep backend engineering experience in Node.js and NestJS, where I regularly build distributed APIs, handle database concurrency, and enforce security. However, .NET 8, C#, and Entity Framework Core were newer syntax for me.

Rather than letting an AI generate unverified code, I used it strictly as an interactive pair programmer under my direct architectural supervision:
- **I set the system architecture, business rules, and security constraints.**
- **I used the AI to translate my NestJS knowledge into idiomatic .NET 8** (e.g. mapping TypeORM concepts to EF Core, NestJS Interceptors to ASP.NET Core Middleware, and writing xUnit tests).
- **I critically reviewed every output**, pushing back whenever the AI proposed naive designs, over-engineered solutions, or unsafe financial patterns.

---

## 2. Concrete Prompts Given & Outcomes

### Prompt 1: Translating NestJS Architecture to .NET 8
> *"see what I'm building; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"*

* **Outcome:** The AI mapped out the full migration roadmap using familiar NestJS comparisons:
  - `AppDbContext` $\leftrightarrow$ TypeORM `DataSource` / Unit of Work
  - Scoped DI services $\leftrightarrow$ NestJS `@Injectable()` providers
  - C# Records with DataAnnotations $\leftrightarrow$ `class-validator` DTOs
  - Custom Middleware $\leftrightarrow$ NestJS Global Exception Filters & Interceptors

### Prompt 2: Challenging the AI on Concurrency Strategy
> *"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."*

* **Outcome:** The AI initially made a blanket claim that pessimistic locking is the universal gold standard. I challenged this: high-scale e-commerce often uses optimistic locking with retries, and modern ledger engines (like Stripe or TigerBeetle) use append-only event sourcing to avoid mutable row locking altogether.
* **Resolution:** For NovaWallet's specific requirement—ensuring balances never drop below zero under concurrent burst transfers against a single wallet—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` with ordered IDs) was the cleanest and most reliable pattern to prevent deadlocks and avoid retry storms under load.

### Prompt 3: Questioning the Exposure of Customer/Account Identifiers in Errors
> *"i think it is wrong to expose customer or what do you think?"*

* **Outcome:** The AI originally included raw identifiers in error strings (e.g. `A wallet already exists for customer '{customerId}'`).
* **Resolution:** I flagged this immediately. I do not believe fintechs use sensitive personal information as primary IDs because that is far too risky; they use database-generated IDs (UUIDs or synthetic keys). However, **internal database IDs simply do not belong in client-facing error messages that an end user sees on a mobile or web screen.** Exposing them leaks internal system details and creates a terrible user experience. I had the AI sanitize all client messages to clean, friendly text while keeping internal IDs strictly in server-side logs.

---

## 3. Where I Caught the AI Being Wrong, Naive, or Dangerous for Finance

Here are four specific cases where AI output was flawed, and how I caught and corrected it:

### 1. The Concurrency Token Conflict (`Version`)
* **The AI's Mistake:** The initial entity model included a `public int Version { get; set; }` column configured with EF Core's `.IsConcurrencyToken()`.
* **Why it's dangerous in finance:** In high-concurrency burst transfers (like the assessment's stress test firing 20 simultaneous transfers against one wallet), optimistic concurrency tokens cause EF Core to append `WHERE Version = @version` on update. 1 request succeeds and 19 immediately crash with `DbUpdateConcurrencyException`. In a banking app, you do not want 19 valid user transfers failing abruptly just because they arrived within the same millisecond.
* **How I fixed it:** I directed: *"so maybe we should not add version if we are going to use pessimistic locking then"*. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with deterministic ID ordering. The database queues concurrent requests cleanly, processing all valid ones sequentially until the funds are exhausted.

### 2. Exposing Internal Identifiers in Client-Facing Messages
* **The AI's Mistake:** The AI wrote exception messages that reflected internal identifiers:
  ```csharp
  // ❌ Bad UX and data leakage:
  throw new DuplicateWalletException(customerId, currency);
  // Returned: "A NGN wallet already exists for customer 'c0a80101-0000-0000-0000-000000000001'."
  ```
* **Why it's flawed:** End users should never see raw database GUIDs or internal account keys in an alert banner or toast. It looks unpolished, confuses customers, and unnecessarily reveals internal database references to anyone inspecting network traffic.
* **How I fixed it:** I instructed the AI to decouple internal telemetry from user-facing copy. The messages were updated to clean, user-friendly text:
  ```csharp
  // ✅ Clean, professional UX:
  public DuplicateWalletException(string customerId, string currency) 
      : base($"A {currency.ToUpperInvariant()} wallet already exists for this account.")
  ```
  The `CustomerId` was kept solely as a C# class property for backend structured logs and audit trails.

### 3. Scope Creep & Over-Engineering (Holds/Liens & Fee Engines)
* **The AI's Mistake:** The AI suggested adding a full `Hold` entity with expiry timers, and writing business logic to route transfer fees into a dedicated bank revenue wallet.
* **Why it's a risk:** The assessment instructions explicitly stated that this is a *simplified* ledger for P2P transfers and deposits over instant NIP rails. Introducing complex hold state machines and unrequested fee-routing logic when transfers settle immediately would have introduced unnecessary complexity and potential reconciliation bugs.
* **How I fixed it:** I asked: *"is Holds / Liens Entity required for any functional requirement or constraints?"* We confirmed it was completely out of scope. I kept the schema focused, kept `AvailableBalance` and `BookBalance` in sync for immediate settlement, and defaulted fees to 0.

### 4. Running Migrations on App Startup in Production
* **The AI's Mistake:** The AI placed `db.Database.Migrate()` directly in `Program.cs` to run unconditionally on every boot.
* **Why it's dangerous in enterprise production:** In multi-pod Kubernetes clusters, multiple replicas booting simultaneously race to run `ALTER TABLE`, risking deadlocks and failed rollouts. Furthermore, it violates the Principle of Least Privilege because the API's database user would need administrative DDL permissions.
* **How I fixed it:** I questioned: *"why? is this allowed in production?"* We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).

---

## 4. Summary

Using AI allowed me to translate my existing architectural knowledge into .NET 8 rapidly. However, building financial software requires strict domain skepticism. If I had blindly accepted the AI's first drafts, the codebase would have suffered from concurrency crashes under load, confusing internal IDs exposed to users, over-engineered unrequested features, and unsafe production migrations. Active developer oversight made the difference in delivering a clean, robust service.
