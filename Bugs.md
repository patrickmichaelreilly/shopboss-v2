# Overall Project
    Scrub all emojis
    Need direct data audit view / Grid view
    View audit logs
    Group multiple related work orders (project phases)
    Workflow for re-cuts, supplementary Nest Sheets

# Import Process
    Handle repeat work order names

# Work Order Detail View
    Group similar hardware and "detached products"
    Filtering
    Archive functionality

# CNC Station
    Improve display
    Eliminate popup Success confirmation
    Double processing on scan barcode action? ("Already...")
    Recent Scan History modal glitches out
    Nest sheet detail view with sticker printing (recuts as well)
    Nest cut status does not correctly update if all the parts from the nest are marked cut by other means

# Sorting Station
    The storage rack fill counts next to their names does not update when i empty a bin.
    In bin details modal, if it has at least 1 part, it should show the rest of the parts for that product in the list, greyed out to show they haven't yet been scanned. Capacity should be "Completion" with denominator=total parts in product.
    Filtering doors, adjustable shelves, drawer fronts
    In Bin Details modal, the Status stat never changes from "Partial"
    When final part for a Product is scanned, the red triangle indicating ready appears in real time, but the Ready For Assembly button does not. The button however does appear if I refresh the view.
    Open to a default rack

    Getting notification for every completed product (repeatedly) after completing a new product
    Getting notification for complete product when carcass is complete but door has not been sorted

    Cut Parts list modal -- "Sort" buttons not working


# Assembly Station
    "Drop Product" off line.
    Scan opens Product details modal, manual button for operator to indicate doors have been installed and cabinet dropped.
    "Locations" info uses Rack LinkID rather than human readable name. Also repeated for every part, only need it once.
    In Assembly Queue, completed Products show 0 Progress.


# Collab Process
    Explicitly tell me how to test every time
    Different types of task: Phase, fix, feature, root cause analysis, 
    MCP Servers
    CLAUDE.md

# Rack Configuration
    Deleting a rack must deal with parts if it is not empty
    