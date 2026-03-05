# WorkHub Database — Detailed Specification

## Technology

**PostgreSQL** hosted on Railway as a managed service. Accessed via **Entity Framework Core** with the Npgsql provider.

---

## Schema

### `users`

Stores all user accounts. Accounts are seeded manually via an EF Core migration or a CLI seed command — there is no public registration.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `email` | `varchar(200)` | Unique, used for login |
| `password_hash` | `varchar(200)` | BCrypt hash of the user's password |
| `name` | `varchar(200)` | Display name |
| `profile_photo_r2_key` | `varchar(500)` | R2 object key for profile photo, nullable |
| `failed_login_attempts` | `integer` | Default 0. Incremented on each failed login. Reset to 0 on successful login |
| `locked_until` | `timestamptz` | Nullable. If set and in the future, login is blocked. Set to `now + 15 min` when `failed_login_attempts` reaches 5 |
| `created_at` | `timestamptz` | |

### `refresh_tokens`

Stores active refresh tokens. Each row represents one valid session. Tokens are deleted on logout or when replaced by a refresh rotation.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `user_id` | `uuid` | FK → `users.id` |
| `token_hash` | `varchar(200)` | SHA-256 hash of the refresh token (never store plaintext) |
| `expires_at` | `timestamptz` | When this refresh token expires |
| `created_at` | `timestamptz` | |

### `customers`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key, default `gen_random_uuid()` |
| `name` | `varchar(200)` | Required |
| `phone` | `varchar(50)` | |
| `email` | `varchar(200)` | |
| `address` | `text` | Full address string |
| `normalized_address` | `varchar(500)` | Lowercased, punctuation-stripped version of address. Auto-populated on save. Used for location photo lookups |
| `notes` | `text` | General notes — single freeform text field, edited via `PUT /v1/customers/{id}` |
| `created_by` | `uuid` | FK → `users.id` |
| `created_at` | `timestamptz` | Auto-set on insert |
| `updated_at` | `timestamptz` | Auto-updated |
| `deleted_at` | `timestamptz` | Null = active, set = soft deleted |

### `customer_photos`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `customer_id` | `uuid` | FK → `customers.id` |
| `r2_object_key` | `varchar(500)` | R2 object key, e.g. `customers/{customerId}/{photoId}.jpg` |
| `file_name` | `varchar(255)` | Original filename |
| `address_tag` | `varchar(500)` | Normalized address string, auto-set from customer address on upload |
| `uploaded_at` | `timestamptz` | |

### `jobs`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `customer_id` | `uuid` | FK → `customers.id` |
| `title` | `varchar(200)` | Short job description |
| `status` | `varchar(50)` | `Pending`, `In Progress`, or `Complete` |
| `priority` | `varchar(50)` | `Low`, `Normal`, or `High` |
| `scope_notes` | `text` | Detailed scope / description |
| `created_by` | `uuid` | FK → `users.id` |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | |
| `deleted_at` | `timestamptz` | Null = active, set = soft deleted |

### `job_notes`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `job_id` | `uuid` | FK → `jobs.id` |
| `content` | `text` | Note text |
| `created_by` | `uuid` | FK → `users.id` — who wrote the note |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | Null on first insert, set on edit |

### `job_photos`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `job_id` | `uuid` | FK → `jobs.id` |
| `r2_object_key` | `varchar(500)` | R2 object key, e.g. `jobs/{jobId}/{photoId}.jpg` |
| `file_name` | `varchar(255)` | Original filename |
| `address_tag` | `varchar(500)` | Normalized address string, auto-set from customer's address via job on upload |
| `uploaded_at` | `timestamptz` | |

### `inventory_items`

The library of known parts. Reference only — no stock tracking.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `name` | `varchar(200)` | Required |
| `description` | `text` | |
| `part_number` | `varchar(100)` | |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | |

### `job_inventory`

