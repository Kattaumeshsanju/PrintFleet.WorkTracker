# WorkTracker

Internal engineering work ledger for a team of seven. The workflow is:

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
- App: http://localhost:5173

The API applies EF Core migrations and loads the October 2026 sample on first startup in Development.

## Development logins

Password for every sample account: `WorkTracker@2026`

| Name | Email | Role |
| --- | --- | --- |
| Team Lead | lead@company.com | TeamLead |
| John Mathew | john@company.com | TeamMember |
| David Kumar | david@company.com | TeamMember |
| Rahul Sharma | rahul@company.com | TeamMember |
| Suresh Nair | suresh@company.com | TeamMember |
| Anita Desai | anita@company.com | TeamMember |
| Meera Iyer | meera@company.com | TeamMember |

On 8 October 2026, Rahul and Anita have not submitted the daily update. Anita's 7 October log is intentionally short so the low-hours notice is visible.

## What you can do

- Monthly plan: tasks, project, priority, estimate, status, remarks. A Team Lead can lock a month.
- Daily timeline: start and end time, calculated duration, linked monthly task, overlap check, submit and reopen.
- Team Lead dashboard: own work, team work, and a combined timeline.
- Calendar, employee page, personal report, team report.
- Excel and PDF export for the daily sheet, a person's month, and the team month.
- Projects, team membership, and an audit trail of task and daily-entry changes.

Team members can only read and change their own plans and daily entries. A locked month or a submitted day cannot be edited until the Team Lead unlocks or reopens it.

## Configuration

Set these outside source control for anything other than the local sample:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (the development key is marked `DEV_ONLY` and is rejected in Production)
- `Jwt__Issuer`, `Jwt__Audience`
- `Cors__Origins__0`
- `Seed__Enabled` if a non-Development environment should load the sample
