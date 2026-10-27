# How I approached the exercise

## Reading the requirements

My first pass was to list what is actually graded: architecture, tests, code quality, functionality,
and how I talk about AI output. Nothing there rewards a large feature set. So before writing code I
decided the scope: one aggregate (a task), users, and no extras.

I also decided what not to build. Comments, sharing, teams, attachments, pagination. Each one would
have added surface without touching the criteria, and every hour spent there was an hour not spent
on the parts being reviewed.

The forbidden list did most of the design work for me. No Entity Framework or Dapper means the data
layer is SQL I write myself. No MediatR means controllers talk to application services directly
instead of hiding the call behind a pipeline. Once those two were fixed, the project layout followed.

## The user story

I wrote the story before the code, because it gives the acceptance criteria something to attach to:

> As a freelancer juggling several clients, I want to keep my tasks in one place and track them from
> to do to done, so that I can tell a client exactly what is in progress and what is finished.

Reading it back, three requirements fall out of it that a generic CRUD demo would miss. "My tasks"
means every query is scoped to the owner. "Track them from to do to done" means the status is a
business rule with a direction, not a free-text field. And a freelancer checking a client's work
needs to understand a rejection without reading a stack trace, which is why validation errors are
returned per field.

## The order I built things

1. The solution skeleton and the contracts: entities, ports, DTOs, the result type.
2. Domain rules with tests, before anything that touches a database.
3. Application services, with substitutes and a fake clock.
4. Infrastructure, against a real SQLite file created per test.
5. Endpoints, through `WebApplicationFactory`.
6. The frontend, against the contract that already existed and was already tested.

I front-loaded the contracts because the layers were being built in parallel. Fixing the interfaces
first meant a service could be written and tested without a working repository, and the API could be
written without a working database.

Domain first was deliberate too. The rules are the cheapest thing to test, the most likely thing to
be questioned in a review, and the only part that is pure logic. If they are right, the layers above
become plumbing.

## Architecture decisions

Four projects, dependencies pointing inwards: `Api -> Infrastructure -> Application -> Domain`.

The one I care about most is that Application has no package references at all. It is easy to claim a
layer is independent; an empty `<PackageReference>` list is proof. If someone adds a database
attribute or an HTTP type there, the build shows it immediately.

I used `Result` for expected failures instead of exceptions. Validation, a duplicate email, a missing
row, an illegal transition: these are normal outcomes of an API, not exceptional ones. Keeping them
as values made the controllers mechanical, since one helper maps an error type to a status code and I
never write that mapping twice.

Exceptions still exist for broken invariants. The difference is that they mean a bug, and one handler
turns them into a ProblemDetails response.

## The data layer

SQLite behind a connection factory, one repository per aggregate, and every value passed as a
parameter. There is no string concatenation anywhere in the data layer.

Mapping rows is manual, through the domain factories. That is more code than an ORM would need, but
it keeps entity construction in one place, so a task can only be created in a valid state.

One decision worth explaining: the ownership predicate is written twice. The service checks it, and
the `UPDATE` and `DELETE` statements repeat `WHERE id = @id AND user_id = @userId`. It looks
redundant, and it is on purpose. If the service layer ever gets it wrong, the database still refuses
to touch someone else's row.

Migrations are numbered SQL scripts embedded in the assembly, applied by a small migrator with a
`schema_version` table. It is about sixty lines and removes any design-time tooling from the setup.

SQLite was chosen for the reviewer's experience as much as mine: one command and the app runs, no
database container, no credentials. The repository tests also get a real engine at file speed instead
of a fake that would not catch a broken `ORDER BY`. The cost is a single writer, which is fine for
one person's board and documented as a limitation.

## Testing

TDD here meant writing the rule first and letting the test decide the service shape. The suite ended
at 107 tests, split by what each can prove.

Domain and application tests use substitutes and a fake clock, so no test depends on the real time or
a database. Infrastructure tests create a throwaway SQLite file per test, which is where mapping,
ordering, the migrator and the seeder get exercised for real. Endpoint tests go through the HTTP
pipeline, so status codes, authorization and the error body are covered as a client sees them.

