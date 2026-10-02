# Book Exchange — Implementation Plan

Sources: `CLAUDE.md` (overrides) and `docs/SPEC.md` v2. Rule IDs (`R-x`) refer to SPEC.md.

## 1. Phase 1 — Repository inspection

**What exists**

| Item | State |
|---|---|
| `CLAUDE.md` | Project instructions, overrides SPEC |
| `Docs/SPEC.md` | Full spec v2 (folder is `Docs`, spec expects `docs/`) |
| Source code, solution, frontend, tests, Compose, CI | **None** |
| Git | **Not a git repository** |

**Toolchain on this machine (real command output)**

| Tool | Result |
|---|---|
| `dotnet --info` | Host 10.0.12 **x86** at `C:\Program Files (x86)\dotnet`; **"No SDKs were found."** Runtimes: ASP.NET Core 8.0.31 / 10.0.12, NETCore 6.0.5 / 8.0.31 |
| `node -v` / `npm -v` | v22.14.0 / 10.9.2 (OK for Vite) |
| `docker --version` | **not found** |
| `git --version` | 2.47.1.windows.1 |

**Reusable:** nothing code-wise; SPEC.md is complete enough to drive design (domain, rules, auth matrix, indexes, tests, design tokens).

**Missing / blockers before Phase 3**
1. **.NET 10 SDK (x64)**. The x86 host is first on PATH and will shadow an x64 SDK install; PATH order must put `C:\Program Files\dotnet` first.
2. **Docker Desktop** (required for `docker compose up`, PostGIS, Azurite and Testcontainers).
3. **Git repo** (`git init`, `.gitignore`, `.gitattributes`) — needed for CI.
4. **Repo location**: path is inside OneDrive with non-ASCII chars (`Área de Trabalho`). OneDrive syncing `bin/`, `obj/`, `node_modules/` and Docker bind mounts cause locks and slow builds (see Q16).

## 2. Architecture decisions (ADR-style)

| # | Decision | Context / why | Consequences |
|---|---|---|---|
| ADR-01 | **Modular monolith**, 4 projects (Domain, Application, Infrastructure, Api) with module folders (R-4, R-5) | Small team, one deployable, clear boundaries | No network boundaries; module rules enforced by review + an architecture test (project references) |
| ADR-02 | **PostgreSQL + PostGIS** `geography(Point,4326)` via NetTopologySuite; GiST index; `ST_DWithin` + `ST_Distance` | Radius search sorted by distance, no extra service | Testcontainers must use `postgis/postgis` image |
| ADR-03 | **Postgres full-text** (`tsvector` generated column on `Book` from title + authors, `simple` config + `unaccent`), GIN index | Avoid Elastic/Azure Search | Ranking is basic; good enough for v1 |
| ADR-04 | **Transactional outbox** + `BackgroundService` processor; scheduled jobs (expiry, lock cleanup) in same host; no broker | R-3; side effects must not be lost or happen without the commit | Processor polls `FOR UPDATE SKIP LOCKED` (safe if we ever scale out); handlers idempotent by key |
| ADR-05 | **No Redis in v1**; built-in ASP.NET Core rate limiter (partitioned by user/IP, per-exchange for handoff); SignalR in-process | Single instance on Container Apps (min=max=1 replica) | When >1 replica: ADR for Redis/Azure SignalR + distributed rate limiting |
| ADR-06 | **Credits as an append-only ledger** (`CreditEvent`), plus a `CreditAccount` row per user used as the lock + cached balance recomputed in the same transaction (R-13) | No editable balance; double-spend must be impossible | Every credit change locks `CreditAccount` (`SELECT … FOR UPDATE`); test asserts cache == Σ events |
| ADR-07 | **Handoff code** replaces dual confirmation: 6 digits (CSPRNG), verified by **HMAC-SHA256 with server pepper** (a bare SHA of 6 digits is brute-forceable offline); requester-only display (see Q2) | One action at the meetup, proves physical presence | Needs secret management (pepper + Data Protection keys) |
| ADR-08 | **Exchange state machine in the Domain** aggregate; every transition is a method that throws `DomainException` on illegal moves; side effects orchestrated by Application in one `DbContext` transaction | R-11, single place for rules | Each illegal (state, action) pair gets a generated unit test (table-driven) |
| ADR-09 | **Optimistic concurrency** via Postgres `xmin` on `ExchangeRequest` and `Listing`, plus **partial unique indexes** (one Accepted/Disputed request per listing as requested *or* offered) | R-12, belt-and-braces against races | `DbUpdateConcurrencyException`/unique violation → `409` via global handler |
| ADR-10 | **Location privacy**: exact point stored only for owner; public DTOs carry `distanceKm` (0.5 km rounding, floor "< 1 km") + `AreaLabel`, computed from a per-listing **fixed jittered public point** | R-14; prevents trilateration from repeated searches | Distances are approximate by design; DTO tests scan JSON for coordinate fields |
| ADR-11 | **PWA** with `vite-plugin-pwa` (app-shell caching only, never API responses), ZXing for barcode, web push via VAPID | Installable, camera, Safari lacks `BarcodeDetector` | iOS push only when installed; manual ISBN fallback mandatory |
| ADR-12 | **Auth**: ASP.NET Core Identity (`AddIdentityCore`, no cookies UI) + JWT access token in memory + rotating hashed refresh token in `HttpOnly; Secure; SameSite=Strict` cookie, family revocation on reuse (R-18…R-21) | Spec | Frontend and API must share a site (same origin via reverse proxy) for SameSite=Strict |
| ADR-13 | **Book metadata** via `IBookMetadataProvider` → Open Library, with standard resilience handler (timeout, retry); `FakeBookMetadataProvider` in tests | Spec; external API is flaky | Lookup failure → manual entry, never blocks listing |
| ADR-14 | **Files** via `IFileStorage` (Blob/Azurite); images validated by magic bytes, EXIF stripped, thumbnail + display size, GUID names | Spec §6.10 | Image library license — see Q10 |
| ADR-15 | **No MediatR/AutoMapper/repositories** (R-1): Minimal API endpoint groups per module → Application service classes → `AppDbContext` | Explicit, readable | Manual mapping functions per DTO |
| ADR-16 | **Time & IDs**: `IClock` (TimeProvider) everywhere; UUIDv7 via BCL `Guid.CreateVersion7()`; `timestamptz` UTC | R-9, testable expiry | — |

