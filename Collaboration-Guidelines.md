# Collaboration Guidelines

## 1 · Roles & Responsibilities

| Role                  | Primary Interface      | Responsibilities                                                    |
|-----------------------|------------------------|---------------------------------------------------------------------|
| **Product Owner**     | Chat / Canvas          | Describe tasks, supply sample data, accept finished work           |
| **Owner's Assistant** | Chat / Canvas          | Review codebase, turn discussion into actionable prompts           |
| **AI Code Agent**     | Terminal / Git commits | Implement code, log progress, maintain documentation               |

---

## 2 · Repository Layout

```
shopboss-v2/
├── src/
│   └── ShopBoss.Web/              # Main ASP.NET Core application
├── docs/
│   ├── requirements/              # Business and technical requirements
│   ├── architecture/              # System design and data models
│   └── api/                       # API documentation
├── tools/
│   └── importer/                  # SDF data importer tool
├── tests/                         # Unit and integration tests
├── README.md                      # Project overview and quickstart
├── Requirements.md                # Domain-specific requirements
├── Collaboration-Guidelines.md    # This file
└── Worklog.md                     # Chronological log + prompts
```

---

## 3 · Task Workflow

### Standard Development Cycle:
1. **Owner / OA (chat only)** – Draft the next-task prompt with clear specifications
2. **AI Code Agent** – Pull repo, append prompt to `Worklog.md`, commit with `task:` prefix
3. **AI Code Agent** – Implement code, tests, and documentation
4. **AI Code Agent** – Add "Status: Completed" in the same prompt block, commit with `done:` prefix
5. **AI Code Agent** – Update project status and create handoff documentation

### Commit Message Standards:
```
task: [Phase X] - [Brief Description]
done: [Phase X] - [Brief Description] - [Agent Name]
```

---

## 4 · Resilience & Restart Rules

### Core Principles:
1. **Everything committed** — prompts, code, completion notes—all in `main`
2. **Linear history** — commit straight to `main`; revert bad commits with `git revert`
3. **Single-file log** — `Worklog.md` is the canonical timeline; read bottom-up to catch up
4. **One-command demo** — after fresh clone, the command in the latest prompt must succeed
5. **Rollback protocol** — if main breaks, revert and add "Rollback" entry explaining why

### Git Workflow Requirements:
```bash
# Every agent MUST follow this complete sequence:
git add .
git commit -m "[appropriate message]"
git push origin main
```

**Critical**: Local commits are invisible until pushed. Always push immediately after committing.

---

## 5 · Multi-Agent Coordination

### Handoff Documentation:
- Always read `Worklog.md` and any existing handoff logs before starting
- Document blockers, incomplete work, and next steps clearly
- Create specific guidance for the next agent
- Update project status files with current completion percentage

### Parallel Development Rules:
- **Core files** (Program.cs, .csproj, DbContext): Single owner only
- **Feature areas**: Different agents can own different functional areas
- **Documentation**: Agents can work on separate sections simultaneously
- **Always verify** no conflicts before committing

---

## 6 · Technology Stack Standards

### Required Technologies:
- **Framework**: ASP.NET Core 8.0 MVC
- **Database**: Entity Framework Core 9.0.0 with SQLite
- **UI**: Bootstrap 5 with responsive design
- **Real-time**: SignalR for progress updates
- **Architecture**: MVC pattern with service layer

### Code Quality Standards:
- Follow C# naming conventions and best practices
- Use dependency injection for services
- Implement proper error handling and logging
- Write unit tests for business logic
- Document public APIs and complex algorithms

---

## 7 · ShopBoss-Specific Guidelines

### Data Integration:
- **Preserve Microvellum IDs** exactly as imported (no modifications)
- **Maintain hierarchy** - respect parent-child relationships
- **Handle dimensions** - all measurements in millimeters
- **Support product numbers** - separate field from product names

### Import Process:
- **External process execution** - importer runs as separate x86 process
- **Progress tracking** - 2-3 minute import requires real-time updates
- **Error handling** - graceful failure with clear user messaging
- **Temporary file cleanup** - remove SDF files after processing

### Business Logic:
- **Selective import** - allow partial work order imports
- **Validation rules** - enforce parent-child selection logic
- **Duplicate prevention** - check existing Microvellum IDs
- **Audit trail** - track what was imported when and by whom

---

## 8 · Quick-Start Checklist (Phase Handoff)

### Before Starting Any Phase:
1. **Pull latest changes**: `git pull origin main`
2. **Read documentation**: Review README.md, Requirements.md, and Worklog.md
3. **Check project status**: Understand current completion state
4. **Verify environment**: Ensure build succeeds (`dotnet build`)

### After Completing Any Phase:
1. **Test thoroughly**: Verify all functionality works as specified
2. **Update documentation**: Reflect any changes in architecture or requirements
3. **Commit with standards**: Follow git workflow requirements exactly
4. **Create handoff notes**: Document completion, issues, and next steps
5. **Update project status**: Mark phase as complete with verification details

---

## 9 · Emergency Procedures

### If Build Fails:
1. Check for missing NuGet packages: `dotnet restore`
2. Verify database migrations: `dotnet ef database update`
3. Review recent commits for breaking changes
4. If unsolvable, revert to last working commit

### If Agent Gets Stuck:
1. Document the specific blocker in Worklog.md
2. Provide detailed error messages and steps attempted
3. Suggest alternative approaches or request guidance
4. Do not proceed with broken/incomplete implementations

### If Requirements Change:
1. Update Requirements.md with new specifications
2. Document impact on existing code and timeline
3. Create migration plan for affected features
4. Get approval before making breaking changes

---

## 10 · Success Metrics

### Definition of Done (Each Phase):
- ✅ All specified functionality implemented and tested
- ✅ Code builds without warnings or errors
- ✅ Documentation updated to reflect changes
- ✅ Git workflow followed correctly (committed and pushed)
- ✅ Handoff documentation created for next phase
- ✅ No breaking changes to existing functionality

### Project Success Criteria:
- **Functional**: Complete SDF import with selective data import
- **Technical**: Modern web app with real-time progress tracking
- **Quality**: Responsive design suitable for shop floor tablets
- **Maintainable**: Clear architecture with proper separation of concerns