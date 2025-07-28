# ShopBoss System Architecture Documentation

## Overview
ShopBoss v2 is a modern shop floor tracking system designed to manage millwork manufacturing workflow from CNC cutting through assembly and shipping. It features hierarchical data import from Microvellum SDF files, real-time status tracking, and comprehensive audit trails.

## Technology Stack
- **Framework**: ASP.NET Core 8.0 MVC
- **Database**: Entity Framework Core 9.0.0 with SQLite
- **Real-time**: SignalR for live status updates
- **Frontend**: Bootstrap 5 with vanilla JavaScript
- **Import**: External x86 process (FastSdfReader) for SDF conversion

## System Architecture Diagram

```mermaid
graph TB
    subgraph "External Systems"
        MV[Microvellum]
        SDF[SDF Files]
    end
    
    subgraph "Import Layer"
        FSR[Fast SDF Reader<br/>x86 Process]
        IMP[Import Service]
        MAP[Column Mapping]
    end
    
    subgraph "Data Layer"
        DB[(SQLite Database)]
        EF[Entity Framework Core]
        CTX[ShopBossDbContext]
    end
    
    subgraph "Domain Models"
        WO[WorkOrder]
        PRD[Product]
        PRT[Part]
        SUB[Subassembly]
        HDW[Hardware]
        NST[NestSheet]
        DPR[DetachedProduct]
    end
    
    subgraph "Business Services"
        AUD[Audit Service]
        SRT[Sorting Service]
        FLT[Filter Service]
        SHP[Shipping Service]
        BKP[Backup Service]
        MON[Health Monitor]
    end
    
    subgraph "Storage System"
        RCK[Storage Rack]
        BIN[Bin]
        RSV[Reservation]
    end
    
    subgraph "Web Layer"
        subgraph "Controllers/Stations"
            ADM[Admin Station]
            CNC[CNC Station]
            SOR[Sorting Station]
            ASM[Assembly Station]
            SHI[Shipping Station]
        end
        
        subgraph "Real-time"
            SHB[Status Hub]
            IHB[Import Hub]
        end
    end
    
    subgraph "Frontend"
        VWS[Razor Views]
        JS[JavaScript]
        SCN[Scanner Module]
        TRE[Tree View]
    end
    
    MV --> SDF
    SDF --> FSR
    FSR --> IMP
    IMP --> MAP
    MAP --> CTX
    CTX --> DB
    EF --> CTX
    
    CTX --> WO
    WO --> PRD
    PRD --> PRT
    PRD --> SUB
    PRD --> HDW
    WO --> NST
    WO --> DPR
    
    AUD --> DB
    SRT --> CTX
    FLT --> CTX
    SHP --> CTX
    BKP --> DB
    MON --> DB
    
    RCK --> BIN
    BIN --> PRT
    
    ADM --> AUD
    ADM --> CTX
    CNC --> CTX
    SOR --> SRT
    ASM --> CTX
    SHI --> SHP
    
    ADM --> SHB
    CNC --> SHB
    SOR --> SHB
    ASM --> SHB
    SHI --> SHB
    
    SHB --> JS
    IHB --> JS
    JS --> SCN
    JS --> TRE
```

## Data Flow Diagram

```mermaid
sequenceDiagram
    participant User
    participant Admin
    participant Import
    participant DB
    participant CNC
    participant Sort
    participant Assembly
    participant Ship
    participant SignalR
    
    User->>Admin: Upload SDF File
    Admin->>Import: Process File
    Import->>Import: Fast SDF Reader
    Import->>DB: Create WorkOrder
    Import->>SignalR: Progress Updates
    SignalR->>User: Real-time Status
    
    User->>CNC: Scan NestSheet
    CNC->>DB: Update Parts (Cut)
    CNC->>SignalR: Status Update
    SignalR->>Sort: Part Ready
    
    User->>Sort: Scan Part
    Sort->>DB: Assign Bin
    Sort->>SignalR: Status Update
    
    User->>Assembly: Scan Product
    Assembly->>DB: Update Status
    Assembly->>SignalR: Progress Update
    
    User->>Ship: Complete Order
    Ship->>DB: Mark Shipped
    Ship->>SignalR: Complete
```

## Domain Model Relationships

