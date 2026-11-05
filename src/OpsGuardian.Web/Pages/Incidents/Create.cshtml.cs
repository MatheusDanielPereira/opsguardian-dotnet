using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpsGuardian.Application.Incidents;
using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Web.Pages.Incidents;

public sealed class CreateModel(IncidentService incidentService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var id = await incidentService.CreateAsync(
            new CreateIncidentCommand(Input.Title, Input.Description, Input.Severity, Input.AssignedEngineer),
            cancellationToken);

        TempData["SuccessMessage"] = "Incident created and SLA tracking started.";
        return RedirectToPage("/Incidents/Details", new { id });
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(160)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2_000)]
        public string Description { get; set; } = string.Empty;

        [EnumDataType(typeof(IncidentSeverity))]
        public IncidentSeverity Severity { get; set; } = IncidentSeverity.Medium;

        [Display(Name = "Assigned engineer")]
        [StringLength(120)]
        public string? AssignedEngineer { get; set; }
    }
}
