# SmartSheet Modular Process Data Model

## Overview

This document defines the data model and implementation strategy for composable process modules in SmartSheet. Instead of rigid templates, this approach allows building bespoke workflows by combining reusable process chunks tailored to each job's specific requirements.

## Core Concepts

### Process Module Definition
A **Process Module** is a self-contained set of related tasks that can be combined with other modules to create a complete project workflow. Each module represents a logical grouping of activities that often occur together in the shop.

### Key Principles
1. **Modularity**: Each process chunk stands alone and has clear inputs/outputs
2. **Reusability**: Modules can be used across different project types
3. **Composability**: Modules can be combined in different sequences
4. **Flexibility**: Modules can be customized when added to projects
5. **Hierarchy**: Modules can contain sub-processes and dependencies

## SmartSheet Data Structure

### Column Schema
```csharp
public class ProcessSheetColumns
{
    // Core identification
    public const string TASK_NAME = "Task Name";
    public const string TASK_TYPE = "Task Type"; // Module, Task, Milestone, Note
    public const string MODULE_ID = "Module ID"; // Groups related tasks
    public const string SEQUENCE = "Sequence"; // Order within module
    
    // Process tracking
    public const string STATUS = "Status"; // Not Started, In Progress, Completed, Blocked
    public const string ASSIGNED_TO = "Assigned To";
    public const string PRIORITY = "Priority"; // Low, Normal, High, Critical
    
    // Scheduling
    public const string START_DATE = "Start Date";
    public const string DUE_DATE = "Due Date";
    public const string DURATION = "Duration"; // Days
    public const string DEPENDENCIES = "Dependencies"; // Row IDs or task names
    
    // Shop-specific
    public const string WORK_CENTER = "Work Center"; // CNC, Assembly, Finishing, etc.
    public const string SKILL_REQUIRED = "Skill Required";
    public const string ESTIMATED_HOURS = "Est. Hours";
    public const string ACTUAL_HOURS = "Actual Hours";
    
    // Process metadata
    public const string PROCESS_CATEGORY = "Process Category"; // Production, QA, Admin, Client
    public const string IS_MILESTONE = "Is Milestone";
    public const string IS_CRITICAL_PATH = "Critical Path";
    
    // Notes and references
    public const string NOTES = "Notes";
    public const string REFERENCE_DOCS = "Reference Docs";
}

public enum TaskType
{
    ModuleHeader,   // Header row identifying the process module
    Task,           // Individual work item
    Milestone,      // Key checkpoint or deliverable
    Note,           // Information or instruction
    Dependency,     // External dependency or wait state
    QualityCheck,   // Quality assurance checkpoint
    ClientAction    // Action required from client
}

public enum ProcessCategory
{
    Design,         // CAD, engineering, specifications
    Production,     // Manufacturing, assembly, fabrication  
    QualityAssurance, // Inspection, testing, approval
    Logistics,      // Shipping, delivery, installation
    Administrative, // Paperwork, invoicing, documentation
    ClientInteraction // Approvals, reviews, communication
}
```

### Row Structure Examples
```csharp
// Example: CNC Cutting Process Module
var cncModule = new ProcessModule
{
    ModuleId = "CNC-001",
    ModuleName = "CNC Cutting Process",
    Category = ProcessCategory.Production,
    Tasks = new[]
    {
        // Module header
        new ProcessTask
        {
            TaskName = "🔧 CNC Cutting Process",
            TaskType = TaskType.ModuleHeader,
            ModuleId = "CNC-001",
            Sequence = 0,
            ProcessCategory = ProcessCategory.Production,
            WorkCenter = "CNC",
            Notes = "Complete CNC cutting operations for project components"
        },
        
        // Individual tasks
        new ProcessTask
        {
            TaskName = "Review cut files and tooling",
            TaskType = TaskType.Task,
            ModuleId = "CNC-001", 
            Sequence = 1,
            EstimatedHours = 0.5,
            WorkCenter = "CNC",
            SkillRequired = "CNC Setup",
            Dependencies = "CAD files approved"
        },
        
        new ProcessTask
        {
            TaskName = "Set up CNC machine", 
            TaskType = TaskType.Task,
            ModuleId = "CNC-001",
            Sequence = 2,
            EstimatedHours = 1.0,
            WorkCenter = "CNC",
            SkillRequired = "CNC Setup"
        },
        
        new ProcessTask
        {
            TaskName = "Cut components",
            TaskType = TaskType.Task, 
            ModuleId = "CNC-001",
            Sequence = 3,
            EstimatedHours = 4.0,
            WorkCenter = "CNC", 
            SkillRequired = "CNC Operation",
            IsCriticalPath = true
        },
        
        new ProcessTask
        {
            TaskName = "Quality inspection",
            TaskType = TaskType.QualityCheck,
            ModuleId = "CNC-001",
            Sequence = 4,
            EstimatedHours = 0.5,
            WorkCenter = "CNC",
            SkillRequired = "Quality Control"
        },
        
        new ProcessTask
        {
            TaskName = "Components cut and approved",
            TaskType = TaskType.Milestone,
            ModuleId = "CNC-001", 
            Sequence = 5,
            IsMilestone = true,
            IsCriticalPath = true
        }
    }
};
```

