# SmartSheet OAuth Testing Checklist

## Overview

This comprehensive checklist ensures OAuth write operations work correctly with proper user attribution before implementing Phase 2 bi-directional capabilities. Each test should be performed systematically to validate the OAuth integration.

## Pre-Testing Setup

### ✅ Environment Preparation
- [ ] **SmartSheet OAuth app configured** with correct redirect URI
- [ ] **Test SmartSheet workspace** created with appropriate permissions  
- [ ] **Test project created** in ShopBoss with SmartSheetId linked
- [ ] **Development environment** running with HTTPS (required for OAuth)
- [ ] **Test user account** with SmartSheet access (patrick@eurotexmfg.com)
- [ ] **Browser developer tools** ready for network inspection
- [ ] **SmartSheet activity log** accessible for verification

### ✅ Configuration Validation
- [ ] **appsettings.json** contains correct OAuth client credentials
- [ ] **Session configuration** properly set up in Program.cs
- [ ] **SmartSheetService** correctly registered with DI
- [ ] **OAuth redirect URL** matches SmartSheet app configuration
- [ ] **Test sheet ID** available for testing operations

## Phase 1: OAuth Authentication Testing

### ✅ OAuth Flow Validation
- [ ] **Initiate OAuth flow** from ShopBoss project details page
- [ ] **Popup window opens** to SmartSheet authorization page
- [ ] **User can authenticate** with SmartSheet credentials
- [ ] **Authorization succeeds** and popup closes automatically
- [ ] **Session tokens stored** (verify in browser dev tools)
- [ ] **User email/name displayed** in ShopBoss interface
- [ ] **SmartSheet connection status** shows as "Connected"

### ✅ Session Management Testing
- [ ] **Session persists** across browser page refreshes
- [ ] **Multiple tabs** can access SmartSheet functions
- [ ] **Session timeout** works correctly (test after configured timeout)
- [ ] **Manual logout** clears session tokens properly
- [ ] **Re-authentication** works after session expiry

### ✅ Token Information Verification
```csharp
// Test these session values exist and are valid:
- [ ] "ss_access_token" - Non-empty access token
- [ ] "ss_refresh_token" - Non-empty refresh token  
- [ ] "ss_token_expires" - Future expiration date
- [ ] "ss_user_email" - Correct user email
- [ ] "ss_user_name" - Correct user name
```

## Phase 2: Read Operations Testing

### ✅ Basic Sheet Access
- [ ] **Get sheet metadata** using OAuth token
- [ ] **Verify sheet information** displays correctly (name, row count, last modified)
- [ ] **Compare with direct API token** results (should be identical)
- [ ] **Error handling** works for invalid sheet IDs
- [ ] **Rate limiting** respected during repeated calls

### ✅ Sheet Content Reading
```csharp
// Test cases for GetSheetAsync:
- [ ] **Get basic sheet data** (no includes)
- [ ] **Get sheet with attachments** (include attachments)
- [ ] **Get sheet with discussions** (include discussions)  
- [ ] **Get sheet with all includes** (attachments, discussions)
- [ ] **Large sheet handling** (100+ rows)
```

### ✅ User Attribution Verification
- [ ] **API calls show correct user** in SmartSheet activity log
- [ ] **User email matches** session stored email
- [ ] **Timestamps are accurate** in SmartSheet logs
- [ ] **No anonymous access** occurring

## Phase 3: Write Operations Testing

### ✅ Row Addition Testing
```csharp
// Test AddRowToSheet functionality:
- [ ] **Add single row** with multiple columns
- [ ] **Verify row appears** in SmartSheet interface
- [ ] **User attribution correct** in SmartSheet activity log
- [ ] **Cell values accurate** (strings, numbers, dates)
- [ ] **Error handling** for invalid column IDs
- [ ] **Return value** contains new row ID
```

### ✅ Cell Update Testing
```csharp
// Test UpdateCellAsync functionality:
- [ ] **Update text cell** with new string value
- [ ] **Update number cell** with numeric value
- [ ] **Update date cell** with DateTime value
- [ ] **Update status/picklist** cell with valid option
- [ ] **User attribution** shows in cell modification history
- [ ] **Error handling** for read-only cells
```

### ✅ Bulk Operations Testing
```csharp
// Test bulk operations:
- [ ] **Add multiple rows** (5-10 rows) in single request
- [ ] **Update multiple cells** across different rows
- [ ] **Verify all changes** appear in SmartSheet
- [ ] **Performance acceptable** (under 5 seconds for 10 rows)
- [ ] **Partial success handling** works correctly
- [ ] **Rate limiting** doesn't trigger during bulk operations
```

### ✅ Comment/Discussion Testing
```csharp
// Test AddCommentAsync functionality:
- [ ] **Add comment to row** with text content
- [ ] **Comment appears** in SmartSheet interface
- [ ] **Correct user attribution** in comment metadata
- [ ] **Comment timestamp** accurate
- [ ] **Multiple comments** on same row work
- [ ] **Comment with mentions** (@username) work if supported
```

## Phase 4: Advanced Write Operations

### ✅ Column Type Handling
```csharp
// Test different column types:
- [ ] **TEXT_NUMBER columns** - strings and numbers
- [ ] **DATE columns** - DateTime values formatted correctly
- [ ] **DATETIME columns** - full timestamp support
- [ ] **CHECKBOX columns** - boolean true/false values
- [ ] **PICKLIST columns** - predefined option values
- [ ] **CONTACT_LIST columns** - email addresses
- [ ] **DURATION columns** - time span values
```

