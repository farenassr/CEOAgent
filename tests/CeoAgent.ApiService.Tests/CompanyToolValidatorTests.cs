using System.Text.Json;
using CeoAgent.ApiService.Modules.Companies.Endpoints;
using CeoAgent.Shared.Request.Company;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

public sealed class CompanyToolValidatorTests
{
    [Test]
    public void Validate_WhenDescriptionIsNull_AllowsTenantToolRequest()
    {
        var validator = new CompanyToolValidator();
        var request = new CompanyToolRequest
        {
            ToolKey = "check_google_calendar_availability",
            Description = null,
            ParametersSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>(),
                additionalProperties = false,
            }),
        };

        var result = validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
