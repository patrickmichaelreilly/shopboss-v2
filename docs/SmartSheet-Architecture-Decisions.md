# SmartSheet Integration - Architecture Decision Records

## Overview

This document captures key architectural decisions for SmartSheet integration into ShopBoss, providing context and rationale for future development teams.

---

## ADR-001: Hybrid Integration Approach

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
We need to integrate SmartSheet functionality into ShopBoss project management while maintaining user familiarity and system performance. Research shows SmartSheet lacks a true client-side embed SDK, only providing API SDK and iframe publishing.

### Decision
Implement a **hybrid integration approach** combining:
1. SmartSheet API SDK for data synchronization 
2. iframe embedding for sheet interaction
3. Custom ShopBoss UI for enhanced functionality

### Rationale
- **API SDK**: Provides reliable data operations and sync capabilities
- **iframe embedding**: Preserves familiar SmartSheet user experience  
- **Custom UI**: Adds ShopBoss-specific enhancements and reporting
- **Hybrid approach**: Leverages strengths while mitigating individual limitations

### Consequences
✅ **Positive:**
- Users maintain familiar SmartSheet workflows
- Reliable data synchronization between systems
- Enhanced reporting and integration capabilities
- Graceful degradation when SmartSheet unavailable

⚠️ **Negative:**
- Increased complexity managing multiple integration patterns
- iframe security and styling limitations
- Potential data consistency challenges between systems

---

## ADR-002: Personal Access Token Authentication

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
SmartSheet supports both personal access tokens and OAuth 2.0 flows. We need to choose an authentication method that balances security, simplicity, and operational requirements.

### Decision
Use **personal access tokens** for Phase 1, with OAuth 2.0 as future enhancement.

### Rationale
**Phase 1 Requirements:**
- Single SmartSheet account integration
- Backend data synchronization only
- Simplified deployment and maintenance

**Personal Access Token Advantages:**
- No OAuth flow complexity
- Long-lived tokens (no refresh required)
- Suitable for server-to-server operations
- Matches current SmartSheetImportService pattern

### Consequences
✅ **Positive:**
- Faster implementation and deployment
- Reduced complexity in authentication flow
- Consistent with existing codebase patterns
- Reliable for automated synchronization

⚠️ **Negative:**
- Limited to single SmartSheet account
- No user-specific permissions
- Full account access (broad permissions)
- Manual token management

### Future Migration Path
- Phase 3: OAuth 2.0 for multi-user scenarios
- User-specific sheet access
- Granular permissions control

---

## ADR-003: Multi-Layer Caching Strategy

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
SmartSheet API has rate limits and latency considerations. We need caching to provide responsive user experience while respecting API constraints.

### Decision
Implement **multi-layer caching architecture:**
1. **L1 Cache (Memory)**: Frequently accessed metadata
2. **L2 Cache (Redis)**: Larger shared data across instances
3. **Background sync**: Proactive cache warming

### Rationale
**Performance Requirements:**
- Sub-second response for project data
- Minimize SmartSheet API calls
- Support multiple concurrent users

**Caching Strategy Benefits:**
- Memory cache: Fastest access for hot data
- Distributed cache: Shared across server instances
- Background sync: Reduces user-facing latency

### Configuration
```csharp
// Cache expiration policies
SheetData: 60 minutes
SheetMetadata: 4 hours  
WorkspaceList: 12 hours
Templates: 24 hours
```

### Consequences
✅ **Positive:**
- Responsive user experience
- Reduced API rate limit pressure
- Scalable across multiple servers
- Graceful handling of SmartSheet outages

⚠️ **Negative:**
- Increased memory and infrastructure requirements
- Cache invalidation complexity
- Potential data staleness
- Additional monitoring requirements

---

## ADR-004: Webhook-Based Real-Time Sync

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
Users need to see changes made in SmartSheet reflected in ShopBoss near real-time. We evaluated polling vs webhook approaches.

### Decision
Implement **SmartSheet webhooks** with SignalR for real-time UI updates.

### Rationale
**Webhook Advantages:**
- Near real-time updates (1-minute debounce in 2025)
- Reduces unnecessary API polling
- Event-driven architecture
- Lower resource consumption

**Integration Pattern:**
```
SmartSheet Change → Webhook → Cache Invalidation → SignalR → UI Update
```

### Implementation Details
- HTTPS endpoint required
- 200 response for successful delivery
- Exponential backoff retry handling
- Cache invalidation triggers

### Consequences
✅ **Positive:**
- Real-time user experience
- Efficient resource usage
- Event-driven architecture benefits
- Reduced API rate limit consumption

⚠️ **Negative:**
- Additional webhook endpoint security
- Network reliability dependencies  
- Complexity in error handling
- Debugging challenges for webhook failures

---

## ADR-005: Workspace-Based Project Organization

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
SmartSheet provides both individual sheets and workspace organization. We need to decide how to structure project data in SmartSheet.

### Decision
Use **SmartSheet workspaces** to organize project-related sheets and assets.