```mermaid
erDiagram
    WorkOrder ||--o{ Product : contains
    WorkOrder ||--o{ DetachedProduct : contains
    WorkOrder ||--o{ NestSheet : contains
    WorkOrder ||--o{ Hardware : contains
    
    Product ||--o{ Part : contains
    Product ||--o{ Subassembly : contains
    Product ||--o{ Hardware : contains
    
    Subassembly ||--o{ Part : contains
    Subassembly ||--o{ Subassembly : nested
    
    DetachedProduct ||--o{ Part : contains
    
    NestSheet ||--o{ Part : arranges
    
    StorageRack ||--o{ Bin : contains
    Bin ||--o| Part : stores
    Bin ||--o| Product : stores
    Bin ||--o| WorkOrder : reserved
    
    AuditTrail }o--|| WorkOrder : tracks
    AuditTrail }o--|| Part : tracks
```

## Status Workflow

```mermaid
stateDiagram-v2
    [*] --> Pending: Import
    Pending --> Cut: CNC Scan
    Cut --> Sorted: Sort Station
    Sorted --> Assembled: Assembly
    Assembled --> Shipped: Shipping
    Shipped --> [*]
    
    note right of Cut: Parts marked as cut
    note right of Sorted: Parts assigned to bins
    note right of Assembled: Product complete
    note right of Shipped: Order fulfilled
```

## Station Workflows

### 1. Import Workflow
```mermaid
flowchart LR
    A[SDF File] --> B[Fast SDF Reader]
    B --> C[Parse Data]
    C --> D[Map Columns]
    D --> E[Create Entities]
    E --> F[Save to DB]
    F --> G[SignalR Updates]
```

### 2. CNC Station Workflow
```mermaid
flowchart LR
    A[Scan Barcode] --> B{Valid NestSheet?}
    B -->|Yes| C[Mark Parts Cut]
    B -->|No| D[Show Error]
    C --> E[Update Status]
    E --> F[SignalR Broadcast]
    F --> G[Update UI]
```

### 3. Sorting Station Workflow
```mermaid
flowchart LR
    A[Scan Part] --> B{Part Cut?}
    B -->|Yes| C[Find Available Bin]
    B -->|No| D[Show Error]
    C --> E[Assign to Bin]
    E --> F[Update Status]
    F --> G[SignalR Update]
```

### 4. Assembly Station Workflow
```mermaid
flowchart LR
    A[Scan Product] --> B{All Parts Ready?}
    B -->|Yes| C[Mark Assembled]
    B -->|No| D[Show Missing]
    C --> E[Update Status]
    E --> F[SignalR Update]
```

### 5. Shipping Station Workflow
```mermaid
flowchart LR
    A[Select Order] --> B{Products Ready?}
    B -->|Yes| C[Mark Shipped]
    B -->|No| D[Show Pending]
    C --> E[Archive Order]
    E --> F[Clear Storage]
    F --> G[SignalR Complete]
```

## Key Components

### Services Layer
- **FastImportService**: High-performance SDF import (0.2s processing)
- **WorkOrderImportService**: Transforms SDF data to domain entities
- **AuditTrailService**: Comprehensive operation logging
- **SortingRuleService**: Part sorting logic
- **BackupService**: Automated backup management
- **SystemHealthMonitor**: Performance and health tracking

### Storage Management
- **StorageRack Types**:
  - Standard (general parts)
  - DoorsAndDrawerFronts
  - AdjustableShelves
  - Hardware
  - Cart (mobile storage)
- **Bin Status**: Empty, Partial, Full, Reserved, Blocked
- **Grid System**: Row/Column addressing (e.g., A01, B02)

### Real-time Communication
- **StatusHub**: Cross-station updates
  - Station groups (cnc-station, sorting-station, etc.)
  - WorkOrder-specific groups
  - Broadcast capabilities
- **ImportProgressHub**: Import progress tracking

### Frontend Architecture
- **universal-scanner.js**: Barcode scanning with cooldown
- **WorkOrderTreeView.js**: Hierarchical data visualization
- **SignalR Integration**: Real-time UI updates
- **Event-driven**: Loose coupling between components

## Security & Audit
- Complete audit trail for all operations
- Session and IP tracking
- User action logging
- Status change history
- Timestamp tracking at each stage

## Performance Optimizations
- Fast SDF import via external process
- Efficient Entity Framework queries
- SignalR for targeted updates
- Background services for maintenance
- Automated backup compression

## Deployment Architecture
- Windows Service compatible
- SQLite database (portable)
- Self-hosted web server
- No external dependencies
- Single-folder deployment