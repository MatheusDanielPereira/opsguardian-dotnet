namespace OpsGuardian.Web.Security;

public static class AuthorizationPolicies
{
    public const string WriteIncidents = "incident.write";

    public const string ResolveIncidents = "incident.resolve";
}

public static class AuthorizationRoles
{
    public const string IncidentManager = "IncidentManager";
}
