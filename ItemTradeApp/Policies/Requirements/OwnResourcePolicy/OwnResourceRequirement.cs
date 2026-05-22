using Microsoft.AspNetCore.Authorization;

namespace ItemTradeApp.Policies.Requirements.OwnResourcePolicy;

public class OwnResourceRequirement : IAuthorizationRequirement
{
    public string RequirementParameterName { get; }

    public OwnResourceRequirement(string requirementParameterName = "id")
    {
        RequirementParameterName = requirementParameterName;
    }
}