### ✅ Error Scenarios Testing
```csharp
// Test error handling:
- [ ] **Invalid sheet ID** returns appropriate error
- [ ] **Invalid column ID** handled gracefully  
- [ ] **Invalid row ID** for updates handled
- [ ] **Permission denied** scenarios handled
- [ ] **Rate limit exceeded** triggers retry logic
- [ ] **Network timeout** handled with retry
- [ ] **Malformed data** rejected appropriately
```

### ✅ Data Validation Testing
```csharp
// Test data integrity:
- [ ] **Special characters** in text fields (Unicode, symbols)
- [ ] **Large text values** (1000+ characters)
- [ ] **Null/empty values** handled correctly
- [ ] **Date ranges** (past, future, edge cases)
- [ ] **Number formats** (integers, decimals, negative)
- [ ] **Boolean representations** (true/false, 1/0)
```

## Phase 5: Integration Testing

### ✅ ShopBoss Integration
```csharp
// Test with actual ShopBoss workflows:
- [ ] **Project status update** syncs to SmartSheet
- [ ] **New milestone creation** adds row to SmartSheet
- [ ] **File attachment** via ShopBoss appears in SmartSheet
- [ ] **User comment** from ShopBoss shows in SmartSheet
- [ ] **Multiple users** can work simultaneously
- [ ] **Session conflicts** handled appropriately
```

### ✅ Real-world Scenarios
```csharp
// Test realistic usage patterns:
- [ ] **Morning startup** - OAuth still valid from previous day
- [ ] **Long session** - work for 4+ hours without re-auth
- [ ] **Multiple projects** - switch between different linked sheets
- [ ] **Concurrent edits** - ShopBoss and SmartSheet simultaneously
- [ ] **Large batch** - add 50+ rows (process module)
- [ ] **Mixed operations** - reads and writes in same session
```

## Phase 6: Performance & Reliability Testing

### ✅ Performance Benchmarks
```csharp
// Measure and verify performance:
- [ ] **Single row add** - < 2 seconds
- [ ] **10 row bulk add** - < 5 seconds  
- [ ] **50 row bulk add** - < 15 seconds
- [ ] **Sheet metadata fetch** - < 1 second
- [ ] **Large sheet read** (500+ rows) - < 10 seconds
- [ ] **Comment addition** - < 3 seconds
```

### ✅ Reliability Testing
```csharp
// Test robustness:
- [ ] **100 consecutive operations** - no failures
- [ ] **Rapid succession** - 10 operations in 30 seconds
- [ ] **Network interruption** - retry logic works
- [ ] **Token near expiry** - refresh handled automatically
- [ ] **SmartSheet maintenance** - graceful degradation
- [ ] **Browser refresh** - session recovery works
```

## Testing Checklist Documentation

### ✅ Test Results Recording
For each test, record:
- [ ] **Test name and description**
- [ ] **Expected behavior**
- [ ] **Actual behavior**  
- [ ] **Pass/Fail status**
- [ ] **Error messages** (if any)
- [ ] **SmartSheet activity log** screenshot
- [ ] **Performance metrics** (timing)
- [ ] **Notes and observations**

### ✅ Issue Tracking
For any failures:
- [ ] **Document exact steps** to reproduce
- [ ] **Include error messages** and stack traces
- [ ] **Note browser/environment** details
- [ ] **Identify root cause** if possible
- [ ] **Prioritize fix** (blocker/high/medium/low)
- [ ] **Test fix verification** once resolved

## Pre-Production Validation

### ✅ Final Verification
Before moving to Phase 2 implementation:
- [ ] **All critical tests passing** (row add/update/comments)
- [ ] **User attribution working** consistently
- [ ] **Performance acceptable** for expected usage
- [ ] **Error handling robust** for common failures
- [ ] **Session management reliable** across typical usage
- [ ] **Integration points tested** with ShopBoss workflows

### ✅ Documentation Complete
- [ ] **Test results documented** with screenshots
- [ ] **Known limitations noted** for future reference
- [ ] **Performance benchmarks recorded** 
- [ ] **User instructions updated** if needed
- [ ] **OAuth flow documented** for other developers
- [ ] **Troubleshooting guide created** for common issues

## Success Criteria

### ✅ Phase 2 Readiness Checklist
OAuth write operations are ready for Phase 2 implementation when:

**Authentication**
- [ ] OAuth flow works reliably for all test users
- [ ] Session management handles typical usage patterns
- [ ] Token refresh works automatically

**Write Operations**  
- [ ] Can add rows to SmartSheet with proper user attribution
- [ ] Can update cells with various data types
- [ ] Can add comments/discussions to rows
- [ ] Bulk operations work within rate limits

**Integration**
- [ ] ShopBoss can modify SmartSheet data through UI actions
- [ ] Changes appear correctly in SmartSheet interface
- [ ] User activity shows proper attribution in SmartSheet logs

**Reliability**
- [ ] Error handling prevents crashes or data corruption
- [ ] Performance meets acceptable standards
- [ ] Rate limiting handled gracefully

**Next Steps**
Once all tests pass, proceed to Phase 2 implementation:
1. Convert existing SmartSheet data fetching to use OAuth
2. Add bi-directional write capabilities to ShopBoss UI
3. Implement "View in SmartSheet" links
4. Prepare foundation for Phase 3 modular process composition

This comprehensive testing ensures OAuth write operations work correctly before building the enhanced bi-directional interface in Phase 2.