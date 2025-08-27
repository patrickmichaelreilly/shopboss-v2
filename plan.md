# Phase 3: SmartSheet OAuth Consolidation Plan

## Overview

Consolidate all SmartSheet integration into a single, session-based OAuth system with true user attribution. Remove deprecated caching infrastructure and duplicate API systems.

**Goals:**
-  True user attribution for all SmartSheet operations
-  Unified SmartSheet integration architecture
-  Remove ~500+ lines of deprecated code
-  Improve UX with persistent OAuth status indicator
-  Maintain migration functionality for importing whole sheets

## Current State Analysis

### Existing SmartSheet Systems

#### System 1: Direct API Integration (TO BE REMOVED)
**Files:**
- `Services/SmartSheetCacheService.cs` (433 lines) - **DELETE COMPLETELY**
- `Services/SmartSheetImportService.cs` (partial) - **REFACTOR** to use OAuth
- `Controllers/ProjectController.cs` (cache methods) - **REMOVE METHODS**

**Methods to Remove from ProjectController:**
- `CacheSheet(long sheetId)`
- `CacheWorkspace(long? workspaceId, string? workspaceName)` 
- `ExecuteCacheQuery([FromBody] CacheQueryRequest request)`
- `GetCacheStats()`
- `ClearCache()`
- All cache-related endpoints (~200 lines)

**Database Cleanup:**
- Remove cache-related tables from database schema
- Remove SmartSheetCacheService registration from Program.cs

#### System 2: OAuth Integration (TO BE ENHANCED)
**Files:**
- `Controllers/SmartSheetAuthController.cs` - **ENHANCE** with session management
- `Services/SmartSheetService.cs` - **ENHANCE** to be session-aware
- `Controllers/SmartSheetMigrationController.cs` - **MIGRATE** to OAuth

**Current Issues:**
- OAuth only triggers during sheet linking
- No persistent auth status indicator
- Tokens stored globally, not per-user session

### Code to Keep but Migrate

**SmartSheetMigrationController:**
- Import whole sheets functionality (currently used for development)
- Data transformation logic from SmartSheetImportService
- Need to convert from API key to OAuth per-user

## Target Architecture

### Per-User Session-Based OAuth

**Session Storage Pattern:**
```csharp
// Store per user session
HttpContext.Session.SetString($"SmartSheet_AccessToken_{userId}", accessToken);
HttpContext.Session.SetString($"SmartSheet_RefreshToken_{userId}", refreshToken);
HttpContext.Session.SetString($"SmartSheet_TokenExpiry_{userId}", expiry.ToString());

// Retrieve for operations
var userToken = HttpContext.Session.GetString($"SmartSheet_AccessToken_{userId}");
```

**OAuth Status Management:**
- Check authentication status on every Project page load
- Display auth status in header with visual indicator
- One-click re-authentication flow
- Handle token refresh transparently

### Unified SmartSheet Service

**Single Service Pattern:**
```csharp
public class SmartSheetOAuthService
{
    // Session-aware client instantiation
    public SmartsheetClient GetUserClient(HttpContext context)
    
    // All operations use user's token
    public async Task<Sheet> GetSheetAsync(long sheetId, HttpContext context)
    public async Task AddCommentAsync(long sheetId, string comment, HttpContext context)
    public async Task UploadAttachmentAsync(long sheetId, IFormFile file, HttpContext context)
}
```

## Implementation Plan

### Phase 3.1: Remove Deprecated Cache System
**Scope:** ~2 days
**Files Modified:** 4 files
**Lines Removed:** ~500 lines

**Tasks:**
1. Remove SmartSheetCacheService.cs entirely
2. Remove cache-related methods from ProjectController
3. Remove cache service registration from Program.cs
4. Remove cache database tables (create migration)
5. Update any UI that references caching

**Validation:**
-  Project builds successfully
-  No broken references to SmartSheetCacheService
-  Cache-related UI elements removed

### Phase 3.2: Enhance OAuth System for Sessions  
**Scope:** ~3 days
**Files Modified:** 3 files
**Lines Added:** ~200 lines

**Tasks:**
1. **SmartSheetAuthController Enhancement:**
   - Add session-based token storage
   - Add token refresh logic
   - Add auth status checking endpoint
   - Add logout/clear session endpoint

2. **SmartSheetService Enhancement:**
   - Make all methods session-aware
   - Add HttpContext parameter to methods
   - Implement per-request client instantiation
   - Add token validation/refresh

3. **OAuth Status Header:**
   - Add auth status indicator to _Layout.cshtml
   - Add JavaScript for status checking
   - Style connected/disconnected states
   - Add one-click auth/re-auth

