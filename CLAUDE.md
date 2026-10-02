# Book Exchange Platform

Hyperlocal book exchange web app (PWA). Users list books they own, find books nearby, and hand them over in person, either as a direct swap or by spending a book credit.

The full original specification lives in `docs/SPEC.md`. **This file overrides SPEC.md wherever they disagree.** When in doubt, ask.

## Product principles

- The #1 product risk is liquidity (not enough matching books nearby). Prefer features that make it easier for a book to find a reader.
- Listing a book must take under ~30 seconds: barcode scan → auto-filled metadata → photo → publish.
- Local first: search defaults to "near me". No shipping.

## Changes vs SPEC.md

### Stack (simplified for v1)
- Frontend: React + TypeScript + Vite + Tailwind + TanStack Query, delivered as a **PWA** (installable, web push, camera access). Barcode scanning via a JS library (e.g. ZXing), since Safari lacks `BarcodeDetector`.
- Backend: .NET 10, ASP.NET Core Web API, EF Core, FluentValidation, SignalR (in-process).
- Database: PostgreSQL with **PostGIS** (geo search) and **built-in full-text search** (`tsvector`) for title/author.
- **No Redis in v1.** Use ASP.NET Core's built-in rate limiter and PostgreSQL. Revisit only when scaling to multiple instances (then also Azure SignalR Service).
- Background work: `BackgroundService` + **transactional outbox table** for notifications, emails, wishlist matching, and expiry jobs. No message broker.
- Book metadata: **Open Library API** by ISBN, behind an `IBookMetadataProvider` interface, with a fake implementation for tests.
- Files: `IFileStorage` → Azure Blob (prod), Azurite/local disk (dev, tests). Generate thumbnails on upload.
- Hosting target: Azure Container Apps + Azure Database for PostgreSQL Flexible Server (Burstable tier).

### Exchanges: two kinds of request
An `ExchangeRequest` targets one listing and is either:
- **Swap**: requester offers one of their own Active listings, or
- **Credit**: requester spends 1 book credit.

States: `Pending, Accepted, Rejected, Cancelled, Expired, Disputed, Completed`.

```
Pending  → Accepted   (requested-listing owner)
Pending  → Rejected   (requested-listing owner)
Pending  → Cancelled  (requester)
Pending  → Expired    (system, 7 days without a response)
Accepted → Completed  (owner enters the correct handoff code)
Accepted → Cancelled  (either participant)
Accepted → Expired    (system, 14 days after acceptance without completion)
Accepted → Disputed   (either participant, with reason)
Disputed → Completed | Cancelled  (admin only, audited)
Rejected | Cancelled | Expired | Completed → terminal
```

Every illegal transition throws a domain exception and has a test.

Side effects (same transaction):
- Accept: listings involved become `Reserved`; competing Pending requests on those listings are auto-rejected (notify); conversation created; handoff code generated; for Credit requests, 1 credit is **held**.
- Complete: listings become `Exchanged`; held credit is **spent** by requester and **earned** by owner; reputation events emitted.
- Cancel / Expire from `Accepted`: listings return to `Active`; any held credit is **released**. Reject / Cancel / Expire from `Pending`: nothing was reserved or held, so only notify.
- Concurrency tokens on `ExchangeRequest` and `Listing`; simultaneous accepts → one wins, other gets 409.

### Handoff code (replaces "both confirm completion")
- 6-digit code generated on Accept, stored **hashed**, shown only to the requester.
- At the meetup, the owner enters it → exchange `Completed`.
- Max 5 wrong attempts per exchange, then lock and notify both; rate-limited endpoint.

### Book credits
- Never an editable balance. Store `CreditEvent { Id, UserId, Type, Amount, ExchangeRequestId?, Description, CreatedAt }` with types `Starter, Held, Released, Spent, Earned, AdminAdjustment`. Balance = sum of events.
- Available balance can never go negative; enforce in the domain and test concurrent spends.
- One `Starter` credit per user after email confirmation. Credits are earned only on completed handoffs, never on listing.

