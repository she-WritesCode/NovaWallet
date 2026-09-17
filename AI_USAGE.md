# AI Usage Report — NovaWallet Ledger Service

## 1. My Approach to AI on this Project

I come from an experienced Node.js and NestJS background. While I understand distributed systems, relational databases, double-entry accounting, and concurrency deeply, C# and .NET 8 syntax and idioms were relatively new territory for me.

Instead of writing everything from scratch in an unfamiliar runtime or letting AI blindly generate code, I used AI as an interactive pair programmer. My strategy was simple:
- **I owned the architecture, security invariants, and business decisions.**
- **I used AI to translate my NestJS mental model into idiomatic .NET 8** (e.g., mapping TypeORM patterns to EF Core, NestJS Interceptors to ASP.NET Middleware, and Jest tests to xUnit).
- **I actively interrogated and challenged AI suggestions** whenever they felt naive, bloated, or dangerous for financial software.

---

## 2. Concrete Prompts I Gave & What Came Back

### Prompt 1: Translating my NestJS mental model to .NET
> *"see what I'm building; guide me to build it step by step, I'm a beginner in .NET but I am a pro in javascript node nest js so you will use those things to explain concepts to me. I have created entities and db context, what is next?"*

* **What came back:** The AI mapped the architecture directly to what I already know:
  - `AppDbContext` as the equivalent of a TypeORM `DataSource` / Unit of Work
  - Scoped DI services as the equivalent of NestJS `@Injectable()` providers
  - Record DTOs with DataAnnotations as `class-validator` DTOs
  - ASP.NET middleware as global NestJS Exception Filters and Interceptors
* **My takeaway:** This allowed me to move fast without getting bogged down in .NET syntax while maintaining the Clean Architecture structure.

### Prompt 2: Challenging the AI on Concurrency Strategy
> *"pessimistic locking is the gold standard for money true or false? False. While pessimistic locking is highly secure for financial transactions, it is not considered the absolute 'gold standard' for all monetary systems... choice depends on traffic patterns and architecture..."*

* **What came back:** The AI originally threw around "pessimistic locking is the gold standard." I pushed back with the nuanced reality: in massive e-commerce systems, optimistic locking or append-only event sourcing (like TigerBeetle or Stripe) is often preferred for high throughput.
* **My takeaway:** For this specific FirstBank assignment—where we have a strict requirement that concurrent transfers against a single wallet must never go negative under load—we agreed that deterministic pessimistic row-locking (`SELECT ... FOR UPDATE` ordered by ID) was the right fit because it avoids the retry storms of optimistic concurrency.

### Prompt 3: Flagging Data Privacy (NDPA 2023) & Account Enumeration
> *"i think it is wrong to expose customer or what do you think?"*

* **What came back:** The AI had generated error messages that echoed the `customerId` directly (e.g., `"No wallet found for customer '08012345678'"`).
* **My takeaway:** I flagged this because in Nigerian fintech, `customerId` is frequently a phone number, email, BVN, or NIN. Leaking that in public API errors allows attackers to enumerate who banks with FirstBank and violates NDPA 2023. I had the AI sanitize all client-facing messages.

---

## 3. Where I Caught the AI Being Wrong, Naive, or Dangerous for Finance

Here are four specific cases where AI output was flawed, and how I caught and corrected it:

### 1. The Concurrency Token Conflict (`Version`)
* **The AI's mistake:** The initial schema had a `public int Version { get; set; }` column configured with EF Core's `.IsConcurrencyToken()`.
* **Why it's dangerous:** If 20 transfer requests hit Alice's wallet at the same millisecond (the exact scenario tested in the assessment's concurrency load test), optimistic concurrency causes EF Core to append `WHERE Version = @version`. 1 request succeeds and 19 crash with `DbUpdateConcurrencyException`. In a real banking app, you don't want 19 customers getting random errors just because they had active transactions at the same time.
* **How I fixed it:** I told the AI: *"so maybe we should not add version if we are going to use pessimistic locking then"*. I stripped out the `Version` token and used PostgreSQL's row-level locking (`SELECT ... FOR UPDATE`) with sorted IDs. The DB queues concurrent transfers sequentially, processing all valid ones and cleanly rejecting the rest only when the balance hits zero.

### 2. Leaking PII & Enabling Account Enumeration
* **The AI's mistake:** The AI wrote exceptions like:
  `throw new DuplicateWalletException($"A NGN wallet already exists for customer '{customerId}'.");`
* **Why it's dangerous:** In an open API, echoing identifiers is an OWASP API security vulnerability (Account Enumeration) and violates the Nigeria Data Protection Act (NDPA 2023). Anyone could write a script testing thousands of phone numbers to see which ones are registered FirstBank customers.
* **How I fixed it:** I caught this during review and instructed the AI to strip `customerId` from all user-facing messages. The exceptions now return generic, safe messages like *"A NGN wallet already exists for this account."* The `customerId` is only retained as an internal property for structured backend logs.

### 3. Over-Engineering Scope (Holds/Liens & Fee Engines)
* **The AI's mistake:** The AI suggested building a full `Hold` entity with expiry timers, and writing business logic to route transfer fees into a dedicated bank revenue wallet.
* **Why it's a risk:** The instructions explicitly stated that this is a *simplified* ledger for P2P transfers and deposits over NIP rails. Introducing complex hold state machines and unrequested fee-routing when transfers settle instantly would have introduced unnecessary complexity and potential reconciliation bugs.
* **How I fixed it:** I asked: *"is Holds / Liens Entity required for any functional requirement or constraints?"* We confirmed it was completely out of scope. I kept the schema clean, kept `AvailableBalance` and `BookBalance` in sync for immediate settlement, and defaulted fees to 0.

### 4. Running Migrations on App Startup in Production
* **The AI's mistake:** The AI placed `db.Database.Migrate()` directly in `Program.cs` to run unconditionally on every boot.
* **Why it's dangerous:** In real production (e.g. running 5 pods in Kubernetes behind a load balancer), having all 5 containers run `ALTER TABLE` simultaneously on startup causes migration deadlocks and race conditions. Furthermore, it violates the Principle of Least Privilege because the API's database user would need administrative DDL permissions.
* **How I fixed it:** I questioned: *"why? is this allowed in production?"* We wrapped the call so it only runs during local development or when `ApplyMigrationsOnStartup=true` is explicitly passed in `docker-compose.yml` for demo convenience, and documented that production uses CI/CD migration bundles (`dotnet ef migrations bundle`).

---

## 4. Final Thoughts

Using AI allowed me to build a high-performance, idiomatic .NET 8 service in a fraction of the time it would have taken me to learn the entire ecosystem from scratch. However, building financial software requires domain skepticism. If I had blindly trusted the AI's first drafts, the codebase would have had concurrency crashes under load, PII leaks in error responses, and unsafe startup migrations. Directing the AI with clear architectural constraints was the key to getting a production-grade result.
