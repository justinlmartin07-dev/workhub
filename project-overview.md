# WorkHub — Project Overview

## Summary

A cross-platform WorkHub field service management app for a 3-person small business team. The app covers customer contact management, job tracking, inventory, scheduling, and photo documentation — all in a simple, intuitive interface.

## Target Platforms

- **Android** (phones/tablets)
- **Windows** (touchscreen-friendly)

## Devices

4–5 devices total across the team.

## Goals

- No data limits — unlimited customer and job records
- Fast, simple UI that anyone on the team can use without training
- Works seamlessly across Android and Windows from the same codebase

---

## Core Features

### Customers
- Customer name, phone number(s), address, and email
- Tap-to-call phone number (opens native phone app)
- Tap-to-navigate address (opens Google Maps and Google Earth)
- Customer photo
- Notes section per customer

### Jobs / Work in Progress
- Linked to a customer
- Status tracking (e.g. Pending, In Progress, Complete)
- Priority level
- Scope notes
- Job photos (camera capture + upload)
- Notes per job (timestamped, attributed to the user who wrote them)

### Inventory
- Item list with custom/user-defined items
- Search and filter
- Notes for inventory items
- Linked to jobs where applicable

### Calendar
- Simple calendar view
- Deadlines and meetings
- Per-event configurable reminders (e.g. 15 minutes, 1 hour, 1 day before)
- Assign one or more team members to events

---

## Project Naming

| Item | Value |
|---|---|
| App name | `WorkHub` |
| Solution | `WorkHub` |
| MAUI project | `WorkHub` |
| Root namespace | `WorkHub` |
| API project | `WorkHub.Api` |
| Application ID | `com.workhub.app` |
| Railway API service | `workhub-api` |
| Railway DB service | `workhub-db` |
| R2 bucket | `workhub-photos` |

## Tech Stack

| Layer | Technology |
|---|---|
| Client | .NET MAUI (Android + Windows) |
| API | ASP.NET Core Web API |
| Database | PostgreSQL |
| Hosting | Railway |
| Auth | Self-hosted JWT (BCrypt password hashing, API-signed tokens) |
| Photo Storage | Cloudflare R2 (private bucket, presigned URLs) |

---

## Guiding Principles

- **Dead simple** — every screen should be self-explanatory
- **Touch-first** — designed for fingers on both Android and Windows touchscreens
- **Reliable** — small team depends on it daily; stability over features
- **Scalable data** — no artificial limits on records
- **No external auth dependencies** — authentication is fully self-contained in the API with no third-party providers that could change policies or break flows
