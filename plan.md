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

 # Phase 1: Attachment System
 1. Add `Label` field to `ProjectAttachment` model
 2. Remove `Category` field from `ProjectAttachment` model (no migration; drop and discard)
 3. Simplify upload controller to handle direct file upload
 4. Set `Label` default to "Label" on upload; allow inline editing afterward
 5. Remove upload modal from _ProjectDetails.cshtml
 6. Update timeline-files.js for direct upload
 7. Implement inline label editing in _TimelineEvent.cshtml
 8. Add API endpoint for updating attachment label
 9. Update Attachment event appearance per sketch: line 1 = icon + Label + delete; line 2 = filename.ext + size; line 3 = timestamp + author
 
 # Phase 2: Project Information Card  
 1. Remove Edit/Save/Cancel buttons
 2. Add `.inline-editable` class to all fields
 3. Implement field-specific editors (text, date, select)
 4. Create unified save API that updates individual fields
 
 # Phase 3: Extend to All Areas
 1. Timeline event descriptions
 2. Task block names  
 3. Work order details
 4. Projects Index: Make `Project Name`, `Project ID`, and `Install Date` inline editable in the index table