Links a job to a library inventory item. Used for both the **Used** and **To Be Ordered** lists via `list_type`.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `job_id` | `uuid` | FK → `jobs.id` |
| `inventory_item_id` | `uuid` | FK → `inventory_items.id` |
| `quantity` | `integer` | Required, default 1 |
| `list_type` | `varchar(20)` | `'used'` or `'to_order'` |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | Null on first insert, set on edit |

### `job_adhoc_items`

Free-text items added directly to a job without a library reference. Same `list_type` flag as `job_inventory`.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `job_id` | `uuid` | FK → `jobs.id` |
| `name` | `varchar(200)` | Required |
| `description` | `text` | |
| `quantity` | `integer` | Required, default 1 |
| `list_type` | `varchar(20)` | `'used'` or `'to_order'` |
| `created_at` | `timestamptz` | |
| `updated_at` | `timestamptz` | Null on first insert, set on edit |

### `calendar_events`

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `title` | `varchar(200)` | |
| `description` | `text` | |
| `start_time` | `timestamptz` | |
| `end_time` | `timestamptz` | Nullable for deadlines |
| `reminder_minutes` | `integer` | Nullable. Minutes before `start_time` to fire a local notification. Common values: 15, 30, 60, 1440 (1 day). Null = no reminder |
| `customer_id` | `uuid` | FK → `customers.id`, nullable |
| `job_id` | `uuid` | FK → `jobs.id`, nullable |
| `created_by` | `uuid` | FK → `users.id` |
| `created_at` | `timestamptz` | |

### `calendar_event_assignments`

Allows multiple users to be assigned to a single calendar event.

| Column | Type | Notes |
|---|---|---|
| `id` | `uuid` | Primary key |
| `calendar_event_id` | `uuid` | FK → `calendar_events.id` |
| `user_id` | `uuid` | FK → `users.id` |
| `created_at` | `timestamptz` | |

---

## Relationships

```
users
  ├── refresh_tokens      (one-to-many — active sessions)
  ├── customers           (created_by)
  ├── jobs                (created_by)
  └── job_notes           (created_by)

customers
  ├── customer_photos     (one-to-many)
  ├── jobs                (one-to-many)
  │     ├── job_notes     (one-to-many)
  │     ├── job_photos    (one-to-many)
  │     ├── job_inventory (one-to-many, list_type: 'used' | 'to_order')
  │     │     └── inventory_items (many-to-one)
  │     └── job_adhoc_items (one-to-many, list_type: 'used' | 'to_order')
  └── calendar_events     (optional link)

inventory_items                    (reference library, standalone)
calendar_events                    (optionally linked to customer and/or job)
  └── calendar_event_assignments   (one-to-many → users)
```

---

## Entity Framework Setup

```csharp
// Npgsql EF Core provider
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

// DbContext
public class WorkHubDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPhoto> CustomerPhotos => Set<CustomerPhoto>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobNote> JobNotes => Set<JobNote>();
    public DbSet<JobPhoto> JobPhotos => Set<JobPhoto>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<JobInventory> JobInventory => Set<JobInventory>();
    public DbSet<JobAdhocItem> JobAdhocItems => Set<JobAdhocItem>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventAssignment> CalendarEventAssignments => Set<CalendarEventAssignment>();
}
```

Connection string comes from the `DATABASE_URL` environment variable set by Railway.

---

## User Seeding

There is no registration endpoint — users are created directly in the database. Use an EF Core seed migration to insert the initial team:

```csharp
// In a migration or DbContext.OnModelCreating
modelBuilder.Entity<User>().HasData(
    new User
    {
        Id = Guid.Parse("..."),
        Email = "mike@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("initial-password-1"),
        Name = "Mike",
        FailedLoginAttempts = 0,
        LockedUntil = null,
        CreatedAt = DateTimeOffset.UtcNow
    },
    new User
    {
        Id = Guid.Parse("..."),
        Email = "sarah@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("initial-password-2"),
        Name = "Sarah",
        FailedLoginAttempts = 0,
        LockedUntil = null,
        CreatedAt = DateTimeOffset.UtcNow
    },
    new User
    {
        Id = Guid.Parse("..."),
        Email = "dave@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("initial-password-3"),
        Name = "Dave",
        FailedLoginAttempts = 0,
        LockedUntil = null,
        CreatedAt = DateTimeOffset.UtcNow
    }
);
```

