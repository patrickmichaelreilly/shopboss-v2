Issues

- Column IDs hardcoded: 
    \\ User Comment: Do we need to ever have the Colum IDs or can we always just address columns by Name? That would be fine by me.

- Sheet not persisted: GetOrCreateProjectSheetAsync always creates a new sheet (TODO comment). This will create a new sheet on every sync.
    - Fix: Store created sheet ID on Project.SmartSheetId and reuse. Respect provided workspace ID.

- Row mapping field misuse: ProjectEvent.RowNumber is now long? holding Smartsheet row IDs, but the rest of the codebase (imports, grouping, timeline badges) treats it as a row number (display order).
    - Fix: Introduce a distinct RowId (long) for Smartsheet row IDs. Keep RowNumber (int?) only if you still display “Row N” from imports. Otherwise, rename for clarity and update usages.

- Hierarchy/indentation ignored: No parent rows for TaskBlocks, no parentId on child rows. This breaks the nesting requirement.
    - Fix: Ensure parent TaskBlock rows exist; set parentId on event rows. Persist parent row IDs (e.g., on TaskBlock or a mapping table).


- Batching
- Inbound path missing: Commit title says “bi-directional” but there’s no “Sync from Smartsheet”. MVP calls for two manual buttons (to/from) at the timeline.
    - Add minimal “from” sync: Read sheet, diff by rowId (and optionally row version), map known rows to events, summarize unknown rows as Unmapped.
- Token refresh: Controller returns “expired” without trying refresh. Optionally call your refresh endpoint, then proceed, to reduce friction.

- Naming: New strings use “SmartSheet”. Standardize to “Smartsheet” (we already normalized elsewhere).

- Changing ProjectEvent.RowNumber type to long? requires an EF migration. No migration included.
    \\ User comment: No migration is required, there is no data, we will create the DB from scratch. Never do a migration. Ever.

- Duplication: This service hand-rolls HTTP to Smartsheet while SmartSheetService uses the SDK and session handling. Consider centralizing auth/token refresh and client setup to SmartSheetService to avoid drift.

- Config keys: Uses SmartSheet:TemplateSheetId and SmartSheet:WorkspaceId. Good. Ensure values are set; defaults match what you provided.