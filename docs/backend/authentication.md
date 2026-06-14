# Authentication

Production authentication should support:

- Email and password login
- Secure password hashing
- Session or JWT-based authentication
- Role claims for Student, Instructor, and Admin
- Password reset in later versions

The current React + ASP.NET MVP includes demo login/register endpoints. Passwords are hashed in memory for the demo; production should persist users in PostgreSQL and add session/JWT handling.