## 3. Dependencies (each verified with real `dotnet add package` / `npm install` output when added — versions are not listed here on purpose, R-2)

**Backend**
| Package | Why |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider for PostgreSQL |
| `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` | Maps PostGIS `geography` points for radius search |
| `Microsoft.EntityFrameworkCore.Design` (+ `dotnet-ef` local tool) | Create and commit migrations |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Password hashing, lockout, email-confirmation/reset tokens |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Validate access tokens (REST + SignalR) |
| `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` | DTO validation (R-23) |
| `Microsoft.AspNetCore.OpenApi` | `/openapi/v1.json` in Development (R-24) |
| `Microsoft.Extensions.Http.Resilience` | Timeouts/retries for Open Library HttpClient |
| `Azure.Storage.Blobs` | `IFileStorage` for Blob (prod) and Azurite (dev) |
| `SixLabors.ImageSharp` (pending Q10) | Format sniffing, EXIF strip, resize — pure managed |
| `MailKit` | SMTP sender (Mailpit in dev, provider in prod — Q6/Q11) |
| `Serilog.AspNetCore` | Structured logging with enrichment |
| `OpenTelemetry.Extensions.Hosting`, `.Instrumentation.AspNetCore`, `.Instrumentation.Http`, `.Exporter.OpenTelemetryProtocol`, `Npgsql.OpenTelemetry` | Traces/metrics for HTTP, HttpClient, DB; OTLP export |
| `Azure.Monitor.OpenTelemetry.AspNetCore` (deploy phase) | Application Insights target in Azure |
| `WebPush` (notifications phase) | VAPID web-push delivery |
| SignalR server, rate limiter, Data Protection, `TimeProvider`, HMAC | Built into the shared framework — no package |

