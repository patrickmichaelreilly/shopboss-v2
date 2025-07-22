# Work Order List (Admin View)
    - Add ability to group multiple related work orders (project phases)
    - Add capablity of storing Shop Drawings in DB attached to Work Orders

# Modify Work Order
    - Add capability to merge additional SDF data into existing Work Order. Handle part re-cuts, supplementary Nest Sheets, etc.

# Import Process
    - Build Work Order Browser Modal from Import interface with new API endopoint for Browsing whitelisted folders.

# CNC Station
    - Improve Nest Sheet detail modal to include Nest Sheet image.
    - Add label printing capabilities to Nest Sheet modal.
    - Verify live status updates when Parts are modified at other stations.
    - Group and sort Nests by material
 
# Sorting Station
    - Getting a "Ready for Assembly" alert twice for Product with filtered parts (doors/drawer fronts). Once after Carcass again after Door. Just need 1 when Carcass is ready.
    - Style billboard area
    - Kill toast messages
    - Improve bin indication colors and visuals - Grey for empty, Yellow for partial, Red for indicating immediate location (matching billboard text), Green for ready to Assemble
       - Add interface for configuring filtering rules (instead of hardcoding) for Doors, Drawer Fronts, etc. Should be extensible for arbitrary rules.

# Universal Scanner Partial
    - Add scan even to audit trail

# Assembly Station
    - Improve list appearance. Decrease vertical size of each element to reduce length of list. Move completed items to end of list.
    - Remove Sorting Rack statistics box on righthand side of page COMPLETELY..

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
    