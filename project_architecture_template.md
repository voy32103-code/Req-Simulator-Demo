# Project Architecture Template

## Context

Req Simulator is a web application for Business Analyst and Requirement Engineering training.

## High-Level Architecture

```text
Frontend
  -> Backend API
      -> Auth Service
      -> Scenario Service
      -> Simulation Service
      -> Evaluation Service
      -> Instructor Dashboard Service
      -> Database
      -> LLM Provider API
```

## MVP Prototype

The current website prototype runs fully in the browser. It keeps the service boundaries visible in code so the logic can later move into a backend API.

## Future Production Target

- React or Next.js frontend
- ASP.NET Core Web API backend
- PostgreSQL database
- LLM provider behind an internal AI service layer
- Docker-based deployment