### Location and privacy
- Listings store a PostGIS point plus a city/area label. Search supports radius (km) and sorts by distance.
- **Never expose exact coordinates publicly**: return distance and an approximate location only (rounded/fuzzed).

### Wishlists (v1, after core exchange flow)
- Users add wanted books by ISBN. When a matching Active listing appears within the user's radius, create a notification via the outbox.

### Design fix
- `Pending` status uses an `accent` **filled badge with `text` color**, never accent-colored text (fails WCAG AA).
- Text on `secondary` fills (secondary CTAs, `Disputed` badge) uses `text`, not `surface`: surface on secondary is 3.26:1 (fails AA), text on secondary is 4.62:1. Verified by `frontend/src/design/tokens.test.ts`. *(approved 2026-10-02)*

### Decisions from docs/PLAN.md §7 (accepted 2026-10-01)
1. `Spent` credit events have Amount **0** (they close the hold); Available = Σ Amount; Held is derived from event counts.
2. Handoff code: HMAC-SHA256 hash (server pepper) for verification + a Data Protection–encrypted copy returned only to the requester; Data Protection keys persisted to Blob.
3. Handoff code lock: requester may regenerate at most 2 times, after that only an admin can unlock.
4. New transition `Pending → Expired` (system, 7 days without a response).
5. No coordinates in any public response (no map in v1): distance from a fixed per-listing jittered point, rounded to 0.5 km, floor "< 1 km", plus `AreaLabel`.
6. Dev email goes over real SMTP to a **Mailpit** container (links visible in the Mailpit UI, never in logs).
7. Accept auto-rejects competing Pending requests where either reserved listing is the requested **or** offered one (reason "no longer available").
8. Reject (from Pending) changes no listings or credits, it only notifies; the state machine itself is unchanged by this.
9. Assertions: **AwesomeAssertions** (Apache-2.0 fork) instead of FluentAssertions.
10. Images: **SixLabors.ImageSharp** for v1 (Split License, eligible now); revisit before commercial launch (fallback SkiaSharp).
11. Production email provider decided in Phase 15; email stays behind `IEmailSender` (SMTP).
12. Listing Q&A and meetup spots move after the MVP (Phases 10 and 12); a few spots seeded in dev.
13. A `Reserved` listing cannot be archived (cancel the exchange first); archiving an `Active` listing auto-rejects its Pending requests.
14. Phase steps use SPEC's superset: Analyze → Plan → Implement → Test → Review → Fix → Verify.
15. Docs folder is lowercase `docs/` (done).
16. Repo lives at `C:\Users\rafae\AQPI` (outside OneDrive, ASCII path); .NET 10 SDK x64 and Docker Desktop installed.
17. Single API replica in v1: Container Apps pinned to min = max = 1; scaling out requires an ADR (Redis / Azure SignalR / distributed rate limiting).

### Decisions from Phase 3 (skeleton)
- Solution file is `BookExchange.slnx` (.NET 10 default). Central package versions in `Directory.Packages.props`; warnings are errors in **all** projects (`Directory.Build.props`).
- Tests run on Microsoft Testing Platform (xunit v3, opted in via `global.json`); no VSTest packages. SDK pinned to any 10.0.x (`rollForward: latestFeature`).
- Integration tests: one PostGIS container per run, a fresh database per test class.
- Outbox: each message is handled in its own transaction (`FOR UPDATE SKIP LOCKED`); handler DB writes commit with the "processed" mark; on failure, rollback to savepoint, backoff 2^n s (max 15 min), dead-letter after 10 attempts.
- Health: `/health` = readiness (DB), `/health/live` = liveness. Dev ports: api 5080, frontend 5173 (proxies `/api`, `/hubs`, `/health`), db 5432, Mailpit 8025/1025, Azurite 10000.
- Frontend linting stays on ESLint (current Vite template ships oxlint).

