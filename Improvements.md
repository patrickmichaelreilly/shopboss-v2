# Work Order List (Admin View)
    - Remove the star icon from before the Name. Create dedicated column for star icon. Remove Work Order ID column. Remove Green Star from Action Buttons.
    - Remove Microvellum branding from page footer
    - Add ability to group multiple related work orders (project phases)
    - Add capablity of storing Shop Drawings in DB attached to Work Orders

# Modify Work Order
    - Not all scan events in audit log.
    - Add capability to merge additional SDF data into existing Work Order. Handle part re-cuts, supplementary Nest Sheets, etc.

# Import Process

# CNC Station
    - Improve Nest Sheet detail modal to include Nest Sheet image.
    - Add label printing capabilities to Nest Sheet modal.
    - Verify live status updates when Parts are modified at other stations.
    - Group and sort Nests by material
    - Add button to un-ProcessNestSheet in Nest Sheet modal

# Sorting Station
    - Getting a "Ready for Assembly" alert twice for Product with filtered parts (doors/drawer fronts). Once after Carcass again after Door. Just need 1 when Carcass is ready.
    - Style billboard area
    - Kill toast messages
    - Increase size of grid/rack display. Remove column and row labels.
    - Improve bin indication colors and visuals - Grey for empty, Yellow for partial, Red for indicating immediate location (matching billboard text), Green for ready to Assemble
    - Add Empty Bin and Empty Rack buttons even when it thinks it's already empty.
    - Add interface for configuring filtering rules (instead of hardcoding) for Doors, Drawer Fronts, etc. Should be extensible for arbitrary rules.
    - Show bins occupied by other work orders as Blocked

# Universal Scanner Partial
    - Remove help button
    - Add scan even to audit trail
    - Transform to all caps

# Assembly Station
    - Improve list appearance. Decrease vertical size of each element to reduce length of list. Move completed items to end of list.
    - Remove Sorting Rack statistics box on righthand side of page COMPLETELY.
    - Combine Hardware items and give totals.
    - Improve list appearance. Decrease vertical size of each element to reduce length of list. Move completed items to end of list.

# Shipping Station
    - Recreate look of a packing list.
    - Packing list print capability.
    - Products not correctly showing "Shipped"

# Rack Configuration view
    - Deleting a rack must deal with parts if it is not empty. Need cascade rules.

# Collaboration and Development Process
    - Stay on top of CLAUDE.md
    - Compress Worklog.md and improve logging hygene to prevent bloat.
    - Utilize data checkpoints
    - Utilize built-in backup and restore
    - Need to slice and refactor bloated Admin Controller
    - Need to create matrix of all possible Scans and corresponding status updates and error validation.
    - Utilize MCP servers.
        - "Consult with PM"
        - Gemini as tool
    - Hooks
    