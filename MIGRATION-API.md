# Migration API Usage Guide

The Migration API is a permanent feature in ShopBoss that allows interactive data migration between different database schemas. This is especially useful when schema changes occur during development.

## Configuration

Enable the Migration API in your environment:

**Development**: Already enabled in `appsettings.Development.json`
**Production**: Set `EnableMigrationApi: true` in configuration (disabled by default)

## Basic Workflow

1. **Deploy new build** with updated schema (empty database)
2. **Place old database** in accessible location (e.g., `shopboss-old.db`)
3. **Use Migration API** to explore and copy data interactively

## API Endpoints

### 1. Connect to Old Database
```bash
curl -X POST http://localhost:5000/api/migration/connect \
  -H "Content-Type: application/json" \
  -d '{"DatabasePath": "/path/to/shopboss-old.db"}'
```

### 2. Compare Schemas
```bash
curl http://localhost:5000/api/migration/schema/compare
```
Returns:
- `OldTables`: Tables in old database
- `NewTables`: Tables in new database  
- `CommonTables`: Tables in both
- `MissingInNew`: Tables only in old
- `NewTablesOnly`: Tables only in new

### 3. Examine Table Schema
```bash
# Both schemas
curl http://localhost:5000/api/migration/schema/Projects

# Just old schema
curl http://localhost:5000/api/migration/schema/Projects?database=old

# Just new schema  
curl http://localhost:5000/api/migration/schema/Projects?database=new
```

### 4. Query Old Database Data
```bash
curl -X POST http://localhost:5000/api/migration/query \
  -H "Content-Type: application/json" \
  -d '{"Sql": "SELECT * FROM Projects LIMIT 5", "Limit": 100}'
```

**Security**: Only SELECT and PRAGMA statements allowed

### 5. Copy Data with Field Mapping
```bash
curl -X POST http://localhost:5000/api/migration/copy \
  -H "Content-Type: application/json" \
  -d '{
    "TableName": "Projects",
    "ClearTargetFirst": true,
    "FieldMappings": {
      "Id": "Id",
      "ProjectId": "ProjectId", 
      "ProjectName": "ProjectName",
      "BidRequestDate": "BidRequestDate",
      "ProjectAddress": "ProjectAddress",
      "SmartSheetId": "SmartSheetId"
    }
  }'
```

### 6. Validate Migration
```bash
curl http://localhost:5000/api/migration/validate
```
Returns row counts for all tables: old vs new

## Example Migration Session

```bash
# 1. Connect to old database
curl -X POST http://localhost:5000/api/migration/connect \
  -d '{"DatabasePath": "./shopboss-old.db"}'

# 2. Compare schemas
curl http://localhost:5000/api/migration/schema/compare

# 3. Examine specific table differences
curl http://localhost:5000/api/migration/schema/Projects

# 4. Check data in old table
curl -X POST http://localhost:5000/api/migration/query \
  -d '{"Sql": "SELECT Id, ProjectName, SmartSheetId FROM Projects"}'

# 5. Copy Projects table with field mapping
curl -X POST http://localhost:5000/api/migration/copy \
  -d '{
    "TableName": "Projects", 
    "ClearTargetFirst": true,
    "FieldMappings": {
      "Id": "Id",
      "ProjectName": "ProjectName", 
      "SmartSheetId": "SmartSheetId"
    }
  }'

# 6. Validate the copy
curl http://localhost:5000/api/migration/validate
```

## Field Mapping

When schemas differ, use `FieldMappings` to map old field names to new ones:

```json
{
  "FieldMappings": {
    "OldFieldName": "NewFieldName",
    "ProjectId": "ProjectId", 
    "ProjectName": "ProjectName"
  }
}
```

- **Keys**: Old database field names
- **Values**: New database field names
- Fields not in mapping are ignored
- New fields not mapped get default/null values

## Safety Features

- **Read-only** access to old database
- **Configuration gated** (disabled by default)
- **Comprehensive logging** of all operations
- **Error handling** with detailed feedback
- **Progress tracking** for large tables

## Troubleshooting

- **403 Forbidden**: Migration API disabled in configuration
- **400 Bad Request**: No old database connected - use `/connect` first
- **500 Internal Error**: Check logs for detailed error messages
- **Field mapping errors**: Verify field names match exactly

This API turns database migrations from a one-time hack into a permanent, reusable solution for all future schema evolution.