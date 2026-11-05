using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpsGuardian.Application.Incidents;
using OpsGuardian.Web.Security;

namespace OpsGuardian.Web.Pages.Incidents;

public sealed class DetailsModel(
    IncidentService incidentService,
    IAuthorizationService authorizationService) : PageModel
{
    public IncidentDetails Incident { get; private set; } = null!;

    [BindProperty]
    public string Engineer { get; set; } = string.Empty;

    [BindProperty]
    public string ResolutionSummary { get; set; } = string.Empty;

    public bool CanManage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadOrNotFoundAsync(id, cancellationToken);

    public async Task<IActionResult> OnPostAssignAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(Engineer) || Engineer.Length > 120)
        {
            ModelState.AddModelError(nameof(Engineer), "Enter an engineer name with up to 120 characters.");
            return await LoadOrNotFoundAsync(id, cancellationToken);
        }

        await incidentService.AssignAsync(id, Engineer, cancellationToken);
        TempData["SuccessMessage"] = "Incident owner updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        await incidentService.AcknowledgeAsync(id, cancellationToken);
        TempData["SuccessMessage"] = "Incident acknowledged.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(ResolutionSummary) || ResolutionSummary.Length > 1_000)
        {
            ModelState.AddModelError(
                nameof(ResolutionSummary),
                "Enter a resolution summary with up to 1,000 characters.");
            return await LoadOrNotFoundAsync(id, cancellationToken);
        }

        await incidentService.ResolveAsync(id, ResolutionSummary, cancellationToken);
        TempData["SuccessMessage"] = "Incident resolved.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync())
        {
            return Forbid();
        }

        await incidentService.ReopenAsync(id, cancellationToken);
        TempData["SuccessMessage"] = "Incident reopened with a new SLA window.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> LoadOrNotFoundAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            Incident = await incidentService.GetAsync(id, cancellationToken);
            Engineer = Incident.AssignedEngineer ?? string.Empty;
            CanManage = await IsAuthorizedAsync();
            return Page();
        }
        catch (IncidentNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<bool> IsAuthorizedAsync()
    {
        var result = await authorizationService.AuthorizeAsync(
            User,
            resource: null,
            AuthorizationPolicies.ResolveIncidents);
        return result.Succeeded;
    }
}
