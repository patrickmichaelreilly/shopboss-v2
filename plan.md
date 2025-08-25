# SmartSheet Integration - Vision & Implementation Plan (UPDATED)

## VISION: ShopBoss as Visual Process Composition Platform

Transform ShopBoss from a tracking system into a **visual workflow composition platform** where:
- **Task Chunks** capture reusable process patterns with inputs/outputs
- **Business entities** (POs, Approvals, Drawings) flow between chunks and systems  
- **SmartSheet** provides communication and coordination layer
- **ShopBoss** orchestrates the overall workflow and business entity management

**Core Innovation:** Instead of rigid templates, enable **modular process composition** where workflows are built from reusable, interconnected Task Chunks that reflect real shop operations.

## Product Requirements Document (PRD) - REVISED

### Core Objectives (Updated)
1. **Use SmartSheet as flexible data backend** while ShopBoss provides process-aware interface
2. **Enable bi-directional sync** between ShopBoss projects and SmartSheet workspace
3. **Compose modular processes** per project instead of rigid templates
4. **Parse SmartSheet data** for enhanced visibility and reporting

### Key Features (Revised)

#### 1. Timeline-Centric Project Dashboard (Evolution in Progress)
- **Core Discovery:** Chronological comment/attachment timeline tells the real project story
- SmartSheet grid templates are inconsistent - timeline is the source of truth
- Answers key questions: current status, waiting states, next actions, recent activity
- **Status:** ✅ Foundation (SmartSheet Migration import) built, enhancing with intelligence

#### 2. Project-Sheet Linking (Completed)
- Link projects to corresponding SmartSheet using manual ID entry
- Session-based OAuth authentication for user attribution
- Display SmartSheet metadata in project cards
- **Status:** ✅ Complete in Phase 1

#### 3. Modular Process Composition (Future)
- Create bespoke workflows by composing process modules per project
- Library of reusable process chunks (CNC, Finishing, Installation, etc.)
- Add/remove process steps based on job requirements
- Write process modules as rows to SmartSheet via OAuth

#### 4. Bi-directional Authoring
- Edit project data in ShopBoss dashboard
- Changes sync to SmartSheet grid automatically
- "View in SmartSheet" for familiar grid access
- Users can work in either interface seamlessly

### Technical Requirements (Updated)
- SmartSheet API for all data operations (no embed SDK needed)
- Session-based OAuth for user attribution
- SignalR for real-time updates within ShopBoss
- Memory caching for performance
- Direct SmartSheet grid access via external links

## PHASED IMPLEMENTATION PLAN (UPDATED)

### Phase 0: Research & Documentation Foundation ✅ COMPLETED
**Objective:** Collect and curate documentation to guide implementation

**Status:** ✅ **Complete** - Comprehensive documentation created including:
- `docs/SmartSheet-Embed-SDK-Guide.md` (determined no embed SDK exists)
- `docs/SmartSheet-Authentication-Flows.md` 
- `docs/SmartSheet-Session-OAuth-Flow.md`
- `docs/SmartSheet-Architecture-Decisions.md`
- All architectural patterns and decisions documented

### Phase 1: SmartSheet OAuth & Project Linking ✅ COMPLETED  
**Objective:** Implement session-based OAuth and project-sheet linking

**Status:** ✅ **Complete** - Full OAuth implementation delivered:
- Session-based OAuth authentication working
- Project-SmartSheet linking via manual ID entry
- SmartSheet metadata display in project cards
- Link/unlink functionality operational
- User attribution preserved in session

**Key Achievement:** Foundation established for all future SmartSheet operations

### Phase 2: Task Chunk Discovery & Manual Organization (CURRENT)
**Objective:** Enable manual organization of timeline events into **Task Chunks** - reusable process building blocks that will become the foundation for workflow composition

**Key Insight:** The groups users create aren't just organization tools - they're **process templates** that capture reusable patterns of work with inputs and outputs that connect to business entities.

**Task Chunk Concept:** Process modules with inputs/outputs containing SmartSheet activities (comments/attachments) that produce business entities (POs, Approvals, Drawings) flowing to other systems.

**Implementation Tasks:**
1. **Restructure Project Cards** - Consolidate SmartSheet status, eliminate Files card, streamline Purchase Orders
2. **Add Manual Grouping UI** - Create Task Chunks by organizing timeline events with grouping
3. **Simple Visual Hierarchy** - Groups show as process headers with bounding boxes around constituent events
4. **No Validation** - Complete flexibility for users to discover natural process patterns

**Technical Foundation - CRITICAL ARCHITECTURE INSIGHT:**
- **TaskChunk as Separate Entity**: TaskChunk must be its own entity, NOT special ProjectEvents
- **Entity Relationship**: TaskChunk -> ProjectEvents via FK (TaskChunkId field on ProjectEvent)
- **Proper Data Model**: 
  ```
  TaskChunk {
    Id, ProjectId, Name, Description, DisplayOrder, IsTemplate
    Events: List<ProjectEvent> (navigation property)
  }
  
  ProjectEvent {
    // existing fields...
    TaskChunkId: string? (FK to TaskChunk)
    ChunkDisplayOrder: int (order within chunk)
  }
  ```
- Simple UI for drag-and-drop selection, chunk creation, reordering
- Build on existing SmartSheet Migration functionality (chronological import already works)
- Foundation for business entity connections and template system (future)

**Learning Phase Goal:**
- Users manually organize existing project data into logical Task Chunks
- Discover patterns of work that naturally emerge from real projects
- Build library of common chunks through hands-on organization

**Validation:**
- Users can organize timeline events in any hierarchy they want
- Groups are collapsible and clearly show relationships through indentation
- All organization persists correctly and displays consistently
- No restrictions on what can be grouped together

### Phase 3: Task Chunk Composition & Deployment (FUTURE)
**Objective:** Deploy discovered Task Chunks as reusable process templates for new project creation

**Approach:** Convert discovered chunks into templates → Visual workflow composition → Business entity flow between chunks and systems → Bi-directional SmartSheet sync

### Phase 4: Advanced Bi-directional Sync (FUTURE)
**Objective:** Full synchronization between ShopBoss and SmartSheet

**Focus:**
- Real-time updates via webhooks
- Conflict resolution strategies  
- Comprehensive data mapping
- Performance optimization

## Foundation Complete ✅
**OAuth, project linking, timeline import, SmartSheet analysis tools** - Ready for TaskChunk implementation

**Key Insight:** Timeline > Grid - SmartSheet timelines tell the real project story, process-aware UI beats generic grid interface.

## Ready for Phase 2: Task Chunk Discovery
**Current Objective:** Enable manual organization of timeline events into Task Chunks - the building blocks for future process composition

**Key Insight:** Timeline events from SmartSheet (comments/attachments) represent the **activities within process chunks**. By letting users manually group these activities, we discover natural process patterns that become reusable templates.

**Implementation Steps (Corrected Architecture):**
1. **Create TaskChunk Entity** - Separate model with proper relationships
2. **Add TaskChunk DbSet** - Configure in DbContext with navigation properties  
3. **Create Migration** - Add TaskChunks table with FK relationships
4. **Update Timeline Rendering** - Display TaskChunks with their contained events
5. **Add Chunk Management UI** - Multi-select events, create chunks, drag-and-drop
6. **Server-Side Persistence** - Controller endpoints for chunk CRUD operations

**Key Lesson Learned:** Avoid implementing groups as special ProjectEvents - use proper entity separation for future extensibility and template support.