**Validation:**
-  Multiple users can authenticate independently
-  Tokens persist across page loads
-  Token refresh works automatically
-  Auth status shows correctly in header

### Phase 3.3: Migrate SmartSheet Migration to OAuth
**Scope:** ~2 days  
**Files Modified:** 2 files
**Lines Modified:** ~150 lines

**Tasks:**
1. **SmartSheetMigrationController Updates:**
   - Remove SmartSheetImportService dependency
   - Add SmartSheetOAuthService dependency
   - Update all methods to use session-based auth
   - Maintain existing import functionality

2. **Data Transformation Migration:**
   - Extract useful logic from SmartSheetImportService
   - Move to SmartSheetOAuthService or helper classes
   - Remove SmartSheetImportService.cs entirely

**Validation:**
-  SmartSheet Migration page works with OAuth
-  Whole sheet import functionality preserved
-  User attribution works for imported data
-  No references to old import service remain

### Phase 3.4: UX Polish and Testing
**Scope:** ~1 day
**Files Modified:** 2-3 files
**Lines Added:** ~100 lines

**Tasks:**
1. **Enhanced UX:**
   - OAuth onboarding flow for new users
   - Better error messages for auth failures
   - Loading states for SmartSheet operations
   - Toast notifications for auth status changes

2. **Integration Testing:**
   - Test multiple users authenticating simultaneously
   - Test token refresh scenarios  
   - Test session timeout scenarios
   - Test all SmartSheet operations with attribution

**Validation:**
-  Multiple concurrent users work correctly
-  Auth flows are intuitive and reliable
-  All SmartSheet operations show proper user attribution
-  Error handling is robust

## Risk Mitigation

### Technical Risks

**Risk:** Session-based tokens don't refresh properly
**Mitigation:** 
- Implement token refresh before each API call
- Add retry logic with re-auth if refresh fails
- Store refresh token expiry separately

**Risk:** Multiple browser tabs cause auth conflicts  
**Mitigation:**
- Use consistent session keys across tabs
- Implement session sharing across browser instances
- Add UI warnings about concurrent sessions

**Risk:** SmartSheet API rate limiting with multiple users
**Mitigation:**
- Implement request queuing/throttling
- Add retry logic with exponential backoff
- Monitor API usage patterns

### UX Risks

**Risk:** Users forget to authenticate before using features
**Mitigation:**
- Prominent auth status in header
- Auto-redirect to auth flow when needed
- Clear messaging about SmartSheet features requiring auth

**Risk:** Complex re-authentication flow
**Mitigation:**
- One-click re-auth from header
- Remember user context during re-auth
- Return to original page after successful auth

## Success Criteria

### Functional Requirements
-  All SmartSheet operations use per-user OAuth tokens
-  Comments and attachments show correct user attribution in SmartSheet
-  Multiple users can be authenticated simultaneously  
-  SmartSheet Migration functionality preserved
-  No deprecated cache code remains

### Technical Requirements  
-  ~500 lines of deprecated code removed
-  Zero compilation errors or warnings
-  All existing SmartSheet features work
-  Session management is robust and reliable

### UX Requirements
-  OAuth status clearly visible in header
-  One-click authentication/re-authentication
-  Smooth user experience with proper loading states
-  Clear error messages for auth failures

## File Impact Summary

### Files to Delete Completely
- `Services/SmartSheetCacheService.cs` (433 lines)
- `Services/SmartSheetImportService.cs` (after migration)

### Files to Modify Significantly  
- `Controllers/ProjectController.cs` (~200 lines removed)
- `Controllers/SmartSheetAuthController.cs` (~100 lines added)
- `Services/SmartSheetService.cs` (~100 lines modified)
- `Controllers/SmartSheetMigrationController.cs` (~50 lines modified)
- `Views/Shared/_Layout.cshtml` (~20 lines added)

### New Files to Create
- `Services/SmartSheetOAuthService.cs` (~300 lines) - Unified OAuth service
- `wwwroot/js/smartsheet-auth.js` (~100 lines) - Auth status management

### Database Changes
- Remove cache-related tables via migration
- No new tables needed (using sessions for tokens)

## Next Steps

1. **Review and Approve Plan** - Discuss any concerns or modifications needed
2. **Begin Phase 3.1** - Start with removal of deprecated cache system  
3. **Incremental Development** - Complete each phase fully before moving to next
4. **User Testing** - Test with real SmartSheet accounts after Phase 3.2
5. **Production Deployment** - Deploy incrementally with rollback plan

---

**Estimated Total Effort:** 8 development days
**Estimated Total Impact:** Remove ~700 lines, Add ~500 lines, Net reduction ~200 lines
**Risk Level:** Medium (session management complexity)
**Business Value:** High (true user attribution, simplified architecture)