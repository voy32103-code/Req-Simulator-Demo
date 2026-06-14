# Authorization

Authorization should be role-based.

## Rules

- Students can access only their own sessions and results.
- Instructors can access sessions for assigned classes.
- Admins can access all system management features.

Every backend route should validate the authenticated user's role and resource ownership.
