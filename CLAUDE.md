# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WorkHub is a cross-platform field service management app for a small 3-person team. It covers customer contacts, job tracking, inventory, scheduling, and photo documentation. Two projects: `WorkHub.Api` (ASP.NET Core Web API, PostgreSQL on Railway, photos in Cloudflare R2) and `WorkHub` (.NET MAUI client, Android + Windows).

## Build & Run Commands

```bash
# MAUI Client (Windows)
cd WorkHub
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0

# MAUI Client (Android)
cd WorkHub
dotnet build -f net9.0-android
```

### Client build profiles

The client's API base URL is a compile-time constant in `WorkHub/AppConfig.cs`, selected by build configuration (`-c`):

| Configuration | API URL |
|---|---|
| `Debug` (default) | `http://localhost:5180/` — local API (`10.0.2.2` on Android; cleartext HTTP is Debug-only) |
| `QA` | `https://workhub-api-staging.up.railway.app/` — Railway staging |
| `Release` | `https://workhub-api-production-1baa.up.railway.app/` — Railway production |

```bash
dotnet build -f net9.0-android -c QA        # staging build
dotnet publish -f net9.0-android -c Release # production build
```

## Architecture Decisions

- **Flat architecture** — no clean architecture or CQRS. Controllers call DbContext directly or via thin services. This is a small internal tool.
- **Self-hosted auth** — API validates credentials against BCrypt hashes, signs its own JWTs (30-min access tokens, 30-day refresh tokens with rotation). No external auth provider.
- **Soft deletes** on customers and jobs (`deleted_at` column). All list queries must filter `WHERE deleted_at IS NULL`. Customer deletion blocked if non-Complete jobs exist.
- **Photos proxied through API** — client POSTs multipart/form-data to API, API uploads to R2, returns presigned URL (1-hour expiry). Database stores only R2 object keys.
- **Address normalization** — server-side function strips punctuation, lowercases, and expands abbreviations for location photo lookups. Tag is snapshotted at upload time.
- **Last-write-wins** — no optimistic concurrency. Acceptable for 3 users.
- **No real-time/SignalR** — polling on app resume is sufficient.
- **All API routes prefixed with `/v1/`**. Auth endpoints (`/v1/auth/*`) and version check (`/v1/version`) are public; everything else requires `[Authorize]`.
- **50MB request body limit** at Kestrel level for photo uploads.
- **Client uses `SecureStorage`** for token persistence and MVVM pattern via CommunityToolkit.Mvvm source generators.
- **No paid UI libraries** — stock MAUI controls + CommunityToolkit only.
- **Responsive split-view layout** — MainLayout uses AdaptiveTrigger at 720dp. Wide: left nav rail + list/detail split panel. Narrow: bottom tabs + full-page navigation.
- **WeakReferenceMessenger** for cross-component communication — list VMs send `ShowDetailMessage` to MainLayout, which renders detail inline (wide) or navigates via Shell (narrow). Cross-tab navigation (e.g. job→customer) uses `SwitchTabIndex` on `DetailRequest` plus `SelectListItemMessage` with `TabIndex` to select/scroll the target list item.
- **Address stored as single field** in API — client splits into Street/City/State/Zip fields for editing, combines to `"Street\nCity, State Zip"` format on save.
- **Two named HttpClients** — `"AuthClient"` (no auth handler, for login/refresh) and `"ApiClient"` (with `AuthDelegatingHandler` for token injection/refresh).

## Database

Local dev uses PostgreSQL via Docker: `docker run -d --name workhub-db -e POSTGRES_USER=Admin -e POSTGRES_PASSWORD=Admin -e POSTGRES_DB=workhub -p 5432:5432 postgres:16`

## Environment Variables (API)

`DATABASE_URL`, `JWT_SECRET_KEY`, `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET_NAME`, `MINIMUM_APP_VERSION`, `GOOGLE_PLACES_API_KEY` (address autocomplete — feature silently disabled when unset), `BOOTSTRAP_USERS` (optional; `email:Name:password;…` — creates missing user accounts at startup, remove after first boot)

Local dev config goes in `WorkHub.Api/appsettings.Development.json` (gitignored).

## Specification Documents

- `project-overview.md` — high-level features and goals
- `api-spec.md` — all endpoints, auth flow, error formats, R2 integration
- `database-spec.md` — full schema, relationships, indexes, cascade rules
- `client-spec.md` — MAUI UI/UX, navigation, MVVM patterns, platform differences
- `setup-checklist.md` — infrastructure setup steps (Railway, R2, local dev)
