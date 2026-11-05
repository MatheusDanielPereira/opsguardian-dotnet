using Microsoft.AspNetCore.Mvc.RazorPages;
using OpsGuardian.Application.Incidents;

namespace OpsGuardian.Web.Pages;

public sealed class IndexModel(IncidentService incidentService) : PageModel
{
    public DashboardSnapshot Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, []);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await incidentService.GetDashboardAsync(cancellationToken);
    }
}
