# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WorkHub is a cross-platform field service management app for a small 3-person team. It covers customer contacts, job tracking, inventory, scheduling, and photo documentation.

## Tech Stack

| Layer | Technology |
|---|---|
| Client | .NET MAUI (.NET 9) — Android + Windows |
| API | ASP.NET Core Web API (.NET 8+) |
| Database | PostgreSQL (Railway-hosted, EF Core + Npgsql) |
| Auth | Self-hosted JWT (BCrypt hashing, API-signed tokens, no external provider) |
| Photo Storage | Cloudflare R2 (private bucket, presigned URLs via AWSSDK.S3) |
| Hosting | Railway |

## Project Structure

```
/WorkHub.Api          — ASP.NET Core Web API
  /Controllers        — AuthController, CustomersController, JobsController, PhotosController, etc.
  /Services           — AuthService, PhotoService, TokenCleanupService
  /Models             — EF Core entity models
  /Data               — WorkHubDbContext, /Migrations
  /DTOs               — /Requests, /Responses
  Program.cs

/WorkHub              — .NET MAUI client (Android + Windows)
  /Controls           — DataStateView (loading/error/empty/content states)
  /Converters         — InverseBoolConverter, StatusColorConverter, etc.
  /Messages           — WeakReferenceMessenger message types (DetailMessages)
  /Models             — API DTOs (CustomerModels, JobModels, etc.)
  /Services           — AuthService, ApiService, PhotoService
  /ViewModels         — MVVM ViewModels (BaseViewModel + per-page VMs)
  /Views              — XAML pages and MainLayout (responsive split-view shell)
  MauiProgram.cs      — DI registration, HttpClient factory
  App.xaml.cs         — Startup flow (version check → session restore → navigate)
  AppShell.xaml.cs    — Route registrations
```

Naming: root namespace `WorkHub`, API project `WorkHub.Api`, app ID `com.workhub.app`.

## Build & Run Commands

```bash
# API
cd WorkHub.Api
dotnet restore
dotnet build
dotnet run

# MAUI Client (Windows)
cd WorkHub
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0

# MAUI Client (Android)
cd WorkHub
dotnet build -f net9.0-android

# Publish
dotnet publish -f net9.0-android -c Release
dotnet publish -f net9.0-windows10.0.19041.0 -c Release

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
- **Responsive split-view layout** — MainLayout uses AdaptiveTrigger at 720dp. Wide: left nav rail + list/detail split panel. Narrow: bottom tabs + full-page navigation.
- **WeakReferenceMessenger** for cross-component communication — list VMs send `ShowDetailMessage` to MainLayout, which renders detail inline (wide) or navigates via Shell (narrow). Cross-tab navigation (e.g. job→customer) uses `SwitchTabIndex` on `DetailRequest` plus `SelectListItemMessage` with `TabIndex` to select/scroll the target list item.
- **Address stored as single field** in API — client splits into Street/City/State/Zip fields for editing, combines to `"Street\nCity, State Zip"` format on save.
- **Two named HttpClients** — `"AuthClient"` (no auth handler, for login/refresh) and `"ApiClient"` (with `AuthDelegatingHandler` for token injection/refresh).

## Known MAUI Windows Issues

- **RefreshView breaks CollectionView scrolling** — do NOT wrap CollectionView in RefreshView on Windows. Lists get stuck and snap back to top. Lists load all pages upfront instead.
- **RemainingItemsThreshold unreliable on Windows** — don't rely on it for pagination. VMs loop through all API pages on load instead.
- **CollectionView flicker on item replace** — updating an item in an ObservableCollection causes the whole row to re-render. For quantity +/- buttons, update Entry.Text directly in code-behind and fire-and-forget the API call.
- **Calendar grid rebuild is slow** — don't rebuild the entire grid on day selection. Update border strokes directly on the old/new cells in the tap handler.
- **CarouselView is unusable on Windows** — swiping is janky and programmatic `Position` changes don't scroll backwards. PhotoViewerPopup shows a plain Image + prev/next buttons on Windows and keeps CarouselView only on Android.
- **Grouped CollectionView crashes on Windows** — `IsGrouped="True"` throws WinUI's "Value does not fall within the expected range" when groups are inserted or mutated at runtime (e.g. clearing a search filter restores filtered-out groups). Don't use IsGrouped. The inventory list renders a flat collection mixing header + item rows via a DataTemplateSelector; expand/collapse is plain row insert/remove.

## Database

14 tables: `users`, `refresh_tokens`, `customers`, `customer_contacts`, `contact_persons`, `customer_photos`, `jobs`, `job_notes`, `job_photos`, `inventory_items`, `job_inventory`, `job_adhoc_items`, `calendar_events`, `calendar_event_assignments`. All PKs are UUIDs. Auto-migration on API startup via `db.Database.Migrate()`. Seed data in `SeedData.cs` populates test data (200 customers, 500 jobs, 100 inventory items, 150 calendar events) when DB is empty.

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
