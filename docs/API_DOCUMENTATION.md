# ShopBoss API Documentation

## API Endpoints & Controllers

### AdminController - Central Management Station
Base Route: `/Admin`

#### Views
- `GET /Admin` - Main dashboard
- `GET /Admin/Details/{id}` - Work order details view
- `GET /Admin/TreeView/{id}` - Hierarchical tree view
- `GET /Admin/Settings` - System settings page
- `GET /Admin/SystemHealth` - Health monitoring dashboard

#### Work Order Management
- `POST /Admin/ToggleArchiveStatus` - Archive/unarchive work order
  - Body: `{ workOrderId: int }`
  - Returns: JSON with success status

- `POST /Admin/DeleteWorkOrder` - Delete work order
  - Body: `{ workOrderId: int }`
  - Returns: JSON with deletion stats

#### Import Operations
- `POST /Admin/Import` - Import SDF file
  - Form data: `file` (multipart)
  - Returns: Redirects to TreeView

- `GET /Admin/GetRecentImports` - Get import history
  - Returns: JSON array of recent imports

#### Storage Management
- `GET /Admin/StorageOverview` - Storage rack overview
- `POST /Admin/UpdateBinStatus` - Update bin status
  - Body: `{ binId: int, status: string }`

### ImportController - File Import API
Base Route: `/api/Import`

- `POST /api/Import/upload` - Upload and process SDF file
  - Form data: `file` (multipart)
  - Returns: JSON with workOrderId and import stats
  - SignalR: Sends progress updates to ImportProgressHub

### CncController - Cutting Station
Base Route: `/Cnc`

#### Views
- `GET /Cnc` - CNC station interface

#### Operations
- `POST /Cnc/ProcessBarcode` - Process nest sheet scan
  - Body: `{ barcode: string }`
  - Returns: JSON with nest sheet details and parts
  - Updates part status to "Cut"
  - SignalR: Broadcasts to cnc-station group

### SortingController - Sorting Station
Base Route: `/Sorting`

#### Views
- `GET /Sorting` - Sorting station interface
- `GET /Sorting/BinMap` - Visual bin map

#### Operations
- `POST /Sorting/ProcessBarcode` - Process part barcode
  - Body: `{ barcode: string }`
  - Returns: JSON with part details and suggested bins

- `POST /Sorting/AssignBin` - Assign part to bin
  - Body: `{ partId: int, binId: int }`
  - Returns: JSON with assignment result
  - SignalR: Updates sorting-station group

- `GET /Sorting/GetAvailableBins` - Get available bins
  - Query: `?partId={id}`
  - Returns: JSON array of available bins

### AssemblyController - Assembly Station
Base Route: `/Assembly`

#### Views
- `GET /Assembly` - Assembly station interface

#### Operations
- `POST /Assembly/ProcessBarcode` - Process product barcode
  - Body: `{ barcode: string }`
  - Returns: JSON with product assembly status

- `POST /Assembly/CompleteAssembly` - Mark product assembled
  - Body: `{ productId: int }`
  - Returns: JSON with completion status
  - SignalR: Updates assembly-station group

- `GET /Assembly/GetPartStatus/{productId}` - Get part readiness
  - Returns: JSON with part status breakdown

### ShippingController - Shipping Station
Base Route: `/Shipping`

#### Views
- `GET /Shipping` - Shipping station interface

#### Operations
- `GET /Shipping/GetShippableOrders` - Get ready orders
  - Returns: JSON array of shippable work orders

- `POST /Shipping/ShipOrder` - Complete shipping
  - Body: `{ workOrderId: int }`
  - Returns: JSON with shipping result
  - Archives order and clears storage
  - SignalR: Updates shipping-station group

### TreeViewController - Hierarchical Data API
Base Route: `/api/TreeView`

- `GET /api/TreeView/LoadChildren` - Load tree node children
  - Query: `?nodeId={id}&nodeType={type}`
  - Returns: JSON array of child nodes
  - Node types: WorkOrder, Product, Subassembly

### AuditController - Audit Trail API
Base Route: `/api/Audit`

- `GET /api/Audit/GetTrail` - Get audit entries
  - Query: `?entityType={type}&entityId={id}&startDate={date}&endDate={date}`
  - Returns: JSON array of audit entries

- `GET /api/Audit/GetUserActivity` - Get user activity
  - Query: `?userId={id}&date={date}`
  - Returns: JSON array of user actions

## SignalR Hubs

### StatusHub
Endpoint: `/statusHub`

#### Methods
- `SendStationUpdate(station, message)` - Send station-specific update
- `SendWorkOrderUpdate(workOrderId, message)` - Send work order update
- `JoinStationGroup(station)` - Join station group
- `LeaveStationGroup(station)` - Leave station group

#### Client Events
- `ReceiveStationUpdate` - Station status change
- `ReceiveWorkOrderUpdate` - Work order status change
- `ReceiveSystemAlert` - System-wide alerts

### ImportProgressHub
Endpoint: `/importProgressHub`

#### Methods
- `StartImport(fileName)` - Initialize import tracking
- `UpdateProgress(percent, message)` - Update import progress
- `CompleteImport(workOrderId, stats)` - Import complete
- `ReportError(error)` - Report import error

#### Client Events
- `ReceiveProgress` - Import progress update
- `ImportComplete` - Import finished
- `ImportError` - Import failed

## Common Response Formats

### Success Response
```json
{
  "success": true,
  "message": "Operation completed",
  "data": { ... }
}
```

### Error Response
```json
{
  "success": false,
  "message": "Error description",
  "errors": ["Error 1", "Error 2"]
}
```

### Import Result
```json
{
  "workOrderId": 123,
  "productCount": 10,
  "partCount": 150,
  "nestSheetCount": 25,
  "processingTime": "0.2s"
}
```

### Part Status
```json
{
  "partId": 456,
  "barcode": "ABC123",
  "status": "Cut",
  "statusDateTime": "2025-07-27T10:30:00",
  "binLocation": "A01",
  "parentProduct": "Kitchen Cabinet"
}
```

## Authentication & Authorization
- Currently uses Windows Authentication
- Station-based access control planned
- All endpoints require authentication

## Error Handling
- HTTP 400: Bad Request (validation errors)
- HTTP 404: Not Found (entity not found)
- HTTP 409: Conflict (duplicate operations)
- HTTP 500: Internal Server Error
- All errors include descriptive messages

## Rate Limiting
- Barcode scanning: 30-second cooldown per barcode
- Import operations: One concurrent import per system
- No other rate limits currently implemented