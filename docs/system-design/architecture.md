# Architecture

## Target Architecture

```text
Frontend
  -> Backend API
      -> Auth Module
      -> User Module
      -> Scenario Module
      -> Simulation Module
      -> Chat Message Module
      -> Requirement Submission Module
      -> Evaluation Module
      -> Feedback Module
      -> Instructor Dashboard Module
      -> Database
      -> LLM Provider API
```

## MVP Demo Architecture

The current demo uses a ReactJS frontend and ASP.NET Core Minimal API backend. The backend owns scenario data, hidden requirement gating, scoring, and instructor review data.

## Production Boundary

In production, the frontend should call backend APIs only. The backend should own authentication, scenario data, hidden requirement logic, prompt construction, AI provider calls, evaluation storage, and instructor review workflows.
