# State Management

## Current React Demo

- App state is held with React state.
- Server state is loaded from the ASP.NET backend API.
- The current backend uses in-memory data, so sessions reset when the backend restarts.

## Production

- Server state should be loaded through API calls.
- Session and auth state should be handled centrally.
- React Query or a similar server-state library can be considered.
