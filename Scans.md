# CNC Station
## Valid Scans
### Nest Sheet
- Do ProcessNestSheet (change all child part Status fields to "Cut")
## Invalid Scans
- Any other entity. Show error message "Not a valid Nest Sheet from the Active Work Order"

---

# Sorting Station
## Valid Scans
### Part of a Product
- Use Filtering rules to assign the Part to a special rack if required (Doors, Drawer Fronts, Adjustable Shelves currently)
- Look if the corresponding Product has been assigned a Bin (another carcass part has already been sorted)
- Write to Billboard the sorting destination (with corresponding carcass parts if bin already assigned, into empty bin if not, into special  racks if Filtered)
- Color alert the Bin in the Rack display (grey for empty, yellow for partially full, green for full and Ready for Assembly, blinking red for currently being indicated by the prior scan action. The blinking red bin should match the one being called out in the Billboard area)
### Part of a Detached Product
- Part bypasses Sorting and Assembly. Write Billboard message to acknowledge the next destination - Shipping.
## Invalid Scans
### Nest Sheet. 
- Show error "Not a part"

---

# Assembly Station
## Valid Scans
### Part of a Product
- Look up corresponding Product associated with the Part. Change status of Product and all Parts to "Assembled"
- Look for associated Doors, Drawer Fronts, Shelves that have been filtered to a special rack. Give Location Guidance in the Billboard area for the operator to find the associated parts.
- Empty related bin/bins.
## Invalid Scans
### Part of a Detached Product
- Error message "Assembly not required"
### Nest Sheet
- Error message "Not a part"

---

# Shipping Station
## Valid Scans
### Part of a Product
- Look up corresponding Product associated with the Part. Change status of Product and all Parts to "Shipped"
### Part of a Detached Product
- Change status of Part and Detached Product to "Shipped"
## Invalid Scans
### Nest Sheet
- Error message "Not a product"
