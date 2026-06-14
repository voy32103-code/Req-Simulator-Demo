# Deployment Architecture

## MVP Demo

The static website can be hosted on any static file host.

## Production

```text
Browser
  -> CDN / Frontend Host
  -> Backend API
  -> PostgreSQL
  -> LLM Provider
```

## Recommended Services

- Frontend: Vercel or Azure Static Web Apps
- Backend: Azure App Service, Render, or Railway
- Database: Managed PostgreSQL
- Observability: application logs, request tracing, AI cost monitoring