## Process Module Library

### Standard Modules for Millwork Manufacturing

#### Design & Engineering Modules
```csharp
public static class DesignModules
{
    public static ProcessModule InitialDesign => new()
    {
        ModuleId = "DSN-001",
        ModuleName = "Initial Design & CAD",
        Category = ProcessCategory.Design,
        EstimatedDuration = TimeSpan.FromDays(2),
        Tasks = new[]
        {
            "🎨 Initial Design & CAD",
            "Review project specifications",
            "Create preliminary CAD drawings",
            "Material selection and sourcing",
            "Initial cost estimation",
            "📋 Design review meeting",
            "Design concept approved"
        }
    };
    
    public static ProcessModule DetailedDrawings => new()
    {
        ModuleId = "DSN-002", 
        ModuleName = "Detailed Engineering Drawings",
        Category = ProcessCategory.Design,
        Dependencies = new[] { "DSN-001" },
        Tasks = new[]
        {
            "📐 Detailed Engineering Drawings",
            "Create production drawings",
            "Generate cut lists", 
            "Create assembly drawings",
            "Hardware specifications",
            "Client approval drawings",
            "Engineering drawings approved"
        }
    };
}
```

#### Production Modules
```csharp
public static class ProductionModules  
{
    public static ProcessModule MaterialPrep => new()
    {
        ModuleId = "PROD-001",
        ModuleName = "Material Preparation", 
        Category = ProcessCategory.Production,
        WorkCenter = "Prep Area",
        Tasks = new[]
        {
            "📦 Material Preparation",
            "Receive and inspect materials",
            "Sort materials by project",
            "Pre-sand if required",
            "Mark materials for cutting",
            "Materials ready for production"
        }
    };
    
    public static ProcessModule CncCutting => new()
    {
        ModuleId = "PROD-002",
        ModuleName = "CNC Cutting Process",
        Category = ProcessCategory.Production, 
        WorkCenter = "CNC",
        Dependencies = new[] { "PROD-001" },
        Tasks = new[]
        {
            "🔧 CNC Cutting Process",
            "Review cut files and tooling", 
            "Set up CNC machine",
            "Cut components",
            "Quality inspection",
            "Components cut and approved"
        }
    };
    
    public static ProcessModule Assembly => new()
    {
        ModuleId = "PROD-003",
        ModuleName = "Assembly Process",
        Category = ProcessCategory.Production,
        WorkCenter = "Assembly",  
        Dependencies = new[] { "PROD-002" },
        Tasks = new[]
        {
            "🔨 Assembly Process",
            "Organize components for assembly",
            "Dry fit components",
            "Apply glue and fasteners", 
            "Clamp assemblies",
            "Clean up glue squeeze-out",
            "Assembly quality check",
            "Assemblies complete"
        }
    };
}
```

#### Quality & Finishing Modules
```csharp
public static class QualityModules
{
    public static ProcessModule PreFinishInspection => new()
    {
        ModuleId = "QA-001",
        ModuleName = "Pre-Finish Quality Inspection",
        Category = ProcessCategory.QualityAssurance,
        Tasks = new[]
        {
            "🔍 Pre-Finish Quality Inspection", 
            "Dimensional inspection",
            "Surface quality check",
            "Fit and finish review",
            "Document any defects",
            "Approve for finishing",
            "QA inspection complete"
        }
    };
    
    public static ProcessModule FinishingProcess => new()
    {
        ModuleId = "FINISH-001",
        ModuleName = "Finishing Process",
        Category = ProcessCategory.Production,
        WorkCenter = "Finishing",
        Dependencies = new[] { "QA-001" },
        Tasks = new[]
        {
            "🎨 Finishing Process",
            "Sand to specified grit",
            "Apply stain if required", 
            "Apply base coat",
            "Sand between coats",
            "Apply final finish coats",
            "Final inspection",
            "Finishing complete"
        }
    };
}
```

