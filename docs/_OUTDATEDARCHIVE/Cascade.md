# Modify Work Order Cascade Rules

## Change status of a Product entity
- All child entities are updated to the same target state. Child entities include Parts, Hardware, Subassemblies, and Subassemblies themselves can contain Parts, Hardware and Nested Subassemblies, and Nested Subassemblies can contain Parts and Hardware.

## Change status of a Detached Product entity
- All child entities are updated to the same target state. Child entities should only include parts.

## Change status of a Nest Sheet entity
- All associated Parts are updated to the same target status.