**Backend tests**
| Package | Why |
|---|---|
| `xunit.v3` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` | Test framework |
| `AwesomeAssertions` (or `FluentAssertions` 7.x — Q9) | Readable assertions without the v8 commercial license |
| `Testcontainers.PostgreSql` | Real PostGIS per test class |
| `Respawn` | Fast DB reset between tests |
| `Microsoft.AspNetCore.Mvc.Testing` | In-memory API host (`WebApplicationFactory`) |
| `Microsoft.AspNetCore.SignalR.Client` | Hub authorization/real-time tests |

**Frontend**
| Package | Why |
|---|---|
| `react`, `react-dom`, `typescript`, `vite`, `@vitejs/plugin-react` | Base stack |
| `tailwindcss` + `@tailwindcss/vite` | Styling with design tokens in theme |
| `@tanstack/react-query` | Server state (R-26) |
| `react-router` | Routing |
| `@microsoft/signalr` | Real-time chat and notifications |
| `vite-plugin-pwa` | Manifest + Workbox service worker + offline fallback |
| `@zxing/browser` | Barcode scan (ISBN) and handoff-QR scan on Safari |
| `qrcode` | Render the handoff code as QR |
| `react-hook-form` + `zod` | Forms with client validation mirroring server rules |
| Dev: `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `msw` | Unit/component tests; MSW mocks the API for client/auth-flow tests |
| Dev: `eslint`, `typescript-eslint`, `eslint-plugin-react-hooks`, `prettier` | Quality gates (R-29) |

**Containers (free):** `postgis/postgis`, `mcr.microsoft.com/azure-storage/azurite`, `axllent/mailpit` (dev email, Q6).

## 4. Domain model — Exchanges & Credits

### 4.1 `ExchangeRequest` (aggregate root, `Exchanges` module)

```
ExchangeRequest
  Id                  Guid (v7)
  Kind                ExchangeKind { Swap, Credit }
  Status              ExchangeStatus
  RequestedListingId  Guid
  OwnerId             Guid      // snapshot of requested-listing owner at creation
  RequesterId         Guid
  OfferedListingId    Guid?     // required iff Kind == Swap
  CreatedAt, AcceptedAt?, ExpiresAt? (= AcceptedAt + 14d), ClosedAt?
  ClosedById?         Guid      // null when system
  CloseReason?        string    // reject/cancel/auto-reject/expiry reason
  DisputeReason?, DisputedById?, DisputedAt?
  ResolvedByAdminId?, ResolutionNote?
  Version             uint (xmin)   // concurrency token
  HandoffCode         HandoffCode?  // owned entity, created on Accept
```

Methods (all take `actorId` and `now`, throw `DomainException` on illegal state/actor):
`Create(...)`, `Accept(ownerId)`, `Reject(ownerId)`, `AutoReject(reason)` (system), `Cancel(actorId)`, `Expire()` (system, only if `now ≥ ExpiresAt`), `OpenDispute(actorId, reason)`, `VerifyHandoffCode(ownerId, code, hasher)`, `RegenerateHandoffCode(requesterId)`, `ResolveDispute(adminId, Completed|Cancelled, note)`.
Each raises a domain event (`ExchangeAccepted`, `ExchangeCompleted`, …) that Application turns into side effects + outbox messages **in the same transaction**.

### 4.2 State machine

| From \ Action | Accept | Reject | Cancel | Expire | Dispute | Enter code | Admin resolve |
|---|---|---|---|---|---|---|---|
| **Pending** | → Accepted (owner) | → Rejected (owner / system auto-reject) | → Cancelled (requester) | ✗ (see Q4) | ✗ | ✗ | ✗ |
| **Accepted** | ✗ | ✗ | → Cancelled (either participant) | → Expired (system, 14 d) | → Disputed (either, reason required) | → Completed (owner, correct code, not locked) | ✗ |
| **Disputed** | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | → Completed / Cancelled (admin, audited) |
| **Rejected / Cancelled / Expired / Completed** | ✗ terminal | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |

✗ = `DomainException` (→ 409) and a dedicated test; wrong actor → 404/403 per R-17.

**Creation preconditions** (Application checks + domain guards): requested listing `Active`, not own; Swap → offered listing `Active` and own; Credit → available balance ≥ 1; no open (Pending/Accepted/Disputed) request for the same pair; requester has < 10 Pending.

**Side effects (one transaction)**

| Transition | Listings | Credits (Credit kind) | Other |
|---|---|---|---|
| → Accepted | requested (+ offered) → `Reserved` | lock account; available ≥ 1 else 409; `Held −1` | competing Pending requests on either listing (as requested **or** offered) → `AutoReject` + outbox notify; `Conversation` (2 participants); `HandoffCode` generated |
| → Completed | → `Exchanged` | `Spent 0` (requester, closes hold) + `Earned +1` (owner) | `ReputationEvent(ExchangeCompleted)` ×2 (from Reviews phase; outbox until then); notify |
| → Cancelled / Expired (from Accepted) | → `Active` | `Released +1` | notify; conversation read-only |
| → Rejected / Cancelled (from Pending) | unchanged (never reserved) | none (never held) | notify |
| → Disputed | stay `Reserved` | hold stays | `Report` to admin queue; code entry disabled |
| Disputed → Completed / Cancelled | as Completed / Cancelled above | as above | audit log entry |

