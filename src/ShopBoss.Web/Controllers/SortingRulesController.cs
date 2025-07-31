using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopBoss.Web.Data;
using ShopBoss.Web.Models;
using ShopBoss.Web.Services;

namespace ShopBoss.Web.Controllers;

public class SortingRulesController : Controller
{
    private readonly ShopBossDbContext _context;
    private readonly ILogger<SortingRulesController> _logger;
    private readonly SortingRuleService _sortingRuleService;

    public SortingRulesController(
        ShopBossDbContext context, 
        ILogger<SortingRulesController> logger,
        SortingRuleService sortingRuleService)
    {
        _context = context;
        _logger = logger;
        _sortingRuleService = sortingRuleService;
    }

    // GET /SortingRules
    public async Task<IActionResult> Index(string search = "", RackType? filterType = null, bool? activeOnly = null)
    {
        try
        {
            var query = _context.SortingRules.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Name.Contains(search) || r.Keywords.Contains(search));
            }

            // Apply rack type filter
            if (filterType.HasValue)
            {
                query = query.Where(r => r.TargetRackType == filterType.Value);
            }

            // Apply active status filter
            if (activeOnly.HasValue)
            {
                query = query.Where(r => r.IsActive == activeOnly.Value);
            }

            var rules = await query
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync();

            ViewBag.SearchTerm = search;
            ViewBag.FilterType = filterType;
            ViewBag.ActiveOnly = activeOnly;
            ViewBag.RackTypes = Enum.GetValues<RackType>().ToList();