Time being a port (`IClock`) was not an afterthought. Due dates have a "today" boundary, and a test
that depends on the machine clock fails at midnight for no reason.

The most useful moment of the exercise came from this suite. Two endpoint tests failed now and then,
always the same two, always on a cold start. The message was a unique constraint on `users.email`
during seeding. The cause was not the seeder: the API read the connection string before the test host
merged its configuration, so every test factory silently shared one database, and two app starts
seeded it at the same time. Moving the connection string resolution behind dependency injection and
giving each factory its own file fixed it. I verified with three cold runs, because one green run is
exactly what hid the bug in the first place.

## Security

Passwords use PBKDF2-SHA256 with 210 000 iterations, a random 16-byte salt per user and a
constant-time comparison. Sessions are JWT HS256 with an eight hour lifetime and validated issuer,
audience and lifetime.

Login does not reveal whether an account exists: an unknown email and a wrong password produce the
same 401 and the same message. Ownership follows the same idea. A task that belongs to another user
returns 404, not 403, because 403 confirms the row exists.

The signing key comes from configuration and startup fails if it is missing or too short. A
misconfigured deployment should break immediately rather than issue tokens nobody can verify.

## Frontend

I kept server state and session state separate. TanStack Query owns the tasks, a small context owns
the token and the current user, and one typed client adds the bearer token and turns ProblemDetails
into an error object with field messages. Those messages land under the matching input, which is why
the API keys validation errors by JSON field name.

The board shows three columns and only offers transitions the API allows, so the UI cannot send a
request the domain will reject. Loading, empty, error and pending states are explicit. A failed list
request keeps the cached board and shows a retry banner instead of logging the user out.

I spent time on accessibility and responsiveness because "user-friendly" is in the brief and because
it is cheap to get right while building: focus management in the modal, labelled inputs, `aria-live`
for errors, and a layout that works at 360px.

## Working with a GenAI assistant

I used an agentic assistant to scaffold the solution, then reviewed the output the way I would review
a junior developer's first pass.

The prompt pinned the constraints and the contract: no ORM or mediator, four projects, exact status
codes, the security parameters, tests per layer. The output was structurally good and wrong in the
details. It proposed a `DbContext` and MediatR handlers, hashed passwords with plain SHA-256, built
SQL with interpolation, returned 200 on create, and never checked ownership on read.

What I kept was the layout, the naming and most DTO shapes. What I rewrote was the persistence, the
security, the authorization and the status mapping.

The validation loop was always the same: check the constraints, compile with warnings as errors,
diff the endpoints against the contract, read the data layer adversarially (quote, null, wrong user,
missing row), write a failing test, then run it. [GENAI.md](GENAI.md) has the prompt, a sample of the
output, the full correction list and the edge cases.

The honest summary is that generated code is a fast first draft and a slow review, and the review is
the skill being tested.

## Trade-offs I accepted

- SQLite: zero setup, single writer. Right for this exercise, wrong for a busy deployment.
- Raw ADO.NET: every query is visible, and mapping is manual work that can drift.
- No refresh tokens and no login rate limiting. Eight hour tokens keep the demo simple.
- The token lives in `localStorage`. Simple, and exposed to XSS. An HttpOnly cookie with CSRF
  protection is the production answer.
- No automated frontend tests. Verification was manual plus axe-core scans.

## What I would do differently

- Put the API contract in the prompt as a table from the first message. Prose produced a wrong status
  code more than once.
- Ask for tests in the same response as the code, per slice. The tests written after review were
  better than the ones written before it.
- Write the frontend tests from the start instead of leaving them for the end and dropping them under
  time pressure.
- Self-host the font rather than depending on a network request during a demo.

If I had more time: refresh tokens with revocation, a Postgres profile behind the same factory,
optimistic concurrency with `If-Match`, and Playwright tests for login, create and status change.

The rest of the repository is the result. [README.md](README.md) explains how to run it and what the
endpoints do.
