# WorkHub — Infrastructure Setup Checklist

## 1. Source Control

- [x] Create a GitHub repository (private) named `workhub`
- [x] Initialize with a `.gitignore` for .NET (`dotnet new gitignore`)
- [x] Add `appsettings.Development.json` to `.gitignore`
- [x] Push initial commit to `main`

---

## 2. Cloudflare R2 (Photo Storage)

- [x] Create a free Cloudflare account at https://dash.cloudflare.com
- [x] Navigate to R2 in the sidebar
- [x] Create a bucket named `workhub-photos`
- [x] **Leave public access OFF** — the bucket must stay private
- [x] Go to R2 → Manage R2 API Tokens → Create API Token
- [x] Set permissions to **Object Read & Write** on the `workhub-photos` bucket
- [x] Copy and save these values (you'll need them for Railway):
  - Account ID (visible in the Cloudflare dashboard URL or R2 overview)
  - Access Key ID (from the API token creation)
  - Secret Access Key (shown once — copy it immediately)

---

## 3. Railway (API Hosting + Database)

- [x] Create a Railway account at https://railway.app
- [ ] Subscribe to **Hobby plan ($5/month)** — prevents container sleep
- [ ] Create a new project named `workhub`

### PostgreSQL Database
- [ ] Add a PostgreSQL service to the project (click "New" → "Database" → "PostgreSQL")
- [ ] Railway auto-generates `DATABASE_URL` — no manual config needed
- [ ] Note: Railway Hobby plan includes daily point-in-time backups

### API Service
- [ ] Add a new service → connect your GitHub repo
- [ ] Point it at the `WorkHub.Api` project folder if it's in a subdirectory
- [ ] Set the deploy branch to `main`
- [ ] Railway will auto-detect the `.csproj` and build on every push

### Environment Variables
Set all of these in the Railway API service dashboard (Settings → Variables):

| Variable | Value | Source |
|---|---|---|
| `DATABASE_URL` | *(auto-set by Railway)* | Railway PostgreSQL service |
| `JWT_SECRET_KEY` | Generate with `openssl rand -base64 64` | You generate this once |
| `R2_ACCOUNT_ID` | Your Cloudflare account ID | Cloudflare dashboard |
| `R2_ACCESS_KEY_ID` | R2 API token access key | Cloudflare R2 token (step 2) |
| `R2_SECRET_ACCESS_KEY` | R2 API token secret key | Cloudflare R2 token (step 2) |
| `R2_BUCKET_NAME` | `workhub-photos` | You chose this in step 2 |
| `MINIMUM_APP_VERSION` | `1.0.0` | You set this; bump when forcing client updates |

- [ ] Generate the JWT secret: run `openssl rand -base64 64` in a terminal
- [ ] Enter all environment variables in Railway
- [ ] Verify the API deploys successfully (check Railway deploy logs)
- [ ] Test the health endpoint: `GET https://<your-railway-url>/v1/version`

---

## 4. Local Development Environment

### PostgreSQL (local)
- [ ] Option A: Install PostgreSQL locally
- [ ] Option B: Run via Docker:
  ```
  docker run -d -p 5432:5432 -e POSTGRES_DB=workhub -e POSTGRES_PASSWORD=dev postgres:16
  ```
- [ ] Create a local database named `workhub`

### API Configuration
- [ ] Create `WorkHub.Api/appsettings.Development.json`:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Database=workhub;Username=postgres;Password=dev"
    },
    "JWT_SECRET_KEY": "local-dev-key-minimum-thirty-two-characters-long",
    "R2_ACCOUNT_ID": "<your-real-cloudflare-account-id>",
    "R2_ACCESS_KEY_ID": "<your-real-r2-access-key>",
    "R2_SECRET_ACCESS_KEY": "<your-real-r2-secret-key>",
    "R2_BUCKET_NAME": "workhub-photos",
    "MINIMUM_APP_VERSION": "0.0.0"
  }
  ```
- [ ] Confirm `appsettings.Development.json` is in `.gitignore`
- [ ] Wire up `Program.cs` to read env vars first, fall back to config:
  ```csharp
  var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
      ?? builder.Configuration["JWT_SECRET_KEY"];
  ```

### MAUI Client
- [ ] Install .NET 8+ SDK
- [ ] Install MAUI workload: `dotnet workload install maui`
- [ ] For Android: install Android SDK via Visual Studio or standalone
- [ ] For Windows: WinUI development tools via Visual Studio
- [ ] Set the API base URL to `https://localhost:<port>/v1/` for local testing
  - Consider a build configuration flag to switch between local and production URLs

---

## 5. Database Initialization

- [ ] Install EF Core tools: `dotnet tool install --global dotnet-ef`
- [ ] Add NuGet packages to the API project:
  ```
  dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
  dotnet add package Microsoft.EntityFrameworkCore.Design
  dotnet add package BCrypt.Net-Next
  dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
  dotnet add package AWSSDK.S3
  ```
