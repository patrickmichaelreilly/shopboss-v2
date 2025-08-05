# SmartSheet Migration Tool - MVP

## Overview
A lightweight tool for manually importing SmartSheet projects one at a time into ShopBoss.

## What Was Built

### 1. Database Changes
- **ProjectEvent Model**: Tracks timeline events (comments, attachments, status changes)
- **Added to DbContext**: Configured with proper relationships
- **No Migrations**: Uses EnsureCreated() - database will be created fresh

### 2. New Files Created
```
/Controllers/SmartSheetMigrationController.cs  - Main controller
/Models/ProjectEvent.cs                        - Timeline event model
/Views/SmartSheetMigration/Index.cshtml       - 4-section UI
/wwwroot/js/smartsheet-migration.js           - Frontend interactions
/Services/SmartSheetImportService.cs          - Extended with 3 new methods
```

### 3. How to Access
Navigate to: **`/SmartSheetMigration`**

### 4. User Flow
1. **Select Sheet**: Choose from Active Jobs or Archived Jobs workspace
2. **View Data**: See all SmartSheet data (summary, attachments, comments, timeline)
3. **Manual Review**: User fills/corrects project form fields
4. **Import**: Creates Project + ProjectEvents + downloads attachments

### 5. What It Does
- ✅ Lists all sheets from SmartSheet workspaces
- ✅ Displays sheet attachments, comments, and reconstructed timeline
- ✅ Pre-fills project form with available data
- ✅ Creates Project entity with timeline events
- ✅ Downloads and stores all attachments
- ✅ Basic error handling with user messages

### 6. What It Doesn't Do
- ❌ No automatic data validation/correction
- ❌ No progress tracking or complex UI
- ❌ No authorization (anyone can access)
- ❌ No retry logic or sophisticated error recovery
- ❌ No duplicate detection (relies on existing ShopBoss validation)

### 7. Configuration Required
Make sure SmartSheet access token is configured in `appsettings.json`:
```json
{
  "SmartSheet": {
    "AccessToken": "your-smartsheet-api-token"
  }
}
```

### 8. Testing
1. Build: `dotnet build src/ShopBoss.Web/ShopBoss.Web.csproj` ✅
2. Run: `dotnet run --project src/ShopBoss.Web`
3. Navigate to: `http://localhost:5000/SmartSheetMigration`

## Architecture Notes
- **Completely Isolated**: Zero changes to existing ShopBoss functionality
- **Manual Process**: User reviews and approves all data
- **Simple Error Handling**: Basic try/catch blocks
- **MVP Approach**: Just enough to explore SmartSheet data and test imports

## Success Criteria
- [x] Build without errors
- [ ] View SmartSheet workspace lists  
- [ ] Display sheet details (attachments, comments)
- [ ] Successfully import a project with timeline
- [ ] Download and attach files from SmartSheet