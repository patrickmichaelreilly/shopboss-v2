# Station Scanning Fix: Case-Insensitive + Suffix Handling

## Problem Analysis
- **Sorting Station**: Uses exact match only - suffixed parts (e.g., `PART123_1`) never get found when scanning `PART123`
- **Assembly Station**: Uses `StartsWith()` correctly for suffix matching but `StartsWith()` is case-sensitive
- **Shipping Station**: Uses `StartsWith()` but it's case-sensitive
- **Scanner**: Currently forces uppercase, breaking mixed-case barcodes

## Current Status Filtering (Consumption Pools)
- **CNC**: Scans NestSheets - updates Parts from `Pending` to `Cut`
- **Sorting**: Scans Parts with `Status == Cut` - updates to `Sorted` 
- **Assembly**: Scans Parts to find Product - must verify Product `Status != Assembled` (DetachedProducts bypass assembly)
- **Shipping**: Scans entities to find Product/DetachedProduct - must verify `Status != Shipped`

## Proposed Solution

### 1. Scanner Fix (Already Done)
### 2. CNC Station (Already Done)  
### 3. Sorting Station (NEEDS SUFFIX FIX)
- **Problem**: Exact match fails for suffixed parts
- **Fix**: Add case-insensitive `StartsWith()` pattern
- **Query**: Parts with `Status == Cut` (correct consumption pool)
- **Logic**: `(exact_match || name_match || case_insensitive_startswith)`

### 4. Assembly Station (NEEDS CASE-INSENSITIVE SUFFIX + PRODUCT STATUS FILTER)
- **Problem**: `StartsWith()` is case-sensitive  
- **Fix**: Make existing `StartsWith()` case-insensitive AND add Product status filter
- **Query**: Parts from any status BUT Product must have `Status != Assembled`
- **Logic**: Keep existing pattern but make case-insensitive + verify Product not already assembled
- **Note**: DetachedProducts are NOT handled at Assembly station - they bypass assembly entirely

### 5. Shipping Station (NEEDS CASE-INSENSITIVE SUFFIX + PRODUCT STATUS FILTER)
- **Problem**: `StartsWith()` is case-sensitive AND missing Product/DetachedProduct status verification
- **Fix**: Make existing `StartsWith()` case-insensitive AND add Product/DetachedProduct status filters  
- **Query**: Products/Parts/Hardware/DetachedProducts BUT must verify Product/DetachedProduct `Status != Shipped`
- **Logic**: Keep existing patterns but make case-insensitive + verify not already shipped
- **USER NOTE**: LIKE THE ASSEMBLY STATION, SCANNING ANY ENTITY INDICATES THE PRODUCT BEING ADDRESSED. SO THE RETURNED Product or Detached Product must be validated to have a Status that is not yet Shipped.

## Implementation Strategy
1. Replace `p.Id.StartsWith(barcode + "_")` with case-insensitive equivalent
2. Add suffix pattern to Sorting station (missing entirely)
3. Use consistent `EF.Functions.Collate("NOCASE")` for all string comparisons
4. Add Product status filters to Assembly and Shipping stations
5. Research current Product status filtering in Assembly/Shipping to avoid breaking existing logic

## Expected Result
- Mixed-case barcodes work at all stations
- Suffixed parts (e.g., `PART123_1`) found when scanning base code (`PART123`)
- Each station maintains its correct consumption pool filtering
- Assembly station won't process already-assembled products
- Shipping station won't process already-shipped products/detached products