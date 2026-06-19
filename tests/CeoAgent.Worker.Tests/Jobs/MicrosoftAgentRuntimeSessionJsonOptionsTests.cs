using System.Reflection;
using System.Text.Json;
using CeoAgent.Infrastructure.Implementation.AI;
using Microsoft.Agents.AI;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class MicrosoftAgentRuntimeSessionJsonOptionsTests
{
    [Test]
    public void SessionJsonOptions_CanResolveChatClientAgentSessionMetadata()
    {
        var runtimeType = typeof(AgentFunctionInvocationGuard).Assembly.GetType(
            "CeoAgent.Infrastructure.Implementation.AI.MicrosoftAgentRuntime",
            throwOnError: true)!;
        var optionsField = runtimeType.GetField(
            "SessionJsonOptions",
            BindingFlags.NonPublic | BindingFlags.Static);
        var options = optionsField.ShouldNotBeNull().GetValue(null).ShouldBeOfType<JsonSerializerOptions>();

        options.GetTypeInfo(typeof(ChatClientAgentSession)).ShouldNotBeNull();
    }
}