#### Client Interaction Modules
```csharp
public static class ClientModules
{
    public static ProcessModule ClientApproval => new()
    {
        ModuleId = "CLIENT-001",
        ModuleName = "Client Design Approval",
        Category = ProcessCategory.ClientInteraction,
        Tasks = new[]
        {
            "👥 Client Design Approval",
            "Prepare approval package",
            "Schedule client review meeting",
            "Present design to client",
            "Document client feedback", 
            "Revise design if needed",
            "Obtain signed approval",
            "Client approval received"
        }
    };
    
    public static ProcessModule FinalClientReview => new()
    {
        ModuleId = "CLIENT-002", 
        ModuleName = "Final Client Review",
        Category = ProcessCategory.ClientInteraction,
        Tasks = new[]
        {
            "👥 Final Client Review",
            "Schedule final inspection",
            "Client final walkthrough",
            "Address any punch list items",
            "Obtain final sign-off",
            "Project approved by client"
        }
    };
}
```

## Implementation Patterns

### Adding Process Modules to Projects
```csharp
public class ProcessModuleService
{
    private readonly SmartSheetWriteService _writeService;
    private readonly ILogger<ProcessModuleService> _logger;

    public async Task<List<long>> AddProcessModuleToProjectAsync(
        long sheetId, 
        ProcessModule module, 
        DateTime? startDate = null,
        string? assignedTo = null)
    {
        var rowIds = new List<long>();
        var currentDate = startDate ?? DateTime.Today;
        
        foreach (var task in module.Tasks)
        {
            var rowData = new Dictionary<long, object>
            {
                [GetColumnId("Task Name")] = task.TaskName,
                [GetColumnId("Task Type")] = task.TaskType.ToString(),
                [GetColumnId("Module ID")] = task.ModuleId,
                [GetColumnId("Sequence")] = task.Sequence,
                [GetColumnId("Status")] = "Not Started",
                [GetColumnId("Process Category")] = task.ProcessCategory.ToString(),
                [GetColumnId("Work Center")] = task.WorkCenter ?? "",
                [GetColumnId("Skill Required")] = task.SkillRequired ?? "",
                [GetColumnId("Est. Hours")] = task.EstimatedHours ?? 0,
                [GetColumnId("Priority")] = task.Priority?.ToString() ?? "Normal"
            };
            
            // Add dates for tasks (not headers or milestones)
            if (task.TaskType == TaskType.Task && task.EstimatedHours > 0)
            {
                rowData[GetColumnId("Start Date")] = currentDate.ToString("yyyy-MM-dd");
                rowData[GetColumnId("Due Date")] = currentDate.AddDays(task.EstimatedHours / 8).ToString("yyyy-MM-dd");
                currentDate = currentDate.AddDays(task.EstimatedHours / 8); // Move start date forward
            }
            
            // Add assignment
            if (!string.IsNullOrEmpty(assignedTo) && task.TaskType == TaskType.Task)
            {
                rowData[GetColumnId("Assigned To")] = assignedTo;
            }
            
            // Add dependencies
            if (task.Dependencies?.Any() == true)
            {
                rowData[GetColumnId("Dependencies")] = string.Join(", ", task.Dependencies);
            }
            
            // Add notes
            if (!string.IsNullOrEmpty(task.Notes))
            {
                rowData[GetColumnId("Notes")] = task.Notes;
            }
            
            var rowId = await _writeService.AddRowToSheetAsync(sheetId, rowData);
            if (rowId.HasValue)
            {
                rowIds.Add(rowId.Value);
            }
        }
        
        _logger.LogInformation("Added process module {ModuleName} with {TaskCount} tasks to sheet {SheetId}",
            module.ModuleName, module.Tasks.Count(), sheetId);
            
        return rowIds;
    }

    public async Task<bool> ComposeProjectWorkflowAsync(
        long sheetId,
        IEnumerable<ProcessModule> modules,
        ProjectWorkflowConfig config)
    {
        var currentDate = config.ProjectStartDate;
        var totalRowsAdded = 0;
        
        foreach (var module in modules)
        {
            // Check if module dependencies are met
            if (module.Dependencies?.Any() == true)
            {
                var dependenciesMet = await CheckModuleDependenciesAsync(sheetId, module.Dependencies);
                if (!dependenciesMet)
                {
                    _logger.LogWarning("Dependencies not met for module {ModuleName}, skipping", module.ModuleName);
                    continue;
                }
            }
            
            // Customize module based on project config
            var customizedModule = CustomizeModuleForProject(module, config);
            
            // Add module to sheet
            var rowIds = await AddProcessModuleToProjectAsync(
                sheetId, 
                customizedModule, 
                currentDate, 
                config.DefaultAssignee);
                
            totalRowsAdded += rowIds.Count;
            
            // Update current date based on module duration
            currentDate = currentDate.Add(customizedModule.EstimatedDuration);
            
            // Add buffer time between modules if specified
            if (config.ModuleBufferDays > 0)
            {
                currentDate = currentDate.AddDays(config.ModuleBufferDays);
            }
        }
        
        _logger.LogInformation("Composed workflow with {ModuleCount} modules, {TotalRows} total rows for sheet {SheetId}",
            modules.Count(), totalRowsAdded, sheetId);
            
        return totalRowsAdded > 0;
    }
    
    private ProcessModule CustomizeModuleForProject(ProcessModule module, ProjectWorkflowConfig config)
    {
        var customized = module.DeepCopy();
        
        // Apply project-specific customizations
        foreach (var task in customized.Tasks)
        {
            // Adjust estimated hours based on project complexity
            if (task.EstimatedHours.HasValue)
            {
                task.EstimatedHours *= config.ComplexityMultiplier;
            }
            
            // Override work center assignments if specified
            if (config.WorkCenterOverrides.TryGetValue(task.WorkCenter ?? "", out var newWorkCenter))
            {
                task.WorkCenter = newWorkCenter;
            }
            
            // Apply skill level adjustments
            if (config.SkillLevelAdjustments.TryGetValue(task.SkillRequired ?? "", out var adjustment))
            {
                task.EstimatedHours *= adjustment;
            }
        }
        
        return customized;
    }
}

public class ProjectWorkflowConfig
{
    public DateTime ProjectStartDate { get; set; } = DateTime.Today;
    public string? DefaultAssignee { get; set; }
    public double ComplexityMultiplier { get; set; } = 1.0;
    public int ModuleBufferDays { get; set; } = 0;
    public Dictionary<string, string> WorkCenterOverrides { get; set; } = new();
    public Dictionary<string, double> SkillLevelAdjustments { get; set; } = new();
    public List<string> SkippedModules { get; set; } = new();
    public Dictionary<string, object> CustomFields { get; set; } = new();
}
```

