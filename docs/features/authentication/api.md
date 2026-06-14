# Authentication API

## Register

`POST /api/auth/register`

## Login

`POST /api/auth/login`

Request:

```json
{
  "email": "student@reqsim.local",
  "password": "demo123"
}
```

## Logout

`POST /api/auth/logout`

Logout can clear the server session or invalidate a refresh token, depending on the chosen auth approach.

## External Login Demo

`POST /api/auth/external-demo`

Request:

```json
{
  "provider": "Google"
}
```

Supported demo providers:

- Google
- GitHub

This endpoint is a development bridge. Production should use provider OAuth redirect/callback endpoints.
