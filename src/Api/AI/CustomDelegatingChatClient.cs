using AgentGovernance;
using Microsoft.Extensions.AI;
using System.Security;

namespace Api.AI;

public class CustomDelegatingChatClient(
    IChatClient chatClient,
    GovernanceKernel governanceKernel) : DelegatingChatClient(chatClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // STRICT METADATA ENFORCEMENT
        if (options?.AdditionalProperties == null 
            || !options.AdditionalProperties.TryGetValue("AgentId", out var aId) 
            || string.IsNullOrWhiteSpace(aId?.ToString()))
        {
            throw new ArgumentException("Governance Violation: Any execution via the CustomDelegatingChatClient must explicitly define a non-empty 'AgentId' within ChatOptions.AdditionalProperties.");
        }

        if (!options.AdditionalProperties.TryGetValue("ToolName", out var tName) ||
            string.IsNullOrWhiteSpace(tName?.ToString()))
        {
            throw new ArgumentException("Governance Violation: Any execution via the CustomDelegatingChatClient must explicitly define a non-empty 'ToolName' within ChatOptions.AdditionalProperties.");
        }

        string agentId = aId.ToString()!;
        string toolName = tName.ToString()!;

        // INBOUND TARGETED ACCESS CHECKS
        var latestMessage = messages.LastOrDefault(m => m.Role == ChatRole.User || m.Role == ChatRole.Tool);

        if (latestMessage != null && !string.IsNullOrWhiteSpace(latestMessage.Text))
        {
            // Enterprise FIDES Policy Evaluation (Run exactly once per call)
            // AgentGuard has already sanitized and verified the text content by this point!
            var governanceResult = governanceKernel.EvaluateToolCall(
                agentId: agentId,
                toolName: toolName,
                args: new() { ["input"] = latestMessage.Text });

            if (!governanceResult.Allowed)
            {
                throw new SecurityException($"Agent Governance Blocked Execution. Reason: {governanceResult.Reason}");
            }
        }

        // DISPATCH SECURE PAYLOAD DOWN THE PIPELINE
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }
}
