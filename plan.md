# Project Module UI/UX Improvement Plan

**Hierarchical Action Architecture**: Timeline manages blocks, blocks manage components. 

**Proven Recursive Architecture**: Current nested TaskBlock implementation works excellently with smooth drag-and-drop. Keep the self-referencing model and recursive rendering - just enhance the authoring UI.

---

## Phase 1: Code Organization **COMPLETED**
## Phase 2: Timeline UI Restructure **COMPLETED**
## Phase 3: Block Management Enhancement **COMPLETED**

## Phase 4: Event Display Redesign

### Event Layout Changes

**Attachment:**
```
📎 [Row 2] Benchmark Permit Plans 3.28.25.pdf (47.60 MB)    [X]
    The text content of the entity would go here.
    2025-04-07 20:11 by steven@eurotexmfg.com
```

**Comment:**
```
💬 [Row 5] Project kickoff meeting notes                     [X]
    Discussed timeline, deliverables, and budget concerns.
    Need to follow up with vendor on materials.
    2025-04-08 14:30 by project.manager@company.com
```

**Purchase Order:**
```
🛒 PO #12345 - Acme Supply Co. ($4,500.00)                  [X]
    Hardware and fasteners for assembly
    Expected delivery: 2025-04-15
    2025-04-07 09:15 by purchasing@company.com
```

**Work Order:**
```
⚙️ WO #5678 - CNC Cutting Operation                         [X]
    Cut panels per specification sheet
    Priority: High | Due: 2025-04-10
    2025-04-06 11:00 by shop.floor@company.com
```

**Custom Work Order:**
```
✏️ Installation Phase 1                                      [X]
    Site preparation and foundation work
    Assigned to: Field Team A
    2025-04-05 08:00 by field.supervisor@company.com
```

- Remove text badges (attachment, comment, etc.) - use icons only
- Move event type icon to leftmost position 
- Place primary content (filename/title) on same line as icon
- Indent all secondary content (descriptions, dates, metadata)
- Add consistent delete button on every event
- Event icons double as drag handles (remove separate drag handle)
- TaskBlock cube icons also act as drag handles (consistency)

### Delete Functionality Requirements
- Every event must have a delete button
- Deletes must cascade properly (e.g., attachment deletion removes both event AND file)
- Confirm before destructive actions
- Handle all event types: attachments, comments, POs, WOs, custom WOs
- Clean up any orphaned relationships

### Visual Hierarchy
- Icon + Row badge + Primary content on top line
- Secondary details indented below
- Consistent spacing and alignment
- Clear visual separation between events
- Compact but readable layout


### **Real-World Validation**
Your sketch shows exactly what we're building toward. sketch.jpg

---

## Phase 4 Implementation Details (Actionable)

**Target Files**
- Views: `src/ShopBoss.Web/Views/Shared/_Timeline.cshtml`, `src/ShopBoss.Web/Views/Shared/_TaskBlockRecursive.cshtml`
- JS: `src/ShopBoss.Web/wwwroot/js/timeline.js`, `timeline-files.js`, `timeline-purchases.js`, `timeline-workorders.js`
- CSS: `wwwroot/css/site.css` (move inline timeline styles here)

**Drag Handles**
- Event handle: use `.event-icon` (left icon) as the Sortable handle; remove `.event-drag-handle`.
- Block handle: use `.block-icon` (cube) in TaskBlock headers as handle.
- JS: update Sortable config to `handle: '.event-icon, .block-icon'` in `timeline.js`.

**Delete Actions**
- Use a single delegated action: `data-action="delete-event"`.
- Required attributes by type:
  - Attachment: `data-event-id`, `data-attachment-id`
  - Purchase Order: `data-event-id`, `data-po-id`
  - Work Order (association): `data-event-id`, `data-wo-id`
  - Custom Work Order: `data-event-id`, `data-cwo-id`
  - Comment: `data-event-id`
- Router in `timeline.js` dispatches by presence of these data attributes to:
  - Attachments → `Timeline.Files.deleteFile(attachmentId, projectId)`
  - Purchase Orders → `Timeline.Purchases.deletePurchaseOrder(poId, projectId)`
  - Work Orders → `Timeline.WorkOrders.detachWorkOrder(woId, projectId)`
  - Custom Work Orders → `Timeline.WorkOrders.deleteCustomWorkOrder(cwoId, projectId)`
  - Comments → `deleteComment(eventId)` (new helper)

**Comment Delete Endpoint**
- Add `POST /Project/DeleteComment` (or `DeleteEvent`) to `ProjectController`:
  - Input: `{ eventId: string }`
  - Effect: deletes the comment event; no orphaned references
  - Return: `{ success: bool, message?: string }`
- Add service method to remove the event safely and persist.
- Client: `apiPostJson('/Project/DeleteComment', { eventId })` with confirm.

**Row Badge Policy**
- Show `Row N` only when available (e.g., attachments with `RowNumber`).
- Do not synthesize row numbers for POs/WO/Comments; omit badge when not present.

**CSS Consolidation**
- Move event and block styles from `_Timeline.cshtml` into `site.css` under a `/* Timeline */` section.
- Add utility classes:
  - `.event-icon { cursor: grab; margin-right: 6px; }`
  - `.block-icon { cursor: grab; }`
  - `.event-primary { display: flex; align-items: center; }`
  - `.event-secondary { margin-left: 22px; }`  // aligns under primary text
  - `.row-badge { margin-left: 6px; }`

**Action Checklist**
- Update markup in `_Timeline.cshtml` and `_TaskBlockRecursive.cshtml`:
  - Icon-first line: icon `.event-icon` + optional row badge + primary text + delete button (`data-action="delete-event"`).
  - Secondary text indented below with `.event-secondary`.
  - Replace separate drag handles with the icon itself.
- Update `timeline.js`:
  - Sortable `handle: '.event-icon, .block-icon'` and keep mixed ordering.
  - Add delegated `delete-event` handler; read data-ids; dispatch to the correct module/API.
  - Add `deleteComment(eventId)` helper using `apiPostJson`.
- Implement `ProjectController.DeleteComment` and service method.
- Move styles to `site.css`, remove inline `<style>` block from `_Timeline.cshtml`.

**Acceptance Criteria**
- Dragging by icon works for all events (root and in blocks) and for TaskBlocks.
- Every event shows a delete button that confirms and deletes the correct entity (attachments delete file + event; WOs detach).
- Comments can be deleted via API; no orphaned relationships remain.
- Event first line: icon + primary text; secondary details indented; badges only when data exists.
- No inline JS; all actions use `data-action` delegation.