### Frontend direction (decided 2026-10-01, after reviewing the earlier AQPI Next.js site)
- Visual design stays on SPEC §11.4 tokens; the old AQPI site (navy/gold palette, mascot) is loose inspiration only, nothing copied over.
- UI text is bilingual: **pt-BR and English** via an i18n layer from Phase 4 on (library choice justified then, per R-1). Code, API and docs stay in English. Every user-facing string goes through translations, none hard-coded.
- Default language follows the browser (`navigator.languages`): Portuguese → pt-BR, English → English, anything else → **pt-BR**. The user can switch; the choice is remembered. *(decided 2026-10-02)*
- User-facing app name is **AQPI** (title, manifest, header, emails). Code and solution names stay `BookExchange`. *(decided 2026-10-02; applied in Phase 4)*
- No dark mode in v1 (as SPEC).

### Decisions from Phase 4 (identity)
- Login requires a confirmed email. Wrong password, unknown email and locked account all return the same 401 (`auth.invalid_credentials`); only the right password on an unconfirmed account gets 403 (`auth.email_not_confirmed`). Dummy password hashing keeps timings equal.
- Passwords: 10–128 characters, no composition rules (Identity options mirror the validator exactly). Lockout after 5 failures for 15 minutes.
- Access token: JWT, 15 min, in memory. Refresh token: 14 days, SHA-256 hashed, cookie `aqpi_refresh` (`HttpOnly; Secure; SameSite=Strict; Path=/api/auth`). Change password keeps the current session and revokes the others; reset password revokes all.
- Auth emails go through the outbox with only the user id; the one-time token is generated at send time (never stored). Registering an existing email sends an "account exists" email instead (identical 202).
- Every endpoint requires login unless marked anonymous (fallback policy); unknown routes answer 401 to anonymous callers. ProblemDetails carry a stable `code` the frontend translates.
- Rate limits per client IP: one shared budget for login/register/password/confirmation endpoints (20/min), a separate one for refresh (60/min). Needs forwarded headers behind Azure (Phase 15).
- Application references EF Core packages and works through `IAppDbContext` (Domain stays BCL-only).
- Frontend i18n is a small typed dictionary (no library); plural = singular only for exactly 1.

## Phases (replaces SPEC.md §14; details and exit criteria in docs/PLAN.md §5)
Each backend phase ships its thin frontend slice in the same phase.
1. Inspect ✅
2. Plan ✅
3. Skeleton + infra ✅
4. Identity + Users (+UI) ✅ (pending approval)
5. Books + Listings (+UI)
6. Exchanges + Credits + Handoff (+UI)
7. Messaging (+UI): journey works end to end in the browser at 375 px
8. MVP gate: `FullExchangeJourneyTests` (credit + swap), dev seed data. **MVP done.**
9. Notifications + web push + email fallback
10. Listing Q&A + Reviews + Reputation
11. Wishlists
12. Moderation (incl. meetup spots)
13. Frontend completion + PWA
14. Test completion
15. Docker + CI/CD
16. Security + architecture review
17. Documentation

## Out of scope for v1
Payments, shipping, multi-book bundles, public chat, social login, native mobile apps, three-way swap cycles.

## Working rules
- Follow precedence from SPEC.md: Security > Data integrity > Business rules > Tests > Architecture > Speed.
- Work phase by phase. Each phase: Analyze → Plan → Implement → Test → Review → Fix → Verify, then a Phase Report (≤ 25 lines) and **stop for my approval** before the next phase.
- Ask before: changing the stack, changing the state machine or authorization rules, adding paid Azure services, deleting data or working code.
- Never: commit secrets, skip or disable failing tests, weaken authorization, or claim results you didn't run. Paste real command output.
- Verify package names and versions with real `dotnet add package` / `npm install` output, never from memory.
- Keep this file up to date: when we make a new decision, add it under "Changes vs SPEC.md".

## Commands
<!-- Fill in during Phase 3 -->
- Run everything: `docker compose up`
- Backend tests: `dotnet test`
- Frontend checks: `npm run typecheck && npm run lint && npm test` (in `frontend/`)