- [ ] Create DbContext and entity models
- [ ] Create initial migration: `dotnet ef migrations add InitialCreate`
- [ ] Add user seed migration with the 3 team member accounts (BCrypt-hashed initial passwords)
- [ ] Add auto-migration on startup in `Program.cs`:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<WorkHubDbContext>();
      db.Database.Migrate();
  }
  ```
- [ ] Run locally and verify the database schema is created
- [ ] Verify you can log in with a seeded user account

---

## 6. Database Backups (Off-Platform)

Railway includes daily backups, but an off-platform backup gives you a second safety net.

### Option A: GitHub Actions Nightly Backup
- [ ] Create `.github/workflows/backup.yml`:
  ```yaml
  name: Database Backup
  on:
    schedule:
      - cron: '0 4 * * *'  # 4 AM UTC daily
    workflow_dispatch:  # manual trigger

  jobs:
    backup:
      runs-on: ubuntu-latest
      steps:
        - name: Install PostgreSQL client
          run: sudo apt-get install -y postgresql-client

        - name: Dump database
          env:
            DATABASE_URL: ${{ secrets.DATABASE_URL }}
          run: |
            FILENAME="workhub-$(date +%Y%m%d-%H%M%S).sql.gz"
            pg_dump "$DATABASE_URL" | gzip > "$FILENAME"
            echo "FILENAME=$FILENAME" >> $GITHUB_ENV

        - name: Upload to R2
          env:
            AWS_ACCESS_KEY_ID: ${{ secrets.R2_ACCESS_KEY_ID }}
            AWS_SECRET_ACCESS_KEY: ${{ secrets.R2_SECRET_ACCESS_KEY }}
            R2_ENDPOINT: ${{ secrets.R2_ENDPOINT }}
          run: |
            aws s3 cp "$FILENAME" "s3://workhub-backups/$FILENAME" \
              --endpoint-url "$R2_ENDPOINT"
  ```
- [ ] Create a second R2 bucket named `workhub-backups`
- [ ] Add GitHub repository secrets:
  - `DATABASE_URL` — the Railway PostgreSQL connection string (external)
  - `R2_ACCESS_KEY_ID` — same Cloudflare R2 key
  - `R2_SECRET_ACCESS_KEY` — same Cloudflare R2 secret
  - `R2_ENDPOINT` — `https://<account-id>.r2.cloudflarestorage.com`
- [ ] Run the workflow manually once to verify it works
- [ ] Verify the backup file appears in the `workhub-backups` R2 bucket

### Option B: Manual Periodic Backup
- [ ] Periodically run `pg_dump` from your local machine against the Railway database
- [ ] Store the dump file somewhere safe (external drive, cloud storage)
- [ ] Less automated but better than nothing

---

## 7. App Distribution

### Android
- [ ] Choose a distribution method:
  - **Option A — Google Play Internal Testing** (recommended): free, handles updates, no Play Store listing required. Requires a Google Developer account ($25 one-time fee). Upload APK/AAB, invite testers by email.
  - **Option B — Direct APK sideload**: build signed APK, copy to each device manually. Simpler but updates require manual pushes to every device.
- [ ] Generate a signing keystore: `keytool -genkey -v -keystore workhub.keystore -alias workhub -keyalg RSA -keysize 2048 -validity 10000`
- [ ] Store the keystore and password securely (not in source control)
- [ ] Build signed release: `dotnet publish -f net8.0-android -c Release`

### Windows
- [ ] Build as MSIX or standalone folder: `dotnet publish -f net8.0-windows10.0.19041.0 -c Release`
- [ ] For MSIX: install on each Windows device (sideloading must be enabled)
- [ ] For standalone: copy the published folder to each device

---

## 8. Post-Setup Verification

Run through this checklist after everything is wired up:

- [ ] API health check returns version: `GET /v1/version`
- [ ] Login with a seeded user account returns tokens
- [ ] Creating a customer stores to the database
- [ ] Uploading a photo stores the file in R2 and returns a presigned URL
- [ ] The presigned URL loads the image in a browser
- [ ] Refresh token exchange returns a new token pair
- [ ] Lockout triggers after 5 failed login attempts
- [ ] MAUI app connects to the production API
- [ ] MAUI app can login, create a customer, upload a photo end-to-end

---

## 9. Create SETUP.md in Repo Root

Create a `SETUP.md` file in the repository that documents everything above for future reference. Include:

- [ ] Every environment variable name and where to get its value
- [ ] How to run the API locally
- [ ] How to run database migrations
- [ ] How to create a new user account (seed migration)
- [ ] How to reset a user's password (direct DB update or future admin endpoint)
- [ ] How to deploy (push to main)
- [ ] How to roll back a bad deploy (Railway dashboard → Deployments → Rollback)
- [ ] How to restore from a database backup
- [ ] How to build and distribute the Android APK
- [ ] How to build and distribute the Windows app
- [ ] Where all the accounts are (Railway, Cloudflare, GitHub, Google Play if used)
