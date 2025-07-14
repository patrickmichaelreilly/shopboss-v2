# Work Order List (Admin View)
    - Remove the star icon from before the Name. Create dedicated column for star icon. Remove Work Order ID column. Remove Green Star from Action Buttons.
    - Remove Microvellum branding from page footer
    - Add ability to group multiple related work orders (project phases)

# Work Order Detail (Modify Work Order)
    - Finish new Modify Work Order interface. Use existing TreeViewApi.
    - Implement undo capability built on existing Audit Trail infrastructure. Add Audit Log Display capability.
    - Integrate new Modify partial, replacing old one. Remove old one.
    - Add capability to merge additional SDF data into existing Work Order. Handle part re-cuts, supplementary Nest Sheets, etc.

# Import Process
    - Handle repeat Work Order Names and Work Order IDs

# CNC Station
    - Improve Nest Sheet detail modal to include Nest Sheet image.
    - Add label printing capabilities.
    - Verify live status updates when Parts are modified at other stations.

# Sorting Station
    - Getting a "Ready for Assembly" alert twice for Product with filtered parts (doors/drawer fronts). Once after Carcass again after Door. Just need 1 when Carcass is ready.
    - Cut Parts Modal - manual "Sort" buttons don't work
    - Need to add billboard area
    - Increase size of grid/rack display. Remove column and row labels. 
    - Improve bin indication colors and visuals - Grey for empty, Yellow for partial, Red for indicating immediate location (matching billboard text), Green for ready to Assemble
    - Add Empty Bin and Empty Rack buttons even when it thinks it's already empty.
    - Add interface for configuring filtering rules (instead of hardcoding) for Doors, Drawer Fronts, etc. Should be extensible for arbitrary rules.

# Universal Scanner Partial
    - Remove help button

# Assembly Station
    - Improve list appearance. Decrease vertical size of each element to reduce length of list. Move completed items to end of list.
    - Remove Sorting Rack statistics box on righthand side of page COMPLETELY.
    - Combine Hardware items and give totals.

# Shipping Station
    - Combine Hardware items and give totals. Manually clicking Shipped on a "bundle" of like Hardware components marks them all as Shipped.

# Rack Configuration view
    - Deleting a rack must deal with parts if it is not empty. Need cascade rulese.

# Collaboration and Development Process
    - Stay on top of CLAUDE.md
    - Compress Worklog.md and improve logging hygene to prevent bloat.
    - Utilize data checkpoints
    - Utilize built-in backup and restore
    - Need to slice and refactor bloated Admin Controller
    - Need to create matrix of all possible Scans and corresponding status updates and error validation.
    