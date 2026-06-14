# Backend

ASP.NET Core Minimal API for the Req Simulator MVP.

## Run

```text
dotnet run --project src/ReqSimulator.Api/ReqSimulator.Api.csproj --urls http://localhost:5088
```

## Demo Accounts

```text
Student: student@reqsim.local / demo123
Instructor: instructor@reqsim.local / demo123
```

## PostgreSQL Configuration

Use either `ConnectionStrings:DefaultConnection` or `DATABASE_URL`.

For local development, copy:

```text
src/ReqSimulator.Api/appsettings.Development.example.json
```

to:

```text
src/ReqSimulator.Api/appsettings.Development.json
```

and place the real Neon/PostgreSQL connection string there. The real local file is ignored by git.

## Current Scope

- Scenario API
- Simulation session API
- AI-style stakeholder response service
- Learner notes
- Requirement submission
- Rubric evaluation
- Instructor dashboard and reviews

The current implementation stores users in PostgreSQL and keeps scenario/session/evaluation demo data in memory. PostgreSQL can replace the remaining demo store later without changing the frontend contract.

## Gemini Configuration

Preferred local setup:

```text
cd src/ReqSimulator.Api
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY_HERE"
```

The API also checks `GEMINI_API_KEY` and `GOOGLE_API_KEY`. The real key is never returned by the API. If Gemini is configured but unavailable, the backend falls back to `MockAiService` and reports the actual provider used in the AI response payload.

## Users Table

On startup, the API creates the table if it does not exist:

```sql
CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    full_name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role TEXT NOT NULL CHECK (role IN ('Student', 'Instructor', 'Admin')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

The API also seeds:

```text
student@reqsim.local / demo123
instructor@reqsim.local / demo123
```

## External Login Demo

The MVP includes:

```text
POST /api/auth/external-demo
```

Body:

```json
{ "provider": "Google" }
```

or:

```json
{ "provider": "GitHub" }
```

This is a provider-tagged demo login that creates or reuses a PostgreSQL user. Replace it with real OAuth redirect/callback flow after adding Google/GitHub client credentials.
