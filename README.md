# WorkTracker

Internal engineering work ledger for one or more departments. A fresh empty database can load a sample team of seven. The workflow is:

**Plan the month → record the day → the Team Lead reviews it → report the month.**

The Team Lead is also a normal user. Their own monthly plan and daily timeline use the same tables as everyone else, and team management is an extra permission on top.

## Stack

- React, TypeScript, Vite, Tailwind CSS, React Router, Recharts
- ASP.NET Core Web API, Entity Framework Core, JWT
- PostgreSQL

```
frontend  →  ASP.NET Core REST API  →  EF Core  →  PostgreSQL
```

Business rules live in services. Controllers stay thin and return DTOs. The signed-in user always comes from the JWT, not from a user id sent by the browser.

## Run it

PostgreSQL must be listening on `localhost:5432` with user `postgres` / password `postgres`. The development database name is `worktracker`. Override the connection with `ConnectionStrings__DefaultConnection`.

```powershell
dotnet run --project backend/PrintFleet.WorkTracker.Api --launch-profile http
```

```powershell
cd frontend
npm install
npm run dev
```

- API: http://localhost:5080
- App: http://localhost:5174

The API applies EF Core migrations on startup. In Development, the October 2026 sample is loaded only when the database has no users yet. An existing company database is left as it is.

## Development logins

Password for every sample account: `WorkTracker@2026`

| Name | Email | Role |
| --- | --- | --- |
| Team Lead | lead@company.com | TeamLead |
| John Mathew | john@company.com | TeamMember |
| David Kumar | david@company.com | TeamMember |
| Rahul Sharma | rahul@company.com | TeamMember |
| Anita Desai | anita@company.com | TeamMember |
| Meera Iyer | meera@company.com | TeamMember |

On 8 October 2026, Rahul and Anita have not submitted the daily update. Anita's 7 October log is intentionally short so the low-hours notice is visible.

## What you can do

- Monthly plan: tasks, project, priority, estimate, status, remarks. A Team Lead can lock a month.
- Daily timeline: start and end time, calculated duration, linked monthly task, overlap check, and submit. Only an admin can reopen a submitted day.
- Team Lead dashboard: own work, team work, and a combined timeline. A team lead sees a member's hour totals and submitted days, and does not see that member's daily descriptions.
- Calendar, employee page, personal report, team report.
- Excel and PDF export for the daily sheet, a person's month, and the team month.
- Projects, team membership, and an audit trail of task and daily-entry changes.

Team members can only read and change their own plans and daily entries. A team lead can edit monthly plans in their own department and can unlock a locked month. A submitted day stays locked until an admin reopens it.

A monthly sheet keeps the department it was opened in. Moving someone to another department does not move that month's goals or the daily entries linked to them. Logging hours does not mark a goal complete; completion is the Completed status. A project that already has goals stays in its department. Cancelling a project or goal keeps the history.

## Operations

### Backup

Stop writes if you need a consistent copy, then from the PostgreSQL bin directory:

```powershell
$env:PGPASSWORD = '<postgres password>'
& 'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe' -h localhost -U postgres -d worktracker -Fc -f worktracker.dump
```

Do not put the mailbox password or `Jwt:Key` in the dump notes, tickets, or git. `appsettings.*.local.json` is gitignored and holds the mailbox password.

### Migrate

The API runs `MigrateAsync` on startup. The attendance migration adds employee numbers and attendance tables only. It does not change users, projects, plans, or daily work rows, and it does not import spreadsheet people. Take the backup above before applying it on a machine that already has data. If it must be rolled back, stop the API, restore that backup, and deploy the previous build. To apply migrations without starting the site:

```powershell
dotnet tool run dotnet-ef database update --project backend/PrintFleet.WorkTracker.Api
```

### Deploy

1. Take a backup.
2. Publish the API and the frontend build.
3. Set `ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `Cors__Origins__0`, and the `Email` section outside source control. Production rejects a JWT key that contains `DEV_ONLY`.
4. Keep the reminder at 17:00 India time, Monday to Friday. The daily reminder goes to active members of each department switched on under Email Reminders. It skips anyone who already submitted that day, or who is marked Holiday or Leave. `Email:Recipients` is no longer used. Monthly-plan reminders use Outlook Graph when `Email:Graph` is complete, and SMTP when it is not. The API logs a missing Graph setup and still starts. Plan reminders stay off until an admin enables them.
5. Start the API, then the web app. The API listens on port 5080 and the web app on 5174.

### Rollback

1. Stop the API.
2. Restore the backup: `pg_restore -h localhost -U postgres -d worktracker --clean --if-exists worktracker.dump`.
3. Deploy the previous API and frontend build.
4. Start the API again. Do not run a newer migration against the restored database.

## Configuration

Set these outside source control for anything other than the local sample:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (the development key is marked `DEV_ONLY` and is rejected in Production)
- `Jwt__Issuer`, `Jwt__Audience`
- `Cors__Origins__0`
- `Seed__Enabled` if a non-Development environment should load the sample