### 4.3 `HandoffCode` (owned by ExchangeRequest)

```
HandoffCode
  CodeHash          byte[]   // HMAC-SHA256(pepper, exchangeId || code), constant-time compare
  ProtectedCode     string   // Data Protection–encrypted, decrypted only for requester (pending Q2)
  FailedAttempts    int      // max 5 → LockedAt set, outbox HandoffCodeLocked to both
  LockedAt?         DateTimeOffset
  Regenerations     int      // max 2, requester action (pending Q3)
  CreatedAt
```
Verify endpoint rate-limited per user and per exchange; codes never logged (R-20).

### 4.4 Credits (`Credits` module)

```
CreditEvent   { Id, UserId, Type, Amount, ExchangeRequestId?, Description, CreatedAt, CreatedByAdminId? }
CreditAccount { UserId (PK), Available, Held, Version }   // cache + lock row, recomputed in same tx
```

| Type | Amount | When | Idempotency (unique index) |
|---|---|---|---|
| `Starter` | +1 | email confirmed | `(UserId) WHERE Type=Starter` |
| `Held` | −1 | Credit request accepted | `(ExchangeRequestId, Type)` |
| `Released` | +1 | accepted credit exchange cancelled/expired/dispute-cancelled | `(ExchangeRequestId, Type)` |
| `Spent` | **0** (pending Q1) | completion; finalizes the hold | `(ExchangeRequestId, Type)` |
| `Earned` | +1 (owner) | completion | `(ExchangeRequestId, Type)` |
| `AdminAdjustment` | ±n | admin, description required, audited | — |

- **Available = Σ Amount** (must stay ≥ 0, checked under the account row lock).
- **Held = #Held − #Released − #Spent**; **Total = Available + Held**.
- `ICreditService.Hold/Release/Spend/Earn/GrantStarter/Adjust` is the only writer; no API writes credits except audited admin adjustment.
- Tests: concurrent accepts of two credit requests by a requester with 1 credit → exactly one succeeds; cache equals Σ events after every scenario.

## 5. Revised MVP phase plan (end-to-end journey first)

Change vs SPEC §14: each backend phase ships its **thin frontend slice in the same phase**, so the journey works in the browser at the end of Phase 7 instead of Phase 8; non-journey features (Q&A, meetup spots, reviews, push) move after the MVP.

| # | Phase | Exit criteria |
|---|---|---|
| 1 | Inspect | ✅ this document §1 |
| 2 | Plan | ✅ this document |
| 3 | Skeleton + infra | Prereqs installed (SDK x64, Docker), `git init`; solution + 2 test projects build; Vite PWA shell with design tokens + contrast test; Compose: api, frontend, PostGIS, Azurite, Mailpit; `/health` green; Testcontainers smoke test; GitHub Actions PR build+test; outbox table + processor skeleton; global ProblemDetails handler |
| 4 | Identity + Users (+UI) | §8 endpoints, refresh rotation/family revocation, rate limits, confirm email via Mailpit → Starter credit; home area; Register/Login/Confirm/Profile pages; centralized API client (R-25) |
| 5 | Books + Listings (+UI) | ISBN lookup (Open Library + fake), manual fallback, CRUD, images (magic bytes/EXIF/thumbnail), radius + full-text search, privacy DTO tests; Create Listing scan flow, Search near me, Listing details, My Listings. **Test ZXing on a real iPhone + Android here.** |
| 6 | Exchanges + Credits + Handoff (+UI) | Full state machine incl. dispute open, credit ledger, handoff code (verify/lock/regenerate), expiry job, concurrency 409s, auto-reject; in-app `Notification` rows via outbox (no push yet); Request form (swap/credit), My Exchanges, Exchange details (code + QR for requester, code entry/scan for owner), Credits page |
| 7 | Messaging (+UI) | Hub + REST cursor history + read receipts/unread; membership re-validated (R-15); chat UI. **Journey works end to end in the browser at 375 px.** |
| 8 | MVP gate | `FullExchangeJourneyTests` (credit + swap, API-level), manual phone-viewport run of the journey, dev seed data (MVP subset), bug-fix buffer. **MVP done.** |
| 9 | Notifications + web push + email fallback | All `NotificationType` triggers, SignalR live badge, push subscriptions + opt-in after first accepted exchange, iOS install prompt |
| 10 | Listing Q&A + Reviews + Reputation | Public Q&A; reviews only after Completed; reputation ledger; public profile |
| 11 | Wishlists | CRUD + matching job within radius, once per user per listing |
| 12 | Moderation | Reports, admin dispute resolution, penalties, credit adjustments (audited), meetup spots (curated) + suggestions in chat |
| 13 | Frontend completion + PWA | All §11.2 pages, R-27…R-29, offline fallback, installability |
| 14 | Test completion | Every §12 row + every §7 negative test; full DoD journey incl. Q&A and reviews |
| 15 | Docker + CI/CD | Multi-stage non-root images, `.env.example`, main workflow with gated Azure deploy, migrations as explicit step |
| 16 | Security + architecture review | §10.2 item by item with evidence |
| 17 | Documentation | README, Mermaid diagrams, final audit table |

