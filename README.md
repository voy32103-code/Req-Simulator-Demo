# Req Simulator

Req Simulator helps IT students and career switchers practice requirement gathering by interviewing AI-simulated stakeholders, discovering hidden requirements, and receiving structured feedback like a real Business Analyst training environment.

This repository currently contains a ReactJS frontend and ASP.NET Core backend MVP demo. It demonstrates the core flow:

- Scenario library
- AI stakeholder chat with hidden requirement gating
- Learner notes
- Requirement submission
- Gemini-backed AI scoring and feedback with mock fallback
- Instructor dashboard and review comments
- English / Vietnamese demo UI switch

## Run The MVP

Start the backend:

```text
dotnet run --project source/backend/src/ReqSimulator.Api/ReqSimulator.Api.csproj --urls http://localhost:5088
```

Start the frontend:

```text
cd source/frontend
npm install
npm run dev
```

Open:

```text
http://127.0.0.1:5173
```

Demo accounts:

```text
Student: student@reqsim.local / demo123
Instructor: instructor@reqsim.local / demo123
```

The login screen also includes Google and GitHub demo buttons. They call the backend demo external-login endpoint and create provider-tagged users in PostgreSQL. Real OAuth can replace this once Google/GitHub client credentials are available.

## Project Structure

```text
access/             Roles, permissions, and API key handling notes
docs/               Product, system design, backend, frontend, testing, and deployment docs
source/backend/     ASP.NET Core Minimal API
source/frontend/    ReactJS frontend
```

## Demo Focus

Req Simulator remains a domain-flexible requirement analysis training platform. The current MVP demo prioritizes the `E-commerce Order & Promotion System` scenario first, while still keeping education and healthcare scenarios available for later demos.

The e-commerce stakeholder does not reveal all hidden requirements immediately. Learners should ask about voucher rules, stock checking, stock reservation, payment failures, shipping fees, cancellation, return and refund flows, actor permissions, and admin reporting.

## Local Secrets

The backend reads PostgreSQL configuration from `ConnectionStrings:DefaultConnection` or `DATABASE_URL`. The local `appsettings.Development.json` file is ignored by git; keep real database keys there or in environment variables only.

Auth now uses PostgreSQL for the `users` table. Scenario, chat, submission, and evaluation data are still in-memory for the MVP demo.

## Real AI With Gemini

The backend calls Gemini only from ASP.NET Core. The frontend never receives the API key. If no key is configured, or if Gemini fails during the demo, the app automatically uses `MockAiService`.

PowerShell:

```powershell
$env:GEMINI_API_KEY="YOUR_KEY_HERE"
dotnet run --project source/backend/src/ReqSimulator.Api/ReqSimulator.Api.csproj --urls http://localhost:5088
```

macOS/Linux:

```bash
export GEMINI_API_KEY="YOUR_KEY_HERE"
dotnet run --project source/backend/src/ReqSimulator.Api/ReqSimulator.Api.csproj --urls http://localhost:5088
```

ASP.NET Core user secrets:

```bash
cd source/backend/src/ReqSimulator.Api
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY_HERE"
```

Optional Gemini config lives in `appsettings.Development.json` or environment variables:

```json
{
  "Gemini": {
    "Model": "gemini-2.0-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
    "ApiKey": "YOUR_KEY_HERE"
  }
}
```

## Verify AI Setup

Check backend AI status without exposing secrets:

```text
GET http://localhost:5088/api/ai/status
```

Try a backend-only AI test:

```text
POST http://localhost:5088/api/ai/test
{
  "message": "Say hello as an e-commerce operations manager."
}
```

If no API key is configured, the backend will report `provider: Mock` and continue serving the demo safely.
If Gemini is configured but unavailable at runtime, the backend now falls back safely and returns `provider: Mock` for the affected AI response.