Each team member logs in with their email and initial password. A password change endpoint (`PUT /v1/me/password`) lets them set their own password after first login.

---

## Soft Delete & Cascade Rules

### Customer deletion

A `DELETE /v1/customers/{id}` request is **blocked with a `409 Conflict`** if the customer has any jobs with `status` other than `Complete` (i.e. any `Pending` or `In Progress` jobs). The error response lists the blocking jobs so the user knows what to resolve first.

If all jobs are `Complete` (or there are no jobs at all), the soft delete proceeds — `deleted_at` is set on the customer record. Completed jobs linked to that customer are **also soft-deleted** in the same operation so they don't appear as orphaned records in the jobs list.

```
DELETE /v1/customers/{id}
  ├── Any non-Complete jobs? → 409 Conflict (list blocking jobs)
  └── All jobs Complete or none?
        ├── Set customer.deleted_at
        └── Set deleted_at on all linked Complete jobs
```

Customer photos, job photos, job notes, and job items are **not** soft-deleted or removed — they remain in the database attached to the soft-deleted parent. If a restore feature is added later, all child data is still intact.

### Job deletion

A `DELETE /v1/jobs/{id}` sets `deleted_at` on the job. No cascade to child records — notes, photos, and items remain attached. No blocking conditions on job deletion.

---

## Sort Order Conventions

Different record types use different sort orders based on how they're consumed in the UI:

| Record type | Sort order | Rationale |
|---|---|---|
| Job notes | `created_at ASC` (oldest first) | Reads like a chronological log — new notes appear at the bottom |
| Job photos | `uploaded_at DESC` (newest first) | Most recent photos are most relevant, shown at the top |
| Customer photos | `uploaded_at DESC` (newest first) | Same as job photos |
| Location photo groups | Most recent `uploaded_at DESC` | Most recent work at the address shown first |

These sort orders are enforced by the API. The client does not re-sort results.

---

## Key Decisions

