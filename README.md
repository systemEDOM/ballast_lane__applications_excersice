# Ballast Lane Tasks

A task board for the .NET technical exercise. You register, sign in, and manage your own tasks from
a React client backed by an ASP.NET Core API.

The brief asked for Clean Architecture, tests, and no Entity Framework, Dapper or MediatR, so the
data layer is hand-written ADO.NET over SQLite and the projects depend inwards only.

## User story

> As a freelancer juggling several clients, I want to keep my tasks in one place and track them from
> to do to done, so that I can tell a client exactly what is in progress and what is finished.

## What you need

Docker Desktop, running. No .NET SDK, Node or database server is required, because everything runs in
containers. Commands below use PowerShell syntax.

## Start the app

```bash
docker compose up --build
```

The first run builds two images, which takes a couple of minutes. After that:

| | |
|---|---|
| App | http://localhost:8080 |
| API documentation | http://localhost:5080/swagger |
| Health check | http://localhost:5080/health |

Demo account: `demo@ballastlane.test` / `Demo12345!`, with six tasks already seeded.

```bash
docker compose down       # stop, keep the data
docker compose down -v    # stop and delete the database, so the next start seeds again
```

Only the API, if you just want Swagger:

```bash
docker compose up -d --build api
```

## Run the tests

```bash
docker compose --profile tests run --rm tests
```

107 tests: 55 for the domain and application layer, 28 for repositories, migrations and security
helpers, 24 through the HTTP endpoints. A failing test fails the image build too.

The same suite without Compose. It only runs the tests, it does not start a server:

```bash
docker run --rm -v "$PWD/api:/src" -w /src -v ballast-nuget:/root/.nuget/packages \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet test BallastLane.sln -c Release
```

`/src` is the working directory inside the container, not a folder of the solution. The named volume
keeps the NuGet cache between runs.

## Project layout

```
api/    .NET solution: Domain, Application, Infrastructure, Api, three test projects
web/    React client (Vite, TypeScript)
```

Domain holds the entities and their rules. Application holds the use cases, the validation and the
interfaces that Infrastructure implements. Infrastructure is the only project that knows SQLite
exists. Api wires everything together and is the only one that knows about HTTP.

Two tables, `users` and `tasks`. Migrations are numbered SQL scripts applied by a small migrator, and
the seed only runs when the demo account is missing. Passwords use PBKDF2-SHA256, sessions are JWT
HS256, and the signing key comes from `Jwt__Key` in the container.

## Endpoints

| Method | Route | |
|---|---|---|
| POST | `/api/auth/register` | Create an account, returns a token |
| POST | `/api/auth/login` | Sign in |
| GET | `/api/auth/me` | Current user, needs a token |
| GET | `/api/public/info` | Anonymous |
| GET | `/api/tasks` | List, optional `?status=` |
| GET | `/api/tasks/{id}` | One task |
| POST | `/api/tasks` | Create |
| PUT | `/api/tasks/{id}` | Update title, description, due date |
| PATCH | `/api/tasks/{id}/status` | Change status |
| DELETE | `/api/tasks/{id}` | Delete |

Task routes are scoped to the token owner. Swagger on port 5080 has the request and response shapes.

## Rules

- Email is trimmed, lowercased and must be unique. A duplicate returns 409.
- Password is 8 to 128 characters with at least one letter and one digit.
- A wrong password and an unknown email get the same 401.
- Title is 3 to 120 characters, description optional up to 1000, due date not in the past.
- Status only moves `Todo -> InProgress -> Done`.
- Another user's task returns 404, never 403.

## Frontend development

The SPA is served by nginx inside the stack. For frontend work, use the Node installation on the host
and let Vite proxy `/api` to the running API:

```bash
cd web
npm install
npm run dev
```

On Windows, if `npm` fails with an execution policy error, use `npm.cmd`. For backend changes,
rebuild the API container with `docker compose up -d --build api`.

The thought process behind the build is in [PRESENTATION.md](PRESENTATION.md), and the GenAI write-up
is in [GENAI.md](GENAI.md).
