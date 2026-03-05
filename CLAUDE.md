# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WorkHub is a cross-platform field service management app for a small 3-person team. It covers customer contacts, job tracking, inventory, scheduling, and photo documentation. No code has been written yet — only specification documents exist.

## Tech Stack

| Layer | Technology |
|---|---|
| Client | .NET MAUI (.NET 8+) — Android + Windows |
| API | ASP.NET Core Web API (.NET 8+) |
| Database | PostgreSQL (Railway-hosted, EF Core + Npgsql) |
| Auth | Self-hosted JWT (BCrypt hashing, API-signed tokens, no external provider) |
| Photo Storage | Cloudflare R2 (private bucket, presigned URLs via AWSSDK.S3) |
| Hosting | Railway |

## Project Structure (Planned)

```
/WorkHub.Api          — ASP.NET Core Web API
  /Controllers        — AuthController, CustomersController, JobsController, PhotosController, etc.
  /Services           — AuthService, PhotoService, TokenCleanupService
  /Models             — EF Core entity models
  /Data               — WorkHubDbContext, /Migrations
  /DTOs               — /Requests, /Responses
  Program.cs

/WorkHub              — .NET MAUI client (Android + Windows)
```

Naming: root namespace `WorkHub`, API project `WorkHub.Api`, app ID `com.workhub.app`.

## Build & Run Commands

```bash
# API
cd WorkHub.Api
dotnet restore
dotnet build
dotnet run

# MAUI Client
dotnet workload install maui
dotnet build -f net8.0-android
dotnet build -f net8.0-windows10.0.19041.0

# Publish
dotnet publish -f net8.0-android -c Release
dotnet publish -f net8.0-windows10.0.19041.0 -c Release

# Database Migrations
dotnet tool install --global dotnet-ef
dotnet ef migrations add <MigrationName> --project WorkHub.Api
dotnet ef database update --project WorkHub.Api
```

## Key NuGet Packages

**API:** `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `AWSSDK.S3`

**Client:** `CommunityToolkit.Maui`, `CommunityToolkit.Mvvm`, `Plugin.Maui.Calendar`, `Plugin.LocalNotification`, `SkiaSharp.Views.Maui.Controls`, `Microsoft.Extensions.Http.Polly`

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

## Database

12 tables: `users`, `refresh_tokens`, `customers`, `customer_photos`, `jobs`, `job_notes`, `job_photos`, `inventory_items`, `job_inventory`, `job_adhoc_items`, `calendar_events`, `calendar_event_assignments`. All PKs are UUIDs. Auto-migration on API startup via `db.Database.Migrate()`.

## Environment Variables (API)

`DATABASE_URL`, `JWT_SECRET_KEY`, `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET_NAME`, `MINIMUM_APP_VERSION`

Local dev config goes in `WorkHub.Api/appsettings.Development.json` (gitignored).

## Specification Documents

- `project-overview.md` — high-level features and goals
- `api-spec.md` — all endpoints, auth flow, error formats, R2 integration
- `database-spec.md` — full schema, relationships, indexes, cascade rules
- `client-spec.md` — MAUI UI/UX, navigation, MVVM patterns, platform differences
- `setup-checklist.md` — infrastructure setup steps (Railway, R2, local dev)