- **UUIDs as primary keys** — avoids integer ID enumeration issues and works well across distributed clients
- **Self-hosted auth with BCrypt** — no external auth provider. The `users` table stores BCrypt-hashed passwords. The API validates credentials and issues its own JWTs. This eliminates all third-party auth dependencies and works on devices with no browser. User accounts are seeded directly in the database
- **Login lockout** — after 5 consecutive failed login attempts, the account is locked for 15 minutes. The `failed_login_attempts` counter resets to 0 on successful login. The `locked_until` timestamp is checked on every login attempt — if it's in the future, the login is rejected immediately without checking the password. This prevents brute-force attacks on the public login endpoint
- **Refresh tokens stored as hashes** — the `refresh_tokens` table stores SHA-256 hashes of refresh tokens, never plaintext. If the database is compromised, the tokens are useless. Tokens are rotated on every refresh (old token deleted, new token issued)
- **R2 object keys for photos** — the database stores only a short key string per photo (`r2_object_key`). Image bytes live in Cloudflare R2 in a **private bucket**. The API generates presigned URLs when returning photo data to the client. This keeps the DB lean and ensures photos are only accessible through authenticated API calls
- **Address normalization for location photo lookups** — a `normalized_address` column on `customers` stores a lowercased, punctuation-stripped version of the address (e.g. "48 elm st minneapolis"). Auto-populated on insert/update. Used to match photos across different customers at the same address without relying on exact string matching. See the API spec for normalization rules and known limitations
- **Soft deletes on customers and jobs** — a `deleted_at` timestamp flags records as deleted without removing them. All list queries filter `WHERE deleted_at IS NULL`. Deleted records can be recovered by clearing the flag. See "Soft Delete & Cascade Rules" above for cascade behavior
- **Migrations via EF Core** — run `dotnet ef migrations add` locally, apply on deploy
- **`ILIKE` for search** — PostgreSQL's case-insensitive `LIKE`, sufficient without needing full-text search at this scale
- **Inventory delete protection** — the API checks for any `job_inventory` rows referencing the item before deletion and returns a `409 Conflict` with a clear error message if any exist. Hard deletes are blocked; the user must remove the item from all jobs first
- **Address tagging on photos** — both `customer_photos` and `job_photos` store a normalized `address_tag` set automatically at upload time. Normalization strips punctuation and lowercases the string so minor variations ("48 Elm St" vs "48 Elm Street") match correctly. The tag is derived from the customer's address at the moment of upload — if the customer's address changes later, existing photo tags are not retroactively updated, which is intentional (the tag represents where the photo was taken, not the customer's current address)
- **`list_type` flag instead of separate tables** — `job_inventory` and `job_adhoc_items` both use a `list_type` column (`'used'` or `'to_order'`) rather than duplicating tables. Both lists have identical structure so a flag is cleaner and easier to extend later
- **`created_by` on job notes** — every note records which team member wrote it. Useful for a shared tool where all 3 users see the same data
- **`updated_at` on mutable child records** — `job_notes`, `job_inventory`, and `job_adhoc_items` all track when they were last edited. Null on first insert, set on any subsequent update
- **`reminder_minutes` on calendar events** — a simple nullable integer representing minutes before the event to fire a local notification. The client is responsible for scheduling the local notification; the server just stores the preference
- **Last-write-wins on concurrent edits** — no optimistic concurrency checks. For a 3-person internal tool the collision risk is negligible and the added complexity isn't worth it

---

## Indexes to Add

```sql
-- Auth lookups
CREATE UNIQUE INDEX idx_users_email ON users(email);
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_expires_at ON refresh_tokens(expires_at);

-- Soft delete filters (partial indexes for active records only)
CREATE INDEX idx_customers_deleted_at ON customers(deleted_at) WHERE deleted_at IS NULL;
CREATE INDEX idx_jobs_deleted_at ON jobs(deleted_at) WHERE deleted_at IS NULL;

-- Address lookups
CREATE INDEX idx_customers_normalized_address ON customers(normalized_address);

-- Foreign key lookups
CREATE INDEX idx_jobs_customer_id ON jobs(customer_id);
CREATE INDEX idx_jobs_status ON jobs(status);
CREATE INDEX idx_job_notes_job_id ON job_notes(job_id);
CREATE INDEX idx_job_inventory_job_id ON job_inventory(job_id);
CREATE INDEX idx_job_adhoc_items_job_id ON job_adhoc_items(job_id);
CREATE INDEX idx_calendar_event_assignments_event_id ON calendar_event_assignments(calendar_event_id);
CREATE INDEX idx_calendar_event_assignments_user_id ON calendar_event_assignments(user_id);

-- Composite indexes for filtered queries
CREATE INDEX idx_job_inventory_list_type ON job_inventory(job_id, list_type);
CREATE INDEX idx_job_adhoc_items_list_type ON job_adhoc_items(job_id, list_type);
CREATE INDEX idx_customer_photos_uploaded_at ON customer_photos(customer_id, uploaded_at DESC);
CREATE INDEX idx_job_photos_uploaded_at ON job_photos(job_id, uploaded_at DESC);

-- Address tag lookups for location photo feature
CREATE INDEX idx_customer_photos_address_tag ON customer_photos(address_tag);
CREATE INDEX idx_job_photos_address_tag ON job_photos(address_tag);

-- Calendar queries
CREATE INDEX idx_calendar_events_start_time ON calendar_events(start_time);

-- Full-text search on customer name
CREATE INDEX idx_customers_name ON customers USING gin(to_tsvector('english', name));
```

The composite `(job_id, list_type)` indexes on both item tables mean fetching "all used items for job X" or "all to-order items for job X" will be fast without a full table scan.
