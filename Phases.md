# Part Categorization Optimization Phases

## Phase 1: Add Category Storage to Part Entity
- Add `Category` enum field to Part.cs model
- Create database migration for the new Category column
- Update all Part-related views and controllers to handle the new field

- Success criteria-- Functionality should not change, we have only added the field.

## Phase 2: Implement Early Categorization During Import
- Modify `ConvertToPartEntity` method in ImportSelectionService.cs to classify parts once during import
- Store the category directly in the Part entity using existing `PartFilteringService.ClassifyPart()` logic
- Only Parts need to be classified, the other entities (products, subassemblies, detached products) do not.

- Success criteria-- Sorting station functionality has still not changed. We are simply populating the new Category field during import IN PARALLEL with the existing filtering approach.

## Phase 3: Simplify PartFilteringService
- Remove redundant methods (`GetCarcassPartsOnly`, `GetFilteredParts`, `ShouldFilterPart`)
- Keep only `ClassifyPart` method for import-time classification
- Add simple query helper methods that work with stored categories

## Phase 4: Optimize Assembly Readiness Checking
- Refactor `CheckAssemblyReadinessAsync` to query by stored Category instead of re-classifying
- Replace complex filtering with simple `WHERE Category = PartCategory.Standard` queries
- Maintain same logic but with dramatically improved performance

## Phase 5: Enable Category Editing in UI
- Add Category field to Modify Work Order interface
- Allow users to override automatic categorization when needed
- No validation is required. The intent is to allow users to manually modify Category during Beta if the filters are not yet optimal.

## Expected Benefits:
- **90%+ reduction** in part classification computation
- **Simpler, more maintainable** PartFilteringService
- **Faster assembly readiness checks** (database queries vs in-memory loops)
- **New UI capability** for category management
- **Better separation of concerns** (classify once, use everywhere)