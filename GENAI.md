# GenAI exercise

This document answers the generative-AI part of the assessment: the prompt I used, a representative
sample of what came back, how I validated it, what I corrected, and how authentication, validation
and edge cases were handled.

The deliverable is a task-management API: CRUD over tasks (`title`, `description`, `status`,
`due_date`) associated with a user.

---

## 1. Tool and workflow

I used an agentic coding assistant (OpenCode) with a .NET-aware model, in a plan → generate → review
→ test loop. I never pasted generated code into the solution without compiling it and writing a test
that fails when the code is wrong. The prompt below is the one I actually used, trimmed of
environment details.

Two rules shaped everything:

1. The assessment forbids Entity Framework, Dapper and MediatR, so the prompt states the constraint
   and the acceptable alternative (raw ADO.NET) explicitly.
2. The output is a draft written by something that has never seen my database, my clock or my
   authorization rules. Treat it as a junior developer's first pass, not as an answer.

---

## 2. The prompt

```text
You are scaffolding a .NET 10 REST API for a small task-management system. Follow Clean
Architecture and produce production-shaped code, not a demo.

Hard constraints:
- No Entity Framework, no Dapper, no MediatR, no AutoMapper. Persistence is raw ADO.NET with
  Microsoft.Data.Sqlite and parameterized commands. No other data-access library.
- Four projects: Domain, Application, Infrastructure, Api. Dependencies point inwards only:
  Api -> Infrastructure -> Application -> Domain. Application must have zero NuGet packages.
- Nullable enabled, warnings as errors, file-scoped namespaces, async everywhere with
  CancellationToken, no sync-over-async, no .Result/.Wait().

Domain:
- User: id (Guid), email (normalized lowercase, unique), displayName, passwordHash, passwordSalt,
  passwordIterations, createdAt.
- TaskItem: id, userId, title, description (nullable), status (Todo | InProgress | Done),
  dueDate (DateOnly, nullable), createdAt, updatedAt.
- Invariants live in the entities: title 3-120 chars trimmed, description <= 1000,
  due date never in the past, status transitions only Todo->InProgress, Todo->Done,
  InProgress->Done, and never the same status twice.
- Expected failures return Result/Error values, not exceptions. Exceptions mean bugs.

Application:
- AuthService: RegisterAsync, LoginAsync, GetProfileAsync.
- TaskService: ListAsync, GetAsync, CreateAsync, UpdateAsync, ChangeStatusAsync, DeleteAsync.
- All task operations take the caller's userId and must scope by it. A task owned by someone else
  is reported as NotFound, never Forbidden.
- Field-level validation errors keyed by the JSON field name, so a UI can bind them to inputs.
- Ports as interfaces: IUserRepository, ITaskRepository, IPasswordHasher, ITokenIssuer, IClock.

Infrastructure:
- SQLite schema, numbered embedded SQL migration scripts, idempotent migrator with a
  schema_version table, idempotent seeder with a demo user and six tasks.
- PBKDF2-HMAC-SHA256, 210k iterations, 16-byte salt, constant-time comparison.
- JWT HS256 with sub/email/name claims, 8-hour expiry, key from configuration (minimum 32 chars).

Api:
- Controllers under /api, [Authorize] on tasks, bearer validation with issuer/audience checks.
- Correct status codes: 201 + Location on create, 204 on delete, 400 with a ProblemDetails
  `errors` object on validation, 401 on bad credentials, 404 for missing/foreign rows,
  409 on duplicate email.
- One exception handler mapping domain exceptions to 400 and unknown exceptions to a 500
  ProblemDetails with no stack trace. Swagger with a bearer security definition.
- No secrets in appsettings.json; development-only values go in appsettings.Development.json.

Tests:
- xUnit + Shouldly + NSubstitute. Unit tests for domain and application, repository tests against a
  throwaway SQLite file, endpoint tests through WebApplicationFactory.
- Name tests Method_Scenario_ExpectedBehaviour. Do not assert on log strings or private members.
- Tests must not touch the developer's app.db.

Output: file-by-file code, no commentary, and a short list of assumptions you made.
```