            return View(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sorting rules");
            TempData["ErrorMessage"] = "An error occurred while loading sorting rules.";
            return View(new List<SortingRule>());
        }
    }

    // GET /SortingRules/Create
    public IActionResult Create()
    {
        var model = new SortingRule
        {
            IsActive = true,
            Priority = GetNextPriority()
        };

        ViewBag.RackTypes = Enum.GetValues<RackType>().ToList();
        return View(model);
    }

    // POST /SortingRules/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SortingRule model)
    {
        try
        {
            if (ModelState.IsValid)
            {
                // Validate priority uniqueness
                if (await _context.SortingRules.AnyAsync(r => r.Priority == model.Priority && r.IsActive))
                {
                    ModelState.AddModelError("Priority", "Priority must be unique among active rules.");
                }

                // Validate keywords
                if (string.IsNullOrWhiteSpace(model.Keywords))
                {
                    ModelState.AddModelError("Keywords", "Keywords are required.");
                }

                if (ModelState.IsValid)
                {
                    model.CreatedDate = DateTime.Now;
                    _context.SortingRules.Add(model);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created sorting rule '{RuleName}' with priority {Priority} targeting {RackType}", 
                        model.Name, model.Priority, model.TargetRackType);

                    return Json(new { success = true, message = $"Sorting rule '{model.Name}' created successfully." });
                }
            }

            // Return validation errors
            var errors = ModelState.Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return Json(new { success = false, message = "Validation failed.", errors = errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sorting rule");
            return Json(new { success = false, message = "An error occurred while creating the sorting rule." });
        }
    }

    // GET /SortingRules/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var rule = await _context.SortingRules.FindAsync(id);
            if (rule == null)
            {
                TempData["ErrorMessage"] = "Sorting rule not found.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RackTypes = Enum.GetValues<RackType>().ToList();
            return View(rule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading sorting rule {RuleId} for editing", id);
            TempData["ErrorMessage"] = "An error occurred while loading the sorting rule.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST /SortingRules/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SortingRule model)
    {
        if (id != model.Id)
        {
            return Json(new { success = false, message = "Invalid sorting rule ID." });
        }

        try
        {
            if (ModelState.IsValid)
            {
                // Validate priority uniqueness (excluding current rule)
                if (await _context.SortingRules.AnyAsync(r => r.Priority == model.Priority && r.IsActive && r.Id != id))
                {
                    ModelState.AddModelError("Priority", "Priority must be unique among active rules.");
                }

                // Validate keywords
                if (string.IsNullOrWhiteSpace(model.Keywords))
                {
                    ModelState.AddModelError("Keywords", "Keywords are required.");
                }

                if (ModelState.IsValid)
                {
                    var existingRule = await _context.SortingRules.FindAsync(id);
                    if (existingRule == null)
                    {
                        return Json(new { success = false, message = "Sorting rule not found." });
                    }

                    // Update properties
                    existingRule.Name = model.Name;
                    existingRule.Priority = model.Priority;
                    existingRule.Keywords = model.Keywords;
                    existingRule.TargetRackType = model.TargetRackType;
                    existingRule.IsActive = model.IsActive;
                    existingRule.LastModifiedDate = DateTime.Now;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Updated sorting rule '{RuleName}' (ID: {RuleId})", 
                        existingRule.Name, existingRule.Id);

                    return Json(new { success = true, message = $"Sorting rule '{existingRule.Name}' updated successfully." });
                }
            }

            // Return validation errors
            var errors = ModelState.Where(x => x.Value != null && x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return Json(new { success = false, message = "Validation failed.", errors = errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sorting rule {RuleId}", id);
            return Json(new { success = false, message = "An error occurred while updating the sorting rule." });
        }
    }

    // POST /SortingRules/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var rule = await _context.SortingRules.FindAsync(id);
            if (rule == null)
            {
                return Json(new { success = false, message = "Sorting rule not found." });
            }

            _context.SortingRules.Remove(rule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted sorting rule '{RuleName}' (ID: {RuleId})", rule.Name, rule.Id);

            return Json(new { success = true, message = $"Sorting rule '{rule.Name}' deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sorting rule {RuleId}", id);
            return Json(new { success = false, message = "An error occurred while deleting the sorting rule." });
        }
    }

    // POST /SortingRules/ToggleStatus/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        try
        {
            var rule = await _context.SortingRules.FindAsync(id);
            if (rule == null)
            {
                return Json(new { success = false, message = "Sorting rule not found." });
            }

            rule.IsActive = !rule.IsActive;
            rule.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var status = rule.IsActive ? "enabled" : "disabled";
            _logger.LogInformation("Toggled sorting rule '{RuleName}' (ID: {RuleId}) to {Status}", 
                rule.Name, rule.Id, status);

            return Json(new { 
                success = true, 
                message = $"Sorting rule '{rule.Name}' {status} successfully.",
                isActive = rule.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling status for sorting rule {RuleId}", id);
            return Json(new { success = false, message = "An error occurred while updating the sorting rule status." });
        }
    }

    // POST /SortingRules/UpdatePriorities
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePriorities([FromBody] int[] ruleIds)
    {
        try
        {
            if (ruleIds == null || ruleIds.Length == 0)
            {
                return Json(new { success = false, message = "No rule IDs provided." });
            }

            // Load all rules in the provided order
            var rules = new List<SortingRule>();
            foreach (var id in ruleIds)
            {
                var rule = await _context.SortingRules.FindAsync(id);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }

            // Update priorities based on order (1-based priority)
            for (int i = 0; i < rules.Count; i++)
            {
                rules[i].Priority = i + 1;
                rules[i].LastModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated priorities for {Count} sorting rules", rules.Count);

            return Json(new { success = true, message = "Rule priorities updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sorting rule priorities");
            return Json(new { success = false, message = "An error occurred while updating rule priorities." });
        }
    }

    // GET /SortingRules/GetRule/{id}
    [HttpGet]
    public async Task<IActionResult> GetRule(int id)
    {
        try
        {
            var rule = await _context.SortingRules.FindAsync(id);
            if (rule == null)
            {
                return Json(new { success = false, message = "Sorting rule not found." });
            }

            return Json(new { 
                success = true, 
                rule = new {
                    id = rule.Id,
                    name = rule.Name,
                    priority = rule.Priority,
                    keywords = rule.Keywords,
                    targetRackType = (int)rule.TargetRackType,
                    isActive = rule.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sorting rule {RuleId}", id);
            return Json(new { success = false, message = "An error occurred while fetching the sorting rule." });
        }
    }

    // POST /SortingRules/TestKeywords
    [HttpPost]
    public IActionResult TestKeywords([FromBody] TestKeywordsRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Keywords) || string.IsNullOrWhiteSpace(request.TestPartName))
            {
                return Json(new { success = false, message = "Keywords and test part name are required." });
            }

            // Create a temporary rule to test
            var tempRule = new SortingRule
            {
                Keywords = request.Keywords,
                TargetRackType = request.TargetRackType
            };

            var matches = tempRule.MatchesPartName(request.TestPartName);
            var matchedKeywords = new List<string>();

            if (matches)
            {
                var keywords = tempRule.GetKeywordsList();
                var upperPartName = request.TestPartName.ToUpperInvariant();
                matchedKeywords = keywords.Where(k => upperPartName.Contains(k)).ToList();
            }

            return Json(new { 
                success = true, 
                matches = matches,
                matchedKeywords = matchedKeywords,
                targetRackType = tempRule.TargetRackType.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing keywords");
            return Json(new { success = false, message = "An error occurred while testing keywords." });
        }
    }

    private int GetNextPriority()
    {
        var maxPriority = _context.SortingRules.Max(r => (int?)r.Priority) ?? 0;
        return maxPriority + 1;
    }

    // Request model for keyword testing
    public class TestKeywordsRequest
    {
        public string Keywords { get; set; } = string.Empty;
        public string TestPartName { get; set; } = string.Empty;
        public RackType TargetRackType { get; set; } = RackType.Standard;
    }
}