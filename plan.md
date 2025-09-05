# ShopBoss v2: Modern Inline Editing System with Direct File Upload

## Vision
Transform the entire UI to support seamless inline editing where:
- Click any field to edit it in-place
- No layout shifts or mode switching  
- Direct file upload without modal
- Consistent experience across all editable content

## Inline Attribute Editing
- Hover: Subtle highlight + edit icon
- Click: Convert to inline input with same dimensions
- Enter/blur: Save via AJAX
- Escape: Cancel edit
- Editing: Blue border, white background
- Saving: Brief spinner, then green checkmark
- Error: Red border with tooltip
- Save with per-field PATCH; optimistic UI with spinner then checkmark

## Keyboard Navigation
- **Tab**: Move between editable fields
- **Enter**: Save current field
- **Escape**: Cancel current edit

# Phase 1: Attachment System ✅
1. Add `Label` field to `ProjectAttachment` model
2. Remove `Category` field from `ProjectAttachment` model (no migration; drop and discard)
3. Simplify upload controller to handle direct file upload
4. Set `Label` default to "Label" on upload; allow inline editing afterward
5. Remove upload modal from _ProjectDetails.cshtml
6. Update timeline-files.js for direct upload
7. Implement inline label editing in _TimelineEvent.cshtml
8. Add API endpoint for updating attachment label
9. Update Attachment event appearance per sketch: line 1 = icon + Label + delete; line 2 = filename.ext + size; line 3 = timestamp + author

# Phase 2: Project Information Card ✅
1. Remove Edit/Save/Cancel buttons
2. Add `.inline-editable` class to all fields
3. Implement field-specific editors (text, date, select)
4. Create unified save API that updates individual fields

# Phase 3: Universal Inline Editing & UI Polish

## Visual Hierarchy System
Establish consistent styling based on position in event, not content type:
- **Primary line**: Main content (black, bold) - attachment label, work order name, comment first line, task block name
- **Secondary line**: Supporting details (muted) - file info, PO details, descriptions
- **Tertiary line**: Metadata (small, muted) - timestamps, authors

## Phase 3C: Bug Fixes (Priority 1)
**Critical issues affecting current functionality**
1. **Fix TaskBlock association for Custom Work Orders** - Ensure custom work orders created in TaskBlocks stay nested (not root level)
2. **Fix TaskBlock association for Work Orders** - Same issue for regular work orders attached to TaskBlocks
3. **Live nesting updates** - Update TaskBlock colors/indentation without page refresh when nesting changes
4. **Notes field overflow** - Constrain textarea resize within Project Information card boundaries
5. **Custom Work Order icon consistency** - Use clipboard icon everywhere (timeline & create button)

## Phase 3B: Projects Index Table Editing (Priority 2)
**Extend inline editing to the projects table view**
1. **Project Name** - Direct editing in table cell
2. **Project ID** - Inline editable with uniqueness validation
3. **Install Date** - Date picker in table cell
4. **Consistent behavior** - Same keyboard shortcuts as other inline fields (Enter save, Escape cancel, Tab navigate)

## Phase 3A: Timeline Inline Editing (Priority 3) ✅
**Core inline editing for all timeline elements**
1. **Comment descriptions** - Primary and secondary lines should be editable ✅
2. **Task block names** - Click to edit task block titles ✅
3. **Purchase order fields** - Primary and secondary lines should be editable ✅
4. **Custom work order fields** - Primary and secondary lines should be editable ✅
5. **Work order details** - No fields should be editable (as specified)
6. **Alt+Enter support** - Allow line breaks in multiline text fields ✅

## Phase 3D: Visual Polish & Consistency (Priority 4)
**Implement visual hierarchy system across all event types**
1. **Primary line styling** - Black text for all first lines across event types
2. **Attachment link hover** - Clear visual feedback for downloadable files
3. **Smartsheet field redesign** - Remove Unlink button, show "Not Synced" or hyperlink to sheet

## Phase 4:
1. Still getting toast message for successful deletion of Custom Work Order from timeline. Remove. Also getting one for creation, remove as well.
1.1 Still getting toast message for successful association / removal of Work Order from timeline, remove.
2. In the Task Block Header collabsible tools pane, remove the Edit button since it is deprecated. Be sure to clean up all code associated.
3. In the Task Block Header collabsible tools pane, move the trash button to the left, followed by the spacer, followed by the rest of the buttons. To prevent acccidental clicks.
4. I get a double confirmation box for deleting a Custom Work Order from the timeline, should just be one.
