# Overall Project
    Scrub all emojis
    Need direct data audit view / Grid view
    Group multiple related work orders (project phases)
    Workflow for re-cuts, supplementary Nest Sheets
    
# Import Process
    Do part filtering now, add field to part data indicating Part Type (which drives storage rack and readiness calc.)

# Dashboard
    Eliminate. Open direct to work order view

# Work Order Detail View
    Nest Sheets count is stuck at 0
    Group similar hardware and "detached products"
    Filtering
    Archive functionality

# CNC Station
    Improve display
    Group items from 
    Eliminate popup Success confirmation
    Double processing on scan barcode action? ("Already...")
    Recent Scan History modal glitches out
    Nest sheet detail view with sticker printing (recuts as well)

# Sorting Station
    The storage rack fill counts next to their names does not update when i empty a bin.
    In bin details modal, if it has at least 1 part, it should show the rest of the parts for that product in the list, greyed out to show they haven't yet been scanned. Capacity should be "Completion" with denominator=total parts in product.
    Filtering doors, adjustable shelves, drawer fronts
    In Bin Details modal, the Status stat never changes from "Partial"
    When final part for a Product is scanned, the red triangle indicating ready appears in real time, but the Ready For Assembly button does not. The button however does appear if I refresh the view.

# Assembly Station
    "Drop Product" off line.
    Scan opens Product details modal, manual button for operator to indicate doors have been installed and cabinet dropped.
    "Locations" info uses Rack LinkID rather than human readable name. Also repeated for every part, only need it once.
    In Assembly Queue, completed Products show 0 Progress.
# Collab Process
    Explicitly tell me how to test every time
    Different types of task: Phase, fix, feature, root cause analysis, 