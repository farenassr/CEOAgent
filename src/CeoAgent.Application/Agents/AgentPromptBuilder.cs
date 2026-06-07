using System.Text;
using System.Globalization;

namespace CeoAgent.Application.Agents;

/// <summary>
/// Builds the system prompt that gives the agent company context, operating constraints, and enabled tools.
/// </summary>
public static class AgentPromptBuilder
{
    /// <summary>
    /// Creates the agent instruction text from the current company profile, local time, schedule, and tool catalog.
    /// </summary>
    public static string Build(AgentPromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = new StringBuilder();
        builder.AppendLine("You are the business assistant for this company.");
        AppendInvariant(builder, $"Company: {context.CompanyName}");
        AppendInvariant(builder, $"Assistant: {context.AgentDisplayName}");
        AppendInvariant(builder, $"Timezone: {context.TimeZoneId}");
        AppendInvariant(builder, $"Local date: {context.LocalNow:yyyy-MM-dd}");
        AppendInvariant(builder, $"Local time: {context.LocalNow:HH:mm}");
        AppendInvariant(builder, $"Language: {context.Language}");
        AppendInvariant(builder, $"Model: {context.ModelName}");
        AppendInvariant(builder, $"Hours: {NormalizeOptionalText(context.WorkingHoursSummary, "not configured")}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(context.PromptOverride))
        {
            builder.AppendLine();
            builder.AppendLine("Company instructions (subordinate to platform rules):");
            builder.AppendLine(context.PromptOverride.Trim());

            builder.AppendLine();
            builder.AppendLine("Platform rules always take precedence over company instructions.");
            builder.AppendLine("- Never bypass company isolation.");
            builder.AppendLine("- Never bypass tool execution or privacy rules.");
        }

        if (context.Tools.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Enabled tools:");
            foreach (var tool in context.Tools)
            {
                AppendInvariant(builder, $"- {tool.Name}: {tool.Description}");
            }
        }

        return builder.ToString();
    }

    private static string NormalizeOptionalText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static void AppendInvariant(StringBuilder builder, FormattableString value)
    {
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }
}