Every phase: Analyze → Plan → Implement → Test → Review → Fix → Verify → Phase Report (≤ 25 lines) → **stop for approval**.

## 6. Top 5 risks

| # | Risk | Mitigation |
|---|---|---|
| 1 | **Liquidity**: too few matching books nearby; empty marketplace | Starter credit + credit requests break double coincidence; 30-s scan listing; launch in one campus; seed real supply with early users; wishlists right after MVP; track "time from listing to first request" from day one |
| 2 | **Data-integrity races** (double accept, credit double-spend, duplicate events from outbox retries) | Ledger + `CreditAccount` row lock; `xmin` tokens; partial unique indexes; idempotency unique keys on credit events and outbox; concurrency tests against real PostGIS (not in-memory) |
| 3 | **Location/privacy leaks** (trilateration, EXIF GPS, coordinates in DTOs/logs) | Jittered public point + 0.5 km rounding + "< 1 km" floor; EXIF strip test with a GPS-tagged fixture; JSON-contract tests asserting no lat/lon fields; Serilog destructuring policy |
| 4 | **Environment blockers**: no .NET SDK (x86 host shadows PATH), no Docker, repo inside OneDrive with non-ASCII path | Fix before Phase 3 (Q16); Testcontainers needs Docker so integration tests are impossible without it; CI on GitHub Actions as the reference environment |
| 5 | **PWA limits on iOS**: barcode scanning reliability, push only for installed PWAs, stale service-worker caches | Manual ISBN entry always available; real-device test in Phase 5; email fallback for key notifications; SW caches app shell only, `autoUpdate` with prompt; handoff code also typeable (QR optional) |

## 7. Contradictions and gaps — questions with recommended answers (✅ all resolved 2026-10-01: recommendations accepted as written, no exceptions)

