# SmartSheet Embed SDK Guide

## Overview

As of August 2025, SmartSheet does **not** have a dedicated "Embed SDK" separate from their API SDK. Instead, SmartSheet provides two primary integration approaches:

1. **SmartSheet JavaScript API SDK** - Server-side Node.js SDK for programmatic access
2. **iframe Publishing** - Client-side embedding of published sheets

## SmartSheet JavaScript API SDK

### Purpose
- Server-side Node.js SDK for accessing SmartSheet API 2.0
- Designed for backend services, not client-side embedding
- Handles authentication, API calls, and data operations

### Installation
```bash
npm install smartsheet
```

### Key Features
- Promise-based and callback-based API interactions
- Support for Node.js 6.x and later (as of 2025)
- Built-in retry logic with exponential backoff
- Custom environment support (US, EU, Gov)

### Basic Usage
```javascript
const smartsheet = require('smartsheet').createClient({
  accessToken: 'YOUR_ACCESS_TOKEN',
  logLevel: 'info',
  maxRetryDurationSeconds: 15
});

// List all sheets
const sheets = await smartsheet.sheets.listSheets();

// Get sheet with data
const sheet = await smartsheet.sheets.getSheet({
  id: sheetId,
  include: 'attachments,discussions'
});
```

### Differences from a True Embed SDK
| Feature | API SDK | Hypothetical Embed SDK |
|---------|---------|------------------------|
| **Purpose** | Backend API operations | Frontend grid embedding |
| **Environment** | Node.js server | Browser JavaScript |
| **Authentication** | Access tokens | OAuth/session-based |
| **UI Components** | None | Interactive grid widgets |
| **Real-time Updates** | Manual polling | WebSocket/SSE |
| **User Interaction** | Programmatic only | Direct user editing |

## iframe Publishing Approach

### How It Works
SmartSheet's primary embedding method uses published sheets displayed in iframes:

1. **Publish Sheet**: Generate public URL and embed code
2. **iframe Embedding**: Display sheet in your application
3. **Dynamic Updates**: Published content stays current

### Publishing Process
```javascript
// 1. Publish via SmartSheet UI or API
// 2. Get embed code:
<iframe 
  width="1000" 
  height="700" 
  frameborder="0" 
  src="https://publish.smartsheet.com/[unique_id]">
</iframe>
```

### Limitations of iframe Approach
- **Security**: Sheet must be published (publicly accessible)
- **Customization**: Limited styling and branding options
- **Authentication**: No user-specific permissions in iframe
- **Integration**: Minimal communication with parent application

## Key Architectural Implications

### For ShopBoss Integration

#### ✅ What Works Well
- **API SDK**: Perfect for syncing data between ShopBoss and SmartSheet
- **iframe Publishing**: Can display sheets in project detail cards
- **Webhook Integration**: Real-time notifications for changes

#### ⚠️ Challenges
- **No True Embed SDK**: No interactive grid components for custom applications
- **Authentication Complexity**: Published sheets vs secure API access
- **Limited Customization**: iframe content cannot be styled to match ShopBoss

### Recommended Hybrid Approach
1. **Use API SDK** for data synchronization and operations
2. **Use iframe publishing** for displaying interactive sheets
3. **Implement custom UI** for extracted/processed data
4. **Use webhooks** for real-time updates

## Alternative Solutions

### 1. Third-Party Grid Components
Consider using JavaScript grid libraries with SmartSheet data:
- **ag-Grid**: Enterprise-grade data grid
- **Luckysheet**: Open-source online spreadsheet
- **OnlyOffice**: Document/spreadsheet editor

### 2. Custom Implementation
Build custom grid using:
- SmartSheet API for data operations
- Modern grid library for UI
- WebSocket for real-time updates

## Conclusion

SmartSheet lacks a true client-side embed SDK as of August 2025. The iframe publishing approach is the closest equivalent but has significant limitations. For ShopBoss, a hybrid approach using the API SDK for data operations and iframe for sheet display, supplemented by custom UI components, will provide the best user experience.

## Next Steps
1. Evaluate iframe publishing limitations in ShopBoss context
2. Design hybrid architecture using API SDK + iframe
3. Plan custom UI components for enhanced functionality
4. Implement webhook system for real-time sync