# API Design

## Auth

- `POST /api/auth/register`
- `POST /api/auth/login`

## Scenarios

- `GET /api/scenarios`
- `GET /api/scenarios/{id}`
- `POST /api/scenarios`
- `PUT /api/scenarios/{id}`

## Simulation

- `POST /api/sessions`
- `GET /api/sessions/{id}`
- `POST /api/sessions/{id}/messages`
- `PUT /api/sessions/{id}/notes`

## Submission And Evaluation

- `POST /api/sessions/{id}/submissions`
- `GET /api/sessions/{id}/evaluation`

## Instructor

- `GET /api/instructor/dashboard`
- `POST /api/instructor/reviews`
