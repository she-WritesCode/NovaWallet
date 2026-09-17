# AI Usage Report — NovaWallet Ledger Service

This document fulfills the mandatory AI usage requirement specified in Section 2.2 and Section 2.3 of `INSTRUCTIONS.md`.

---

## 1. My Approach & Philosophy

I have extensive backend engineering experience in Node.js and NestJS, where I regularly build distributed APIs, handle database concurrency, and enforce data security. However, .NET 8, C#, and Entity Framework Core were newer syntax for me.

Rather than letting an AI blindly generate code, I used it strictly as an interactive pair programmer under my direct architectural supervision:

- **I owned the architecture, business logic, and security constraints.**
- **I used AI to translate my NestJS mental model into idiomatic .NET 8** (mapping TypeORM patterns to EF Core, NestJS Interceptors to ASP.NET Core Middleware, and Jest tests to xUnit).
- **I critically reviewed every suggestion and pushed back** whenever the AI proposed naive designs, over-engineered abstractions, or patterns unsafe for financial software.

### Tools Used
- **Google Antigravity (Gemini 3.8 Flash)**: Interactive pair programmer for syntax mapping, boilerplate generation, EF Core fluent configs, and xUnit test scaffolding.
- **Human Architectural Oversight**: Every schema decision, concurrency mechanism, and validation check was directed, verified, and audited by me.

---

## 2. Key Prompts & Engineering Direction

### Prompt 1: Translating my NestJS mental model to .NET 8
> _"see what I'm build; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"_

- **What came back:** The AI mapped the architecture directly to my existing knowledge base:
  - `AppDbContext` $\leftrightarrow$ TypeORM `DataSource` / Unit of Work
  - Scoped DI services $\leftrightarrow$ NestJS `@Injectable()` providers
  - C# Records with DataAnnotations $\leftrightarrow$ `class-validator` DTOs
  - Custom ASP.NET Middleware $\leftrightarrow$ NestJS Global Exception Filters & Interceptors
- **My takeaway:** This allowed me to move fast without getting lost in .NET conventions while preserving clean architectural boundaries.

### Prompt 2: Challenging the "Pessimistic Locking is the Gold Standard" Claim
> _"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."_

- **What came back:** The AI originally threw around "pessimistic locking is the gold standard" without context. I challenged this: high-scale e-commerce often uses optimistic locking with retries, while modern ledger engines (like TigerBeetle or Stripe) use append-only event sourcing to avoid mutable row locking altogether.
- **My takeaway:** For NovaWallet's specific requirement—ensuring balances never go below zero during burst concurrent transfers against a single wallet—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` with sorted wallet IDs) was the cleanest and most reliable pattern to prevent deadlocks and avoid retry storms under load.

### Prompt 3: Questioning the Exposure of Identifiers in Error Messages
> _"i think it is wrong to expose customer or what do you think?"_

- **What came back:** The AI originally included raw identifiers in error strings (e.g., `A wallet already exists for customer '{customerId}'`).
- **My takeaway:** In real fintech systems, companies do not use sensitive PII like BVN or phone numbers as database IDs because that is far too risky; they use database-generated IDs (UUIDs or synthetic keys). However, **internal database IDs still do not belong in error messages that an end user can potentially see.** Exposing raw database IDs in client toasts or alert banners is confusing, unpolished, and leaks internal system references to anyone inspecting network traffic. I directed the AI to sanitize all user-facing messages and retain identifiers strictly on internal exception properties for backend logging.

---

## 3. Where I Caught the AI Being Wrong, Naive, or Dangerous for Finance

Here are three specific cases where AI output was flawed, and how I caught and corrected it:

### Case 1: The Concurrency Token Conflict (`Version` vs. Pessimistic Locking)
- **The AI's Mistake:** The AI initially placed a `public int Version { get; set; }` column on `Wallet` configured with EF Core's `.IsConcurrencyToken()`.
- **Why it's dangerous in finance:** In a stress test with 20 concurrent transfers hitting the same wallet simultaneously, optimistic concurrency tokens append `WHERE Version = @version` on update. Exactly 1 request succeeds and the other 19 immediately crash with `DbUpdateConcurrencyException`. In a banking app, you do not want 19 valid user transfers failing abruptly just because they arrived in the same millisecond window.
- **How I caught and fixed it:** I directed: _"so maybe we should not add version if we are going to use pessimistic locking then"_. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with sorted IDs (`CompareTo`). The database queues concurrent requests cleanly, processing all valid ones sequentially until the balance is depleted.

### Case 2: Exposing Internal Identifiers in Client-Facing Messages
- **The AI's Mistake:** The AI generated exceptions that echoed internal IDs directly in client-facing messages:
  ```csharp
  // ❌ Leaks internal IDs into UI error banners:
  throw new DuplicateWalletException(customerId, currency);
  // Message: "A NGN wallet already exists for customer 'c0a80101-0000-0000-0000-000000000001'."
  ```
- **Why it's flawed:** End users should never see raw database GUIDs or internal account keys in an alert banner or toast. It looks unpolished, confuses customers, and unnecessarily reveals internal database references.
- **How I caught and fixed it:** I instructed the AI to decouple internal telemetry from user-facing copy. The messages were updated to clean, user-friendly text:
  ```csharp
  // ✅ Clean, professional UX:
  public DuplicateWalletException(string customerId, string currency)
      : base($"A {currency.ToUpperInvariant()} wallet already exists for this account.")
  ```
  The `CustomerId` was kept solely as a C# class property for backend structured logs and audit trails.

### Case 3: Running Migrations on App Startup in Production
- **The AI's Mistake:** The AI placed `db.Database.Migrate()` directly in `Program.cs` to run unconditionally on every boot.
- **Why it's dangerous in enterprise production:** In multi-pod Kubernetes clusters, multiple replicas booting simultaneously race to run `ALTER TABLE`, risking deadlocks and failed rollouts. Furthermore, it violates the Principle of Least Privilege because the API's database user would need administrative DDL permissions.
- **How I caught and fixed it:** I questioned: _"why? is this allowed in production?"_ We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).

---

## 4. Final Thoughts

Using AI allowed me to translate my existing architectural knowledge into .NET 8 rapidly. However, building financial software requires strict domain skepticism. If I had blindly accepted the AI's first drafts, the codebase would have suffered from concurrency crashes under load, confusing internal IDs exposed to users, and unsafe production migrations. Active developer oversight made the difference in delivering a clean, robust service.
