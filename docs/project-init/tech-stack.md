# Tech Stack

## MVP Demo

- Frontend: ReactJS with Vite
- Backend: ASP.NET Core Minimal API
- Styling: CSS implementation based on the provided dark cyan glass UX/UI demo
- Storage: in-memory demo data for the first integration pass

## Recommended Production Stack

- Frontend: React or Next.js with TypeScript
- Backend: ASP.NET Core Web API
- Database: PostgreSQL
- AI: OpenAI, Gemini, or Azure OpenAI behind an internal service layer
- Deployment: Docker, Vercel for frontend, Render/Railway/Azure App Service for backend

## Rationale

The current MVP keeps infrastructure light while separating frontend and backend concerns. The backend API can later move from in-memory demo data to PostgreSQL and a real LLM provider service.