### Workspace Structure
```
Project Workspace: "{ProjectId} - {ProjectName}"
├── Primary Project Sheet (main tracking)
├── Supporting Sheets (sub-processes)
├── Reports (aggregated views)
└── Dashboards (visual summaries)
```

### Rationale
**Workspace Benefits:**
- Logical grouping of project assets
- Inheritance of permissions and branding
- Template-based project creation
- Consistent organization across projects

**Integration Approach:**
- Create workspace from templates
- Link workspace to ShopBoss Project entity
- Sync primary sheet data for core functionality

### Consequences
✅ **Positive:**
- Organized project structure in SmartSheet
- Template-driven consistency
- Scalable to complex project requirements
- Clear permission boundaries

⚠️ **Negative:**
- Increased complexity in workspace management
- Template maintenance requirements
- Potential workspace proliferation
- Additional API operations required

---

## ADR-006: iframe Publishing for Sheet Display

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
Users need to interact directly with SmartSheet data within ShopBoss interface. We evaluated custom grid implementations vs iframe embedding.

### Decision
Use **SmartSheet iframe publishing** for direct sheet interaction within project detail cards.

### Implementation
```html
<iframe 
  src="https://publish.smartsheet.com/{sheetId}"
  width="100%" height="600" frameborder="0">
</iframe>
```

### Rationale
**iframe Advantages:**
- Preserves full SmartSheet functionality
- No custom UI development required
- Automatic updates from SmartSheet
- Familiar user experience

**Security Considerations:**
- Sheets must be published (public access)
- CSP headers for iframe security
- Validation of sheet ownership

### Consequences
✅ **Positive:**
- Rapid implementation
- Full SmartSheet feature preservation
- Automatic updates
- Familiar user interface

⚠️ **Negative:**
- Security implications of published sheets
- Limited styling/branding control
- iframe communication limitations
- Dependency on SmartSheet availability

---

## ADR-007: Background Synchronization with Conflict Resolution

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
Data can be modified in both ShopBoss and SmartSheet. We need a strategy to handle bi-directional sync and conflict resolution.

### Decision
Implement **background synchronization** with **timestamp-based conflict resolution**.

### Sync Strategy
```csharp
public enum SyncConflictResolution
{
    ShopBossWins,     // ShopBoss data takes precedence
    SmartSheetWins,   // SmartSheet data takes precedence  
    MostRecentWins,   // Latest timestamp wins
    ManualResolution  // Flag for manual review
}
```

### Conflict Resolution Rules
1. **High-level project data**: ShopBoss wins (authoritative source)
2. **Task details and progress**: SmartSheet wins (work execution)
3. **Timestamps conflicts**: Most recent wins
4. **Critical conflicts**: Flag for manual resolution

### Consequences
✅ **Positive:**
- Automated conflict handling
- Preserves data integrity
- Flexible resolution strategies
- Audit trail for changes

⚠️ **Negative:**
- Complex synchronization logic
- Potential data loss in conflicts
- Manual intervention required for complex conflicts
- Increased testing complexity

---

## ADR-008: Graceful Degradation Strategy

**Date:** 2025-08-23  
**Status:** Accepted  
**Deciders:** Development Team

### Context
SmartSheet integration should enhance ShopBoss functionality without creating hard dependencies that break core workflows.

### Decision
Implement **graceful degradation** where SmartSheet unavailability doesn't prevent core ShopBoss operations.

### Degradation Levels
1. **Full Integration**: All features available
2. **Limited Integration**: Basic sync, no real-time updates
3. **Local Only**: ShopBoss data only, SmartSheet unavailable
4. **Emergency Mode**: Core project management without enhancements

### Implementation
```csharp
public class ProjectService
{
    public async Task<ProjectDisplayData> GetProjectDataAsync(string id)
    {
        var project = await GetCoreProjectDataAsync(id); // Always available
        
        try
        {
            var enhancedData = await GetSmartSheetDataAsync(id);
            project.EnhancedData = enhancedData;
            project.IntegrationStatus = IntegrationStatus.Available;
        }
        catch (SmartSheetException)
        {
            project.IntegrationStatus = IntegrationStatus.Degraded;
            // Core functionality continues
        }
        
        return project;
    }
}
```

### Consequences
✅ **Positive:**
- System resilience to external service failures
- User experience continuity
- Reduced support burden
- Operational flexibility

⚠️ **Negative:**
- Additional complexity in error handling
- Feature inconsistency across degradation modes
- Testing complexity for various failure scenarios

---

## Summary

These architecture decisions provide a foundation for SmartSheet integration that balances functionality, performance, security, and maintainability. The hybrid approach enables leveraging SmartSheet's strengths while maintaining ShopBoss's core capabilities and user experience.

Key principles underlying these decisions:
- **User Experience**: Preserve familiar workflows
- **System Resilience**: Graceful degradation when dependencies fail
- **Performance**: Multi-layer caching and efficient API usage
- **Security**: Appropriate authentication and access controls
- **Maintainability**: Clear patterns and well-documented decisions

Future teams should reference these decisions when extending or modifying the SmartSheet integration.