---

## 3. Representative sample of the output

The first pass produced a plausible skeleton, and a handful of things I would not ship. Trimmed
excerpt of the generated controller and repository:

```csharp
// As generated.
[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request)
{
    var existing = await _users.FindByEmail(request.Email);
    if (existing != null)
        return BadRequest("Email already exists");

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = request.Email,
        DisplayName = request.DisplayName,
        PasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)))
    };

    await _users.Add(user);
    return Ok(user);
}

// As generated.
public async Task<User?> FindByEmail(string email)
{
    using var connection = new SqliteConnection(_connectionString);
    var command = connection.CreateCommand();
    command.CommandText = $"SELECT * FROM users WHERE email = '{email}'";
    var reader = await command.ExecuteReaderAsync();

    if (await reader.ReadAsync())
        return new User { Id = Guid.Parse(reader["id"].ToString()) };

    return null;
}
```

It compiles, it returns a token, and every single line of it is wrong in a way a reviewer would
catch: plain SHA-256 for passwords, SQL injection through interpolation, an unvalidated email, a
`200` where the contract says `201`, an entity constructed with public setters bypassing its
invariants, and a reader that ignores every other column.

---

## 4. How I validated it

Review order, every time:

1. **Constraint check.** Does it reach for a forbidden dependency? The first draft did, twice: an
   `AppDbContext` and a `IMediator` handler pipeline. Both discarded before running anything.
2. **Compile.** `dotnet build` inside the SDK container with warnings as errors. Generated code
   often assumes nullable annotations it does not have.
3. **Contract check against the API table in `README.md`.** Every route, verb, status code and JSON
   key compared line by line. This caught `Ok(user)` on register, `Email` instead of `email` in the payload, and
   `DateTime` where the contract says a date-only value.
4. **Adversarial read of the data layer.** For each query I asked: what happens with a quote, a null,
   a wrong user, a missing row, a duplicate? That is where the interpolation and the missing
   `user_id` predicate surfaced.
5. **Tests first, then trust.** Behaviour I cared about got a failing test before the implementation
   was accepted. The tests also act as the review record: `ChangeStatus_FromDone_ReturnsValidationError`
   is the refusal to accept the generated "any transition is fine" shortcut.
6. **Run it.** The seeder, the health probe and the container start are part of validation; a green
   unit test suite does not prove the app boots with a mounted volume.

---

## 5. What I corrected

| Generated | Problem | What the repository does |
|---|---|---|
| `AppDbContext`, `DbSet<TaskItem>` | Forbidden ORM, hides the SQL | Hand-written repositories over `Microsoft.Data.Sqlite`, one class per aggregate |
| MediatR handlers | Forbidden pipeline, and unnecessary for six operations | Controllers call Application services directly |
| `SHA256(password)` | Fast hash, no salt, rainbow-table friendly | PBKDF2-HMAC-SHA256, 210k iterations, 16-byte salt, `FixedTimeEquals` |
| `$"... WHERE email = '{email}'"` | SQL injection | Parameterized commands everywhere; no string concatenation in any SQL |
| Public setters on entities | Invariants bypassed | Private setters, `Create`/`Rehydrate` factories, state changes through methods |
| `Ok(user)` on register | Leaks the hash, wrong status | `201 Created` with `AuthResultDto` (token + safe user projection) |
| `BadRequest("Email exists")` | Same message for every failure, no field mapping | `409` ProblemDetails with `code`, and `400` with `errors` keyed by field |
| `FindById(id)` with no user check | Any authenticated user can read another's task | Ownership in the service; `UPDATE`/`DELETE` repeat `WHERE user_id = @userId` |
| `Forbidden` for foreign tasks | Confirms that the row exists | `404` for both missing and foreign rows |
| `DateTime` for due dates | Time-of-day noise and timezone bugs | `DateOnly` end to end, `yyyy-MM-dd` in SQLite and JSON |
| Status manipulated by the client | Any status to any status | `ChangeStatus` enforces the transition table in the domain |
| Seed ran on every start | Duplicate demo rows after restarts | Seeder checks for the demo account first |
| JWT key in `appsettings.json` | Secret in source control | `Jwt:Key` from configuration/`Jwt__Key`, startup fails if shorter than 32 chars |
| Tests asserting on private fields and log text | Brittle, no behaviour covered | Tests assert returned `Result`, HTTP status, body and persisted state |

