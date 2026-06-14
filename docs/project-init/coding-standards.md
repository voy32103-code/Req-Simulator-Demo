# Coding Standards

## General

- Keep MVP code small and understandable.
- Prefer clear service boundaries over premature abstraction.
- Keep AI provider calls behind internal services.
- Avoid calling AI directly from the frontend in production.

## Frontend

- Build the actual learning workflow as the first screen.
- Keep controls predictable and efficient.
- Ensure layouts work on desktop and mobile.
- Keep visual design focused on training workflows, not marketing decoration.

## Backend

- Validate all inputs.
- Use role-based authorization.
- Persist simulation sessions and evaluation results.
- Store scenario and hidden requirement data in structured form.
