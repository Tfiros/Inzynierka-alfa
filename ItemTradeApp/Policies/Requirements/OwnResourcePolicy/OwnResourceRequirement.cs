using Microsoft.AspNetCore.Authorization;

namespace ItemTradeApp.Policies.OwnResourcePolicy.Requirements;

public class OwnResourceRequirement : IAuthorizationRequirement
{
    public string RequirementParameterName { get; }

    public OwnResourceRequirement(string requirementParameterName = "id")
    {
        RequirementParameterName = requirementParameterName;
    }
}