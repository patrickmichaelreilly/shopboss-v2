# Smartsheet Integration PRD (condensed)

## Inputs Needed
- Template sheet ID: 2455059368464260
- Target workspace ID: 6590163225732996

## Executive Summary
Enable reliable bi‑directional sync between ShopBoss Projects and Smartsheet with secure auth, stable row mapping, batching, and conflict-safe updates.

## Phase 0: Fix Import OAuth (0.5d)
- Bug: Import modal shows “Authentication cancelled” after successful login.
- Fix: In `smartsheet.js:triggerSmartSheetAuth()`
  - Resolve/reject only on `postMessage` from popup; do not reject on manual close.
  - Validate `event.source === popup` and `event.origin` whitelist.
  - After popup closes, poll `/smartsheet/auth/status` (≤10s) to confirm session.

## Phase 1: OAuth Attribution + UI (1d)
- Attribution: On create (comments, attachments, PO/WO, TaskBlock), use Smartsheet `ss_user_id` and email from session; store both.
- Behavior: Block authoring without Smartsheet session and prompt to connect.
- UI: Project header shows Connect/Disconnect + connected user email; no auto-redirects.

## Phase 2: Smartsheet Writes (2d)
- Service: Add CreateSheet, AddRows, UpdateRows, AddComment, AttachFile.
- Mapping: Use column IDs (not titles); no hidden columns in MVP; rely on DB `row_mappings`.
- Batching: Batch rows (200–400/request). Exponential backoff on 429/5xx with jitter.
- Attachments: Stream upload; retry small files; surface per-item status.
- Observability: Set a change-agent header/marker for loop avoidance and audit.

## Phase 3: Templates & Formatting (2d)
- Columns: Project#, Name, Status, Dates, Amounts, Notes (as in provided template).
- Entities → rows: Comment, Attachment, PO, WO, Custom WO; TaskBlock as parent rows.
- Formatting: Minimal API-supported styles; hierarchy via parent/child rows.

## Phase 4: Manual Sync UX (1d)
- Timeline UI: Two buttons — `Sync to Smartsheet`, `Sync from Smartsheet` (with progress + summary).
- Outbound: Diff latest ShopBoss events → batched Add/Update rows; update `row_mappings` with `rowId`,`rowVersion`.
- Inbound: Read sheet (since last sync) → map via `row_mappings`; unknown rows summarized as Unmapped (ignored or manual action).
- Conflicts: Last‑write‑wins; low user count → accept rare collisions.
- No webhooks in MVP (future phase).

## Architecture & Data Model
- No hidden columns in MVP; rely on Smartsheet `rowId` and DB mapping.
- Tables: `smartsheet_accounts`, `project_sheet_links`, `row_mappings (sheetId,rowId,entityId,rowVersion)`, `sync_log`.
- Sheet provisioning: If unlinked, copy provided template to target workspace; persist link.

## Security
- OAuth: PKCE + CSRF `state`, short‑lived tokens with refresh; rotate on login; encrypt at rest.
- postMessage: Strict origin + `event.source` checks; handle popup blockers.
- Scopes: Minimum necessary Smartsheet scopes.

## Acceptance Criteria
- OAuth: Import modal updates within 2s; no false “cancelled”.
- Writes: Batch adds/updates <1% retries; p95 <2s.
- Manual sync: Two buttons visible; each completes with clear summary; 0 duplicates.
- Provisioning: If no link, one‑click create/attach sheet with correct schema.

## Timeline
- Total: 6.5 days
- P0: 0.5d; P1: 1d; P2: 2d; P3: 1.5d; P4: 1.5d

## Non‑Goals (MVP)
- Webhooks/real‑time sync, HMAC validation, and conflict UI workflows.
