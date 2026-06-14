# Setup Guide

## Backend

```text
dotnet run --project source/backend/src/ReqSimulator.Api/ReqSimulator.Api.csproj --urls http://localhost:5088
```

## Frontend

```text
cd source/frontend
npm install
npm run dev
```

Open:

```text
http://127.0.0.1:5173
```

## Optional Smoke Test

If Node.js is available:

```text
node --test source/frontend/tests/*.test.js
```

If .NET is available:

```text
dotnet build source/backend/src/ReqSimulator.Api/ReqSimulator.Api.csproj
```