1. **Spent amount.** SPEC has `Held −1` and `Released +1`, and "balance = sum of events". If `Spent` were −1 the requester would pay twice. *Recommend:* `Spent` has Amount **0** (marker that finalizes the hold); Available = Σ Amount; Held derived from event counts. **✅ Resolved: accepted (2026-10-01).**
2. **Handoff code "stored hashed" vs "shown to requester".** The owner triggers Accept, so the requester must be able to view the code later; a hash can't be shown. *Recommend:* store an HMAC hash for verification **plus** a Data Protection–encrypted copy returned only by a requester-authorized endpoint; Data Protection keys persisted to Blob storage. **✅ Resolved: accepted (2026-10-01).**
3. **Code regeneration.** CLAUDE.md says "lock and notify both" but is silent on unlocking; SPEC allows requester regeneration (max 2) or admin. *Recommend:* keep SPEC's rule (2 regenerations, then admin only). **✅ Resolved: accepted (2026-10-01).**
4. **Pending requests never expire.** Stale Pending requests count toward the 10-request cap and hide demand. *Recommend:* add `Pending → Expired` (system, 7 days without response). This changes the state machine, so it needs your approval; otherwise leave as-is. **✅ Resolved: accepted (2026-10-01).**
5. **"Approximate location (rounded/fuzzed)" (CLAUDE.md) vs "distanceKm + AreaLabel only" (SPEC R-14).** *Recommend:* expose **no coordinates at all** in v1 (no map); distance from a per-listing jittered point, rounded to 0.5 km, floor "< 1 km". **✅ Resolved: accepted (2026-10-01).**
6. **Email confirmation in dev vs R-20 (dev sender only logs that an email was sent).** Without a link nobody can confirm, so no Starter credit. *Recommend:* real SMTP to a **Mailpit** container in dev (link visible in Mailpit UI, never in logs). **✅ Resolved: accepted (2026-10-01).**
7. **Auto-reject scope on Accept.** SPEC says "competing Pending requests on those listings". *Recommend:* include requests where either reserved listing is the **requested or offered** one; system `Rejected` with reason "no longer available". **✅ Resolved: accepted (2026-10-01).**
8. **CLAUDE.md "Reject: listings return to Active; held credit released".** Reject only happens from Pending, where nothing is reserved or held. *Recommend:* treat it as a no-op for listings/credits (only notify); no state-machine change. **✅ Resolved: accepted (2026-10-01).**
9. **FluentAssertions licensing.** v8+ is commercial for non-OSS use. *Recommend:* **AwesomeAssertions** (Apache-2.0 fork, same API) — a small stack change, hence the question. (Alternative: pin FluentAssertions 7.x.) **✅ Resolved: accepted (2026-10-01).**
10. **ImageSharp license** (Six Labors Split License: free for open source or under $1M revenue). *Recommend:* use ImageSharp now (eligible as a college project); revisit before commercial launch (fallback: SkiaSharp, MIT). **✅ Resolved: accepted (2026-10-01).**
11. **Production email provider** (needed for confirm/reset + fallback notifications). Azure Communication Services Email is a paid (pay-per-email) service. *Recommend:* decide in Phase 15; keep SMTP behind `IEmailSender` so ACS or any SMTP provider plugs in. **✅ Resolved: accepted (2026-10-01).**
12. **Listing Q&A and meetup spots** are in the DoD journey / SPEC Phase 7 but not in your MVP journey. *Recommend:* move both after the MVP (Phases 10 and 12); seed a few spots in dev. **✅ Resolved: accepted (2026-10-01).**
13. **Archive/edit while Reserved or with Pending requests.** Not specified. *Recommend:* owner can't archive a `Reserved` listing (cancel the exchange first); archiving an `Active` listing auto-rejects its Pending requests. **✅ Resolved: accepted (2026-10-01).**
14. **Phase steps.** CLAUDE.md: Analyze→Plan→Implement→Test→Verify; SPEC adds Review→Fix. *Recommend:* use SPEC's superset. **✅ Resolved: accepted (2026-10-01).**
15. **`Docs/` vs `docs/`.** Linux CI and Docker are case-sensitive. *Recommend:* rename `Docs` → `docs` in Phase 3 (rename only, no content change). **✅ Resolved: accepted (2026-10-01).**
16. **Environment.** *Recommend:* before Phase 3 you install the .NET 10 SDK x64 (and put `C:\Program Files\dotnet` before the x86 path), install Docker Desktop, and move the repo outside OneDrive to an ASCII path (e.g. `C:\dev\book-exchange`); I then run `git init`. **✅ Resolved: accepted (2026-10-01).**
17. **Single replica.** No Redis implies one API instance. *Recommend:* pin Container Apps to min = max = 1 replica in v1 and record the scale-out trigger as an ADR. **✅ Resolved: accepted (2026-10-01).**

Accepted decisions are recorded under "Changes vs SPEC.md" in `CLAUDE.md`.

## 8. Update 2026-10-02: AQPI-v2 merge

The owner's earlier AQPI-v2 brief (token-based exchange through a shared collection, 7 categories, reader
reviews, landing page, Duda's brand) was merged into this plan. Summary of what changed (details and reasons in
`CLAUDE.md`, "Decisions from the AQPI-v2 merge"):

- New **Phase 5b — Brand + catalogue**: AQPI brand, landing page, 6 categories + "Bom para começar" tag, starter
  ficha moved to the first listing with a photo.
- New **Phase 6b — AQPI collection**: a small local central collection modelled as an operator account; donations
  accepted by an admin mint +1 ficha; collection books are picked with a normal Credit request and handed over by
  pickup or hand delivery (no shipping integration).
- **Phase 10** also covers book reviews (reader reviews with a page per book).
