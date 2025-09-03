Issues (for discussion)

1) Column targeting (prescription)
- Resolve column IDs by Title per target sheet: on first use of a sheet, GET its columns, build a Title→Id map, and cache it in memory keyed by sheetId; use this map for all writes/updates. On 4xx invalid-column errors, refresh the map and retry once. Do not hardcode template column IDs.

2) Persist linked sheet per project (prescription)
- Enforce 1:1 mapping: if `Project.SmartSheetId` is null, copy the template (2455059368464260) into workspace 6590163225732996, name it `ShopBoss - {ProjectName}`, store the new sheetId on the project, and reuse it for all subsequent syncs. Never write back to any source sheet used during migration.

3) Row mapping and badges (prescription)
- Model: Add `RowId` (long?) on ProjectEvent to store Smartsheet row ID. Keep `RowNumber` (int?) solely for UI display.
- Outbound sync: Use `RowId` for updates; when creating new rows, set `RowId` from the API result. After batch writes, fetch the sheet once and build an `id→rowNumber` map; update `RowNumber` for affected events and save.
- Inbound/refresh: On “Sync from Smartsheet” or timeline load, fetch rows and refresh `RowNumber` for all events with a `RowId` so badges stay accurate after moves/reorders.
- UI: Badges render `RowNumber` if present; if null, omit the badge.

4) Hierarchy and indentation (prescription)
- Parent rows: Each TaskBlock corresponds to a parent Smartsheet row; all its events are children (indentation under that row).
- Storage: Add `SmartsheetRowId` (long?) to TaskBlock to store the parent row ID (simple and fast; DB recreated so no migration concerns).
- Outbound: Ensure/create parent row for each TaskBlock; update its title when the block name changes. For child events, set `parentId` to the TaskBlock’s `SmartsheetRowId` and batch create/update.
- Inbound/refresh: Read sheet, rebuild `rowId → parentId` map, and verify TaskBlock parent rows still exist; optionally refresh event `RowNumber` as in item 3.

5) Manual inbound sync (from Smartsheet)
- Add second button and endpoint: read rows, match by RowId, update known events; summarize Unmapped rows (ignore in MVP).

6) Batching and limits
- Batch add/update rows (e.g., 200–400/request); add basic retry with backoff for 429/5xx.

7) Token refresh on sync
- If expired, attempt refresh once then proceed; otherwise surface re‑auth message.

8) Naming consistency
- Normalize user-facing text to “Smartsheet” (Phase 2 reintroduced “SmartSheet” in places).

9) Remove unused Project Details view (prescription)
- Delete `Views/Project/Details.cshtml` and remove the unused `ProjectController.Details(string id)` action. Verify no references to the route exist (links, JS). Timeline remains rendered via partials on the index.

10) Service consolidation
- Reuse SmartSheetService for auth/client setup (and future refresh), keep SyncService focused on mapping + payloads.

11) Config
- Use TemplateSheetId=2455059368464260 and WorkspaceId=6590163225732996 from config; no hardcoded defaults in code.