I kept the generated skeleton where it was right: the file layout, the naming, the DI style, and most
of the DTO shapes. Rewriting everything would have been slower and no better.

---

## 6. Edge cases and how they are handled

| Case | Handling |
|---|---|
| Email with mixed case or surrounding spaces | Trimmed and lowercased in the domain; unique index on the normalized value |
| Two registrations for the same email | `EmailExistsAsync` gives a `409`; the unique index is the real guard if two requests race |
| Password reuse of a weak value | Minimum 8 characters with a letter and a digit, maximum 128 to bound hashing cost |
| Login for an unknown email | Same `401` message and timing-shaped path as a wrong password |
| Title of only spaces | Trimmed before the length check, so it fails as too short |
| Description left blank | Normalized to `null`, never an empty string in the database |
| Due date in the past | Rejected on create and on update; an unchanged past date is allowed so old tasks stay editable |
| Due date "today" | Accepted — the boundary is `>= clock.Today` |
| `?status=bogus` | `400` listing the allowed values instead of silently returning everything |
| Status moved backwards | Domain rejects it; the service turns the rule violation into a field error |
| Reading or deleting someone else's task | `404`, and the SQL predicate makes the scope defence-in-depth |
| Token expired or tampered | JWT validation rejects it; the frontend clears the session on the first `401` |
| Concurrent edits | Last write wins, by design; the row still carries `updated_at` so the behaviour is visible |
| Malformed JSON / missing body | Model binding returns a `400` before any service code runs |
| Database file missing on boot | Migrator creates the schema; the container volume is initialized once and reused |

---

## 7. Authentication and validation, specifically

**Authentication.** Registration hashes the password and returns a token, so the client is signed in
without a second round trip. Login is deliberately non-enumerating. The token is a signed JWT with
`sub`, `email` and `name`, validated for issuer, audience, lifetime and signature, with a 30-second
clock skew. There is no refresh token: an 8-hour token keeps the demo simple, and the trade-off is
written down in the README rather than hidden.

**Validation.** Three rings, each with a different job:

1. **Transport** — `[ApiController]` rejects malformed bodies and route/query type mismatches.
2. **Application** — validators produce `errors` keyed by JSON field name, so the React form can put
   each message under the right input. These are the messages a user reads.
3. **Domain** — invariants that must hold no matter who calls the aggregate (title bounds, no past
   due dates, legal status transitions). These throw, and the exception handler turns them into a
   `400` rather than a `500`.

Validation is not duplicated by accident: the Application ring exists to produce good messages, the
Domain ring exists to keep the object valid. The tests pin both.

---

## 8. What I would do differently

- Put the prompt in the repository from the first commit and keep the review notes next to it; the
  correction list is much easier to present when it is written while reviewing.
- Ask for tests in the *same* response as the code, per slice — regeneration after review produced
  better tests than the first pass did.
- Pin the API contract (routes, status codes, JSON keys) in the prompt as a table. Prose descriptions
  produced `Ok(...)` where the contract needed `201` more than once.
- Never let the model choose a security primitive. Ask for the algorithm, the iteration count and
  the salt size explicitly, or write that code by hand and generate only the scaffolding around it.