### Process Module Templates
```csharp
public static class ProjectTemplates
{
    public static List<ProcessModule> StandardCabinetProject => new()
    {
        DesignModules.InitialDesign,
        DesignModules.DetailedDrawings, 
        ClientModules.ClientApproval,
        ProductionModules.MaterialPrep,
        ProductionModules.CncCutting,
        ProductionModules.Assembly,
        QualityModules.PreFinishInspection,
        QualityModules.FinishingProcess,
        ClientModules.FinalClientReview,
        LogisticsModules.PackagingAndDelivery
    };
    
    public static List<ProcessModule> CustomMillworkProject => new()
    {
        DesignModules.InitialDesign,
        DesignModules.DetailedDrawings,
        DesignModules.PrototypeCreation, // Custom complex projects may need prototyping
        ClientModules.ClientApproval,
        ProductionModules.MaterialPrep,
        ProductionModules.SpecialtyMachining, // Custom processes
        ProductionModules.HandCrafting, // Manual work
        ProductionModules.Assembly,
        QualityModules.ExtensiveQA, // More rigorous QA
        QualityModules.FinishingProcess,
        InstallationModules.SitePreparation, // On-site work
        InstallationModules.Installation,
        ClientModules.FinalClientReview
    };
    
    public static List<ProcessModule> RepairProject => new()
    {
        DiagnosticModules.InitialAssessment,
        ClientModules.RepairQuoteApproval,
        ProductionModules.Disassembly,
        ProductionModules.RepairWork,
        QualityModules.RepairInspection,
        QualityModules.Refinishing,
        LogisticsModules.DeliveryAndInstallation
    };
}
```

## Usage in ShopBoss Integration

### Project Creation with Module Selection
```csharp
public class ProjectCreationService
{
    public async Task<bool> CreateProjectWithModulesAsync(
        Project project,
        ProjectType projectType,
        ProjectComplexity complexity)
    {
        // Create SmartSheet from base template
        var sheetId = await CreateProjectSheetAsync(project);
        if (!sheetId.HasValue) return false;
        
        // Select appropriate modules based on project type
        var modules = SelectModulesForProject(projectType, complexity);
        
        // Configure workflow parameters
        var config = new ProjectWorkflowConfig
        {
            ProjectStartDate = project.StartDate ?? DateTime.Today,
            ComplexityMultiplier = GetComplexityMultiplier(complexity),
            DefaultAssignee = project.ProjectManager
        };
        
        // Compose and add workflow
        return await _processModuleService.ComposeProjectWorkflowAsync(
            sheetId.Value, modules, config);
    }
    
    private List<ProcessModule> SelectModulesForProject(ProjectType type, ProjectComplexity complexity)
    {
        return type switch
        {
            ProjectType.StandardCabinets => ProjectTemplates.StandardCabinetProject,
            ProjectType.CustomMillwork => ProjectTemplates.CustomMillworkProject,
            ProjectType.Repair => ProjectTemplates.RepairProject,
            _ => ProjectTemplates.StandardCabinetProject
        };
    }
}
```

This modular process design enables ShopBoss to create truly flexible project workflows that reflect the realities of job-shop manufacturing while maintaining the structure needed for effective project management.