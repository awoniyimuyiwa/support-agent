using Api.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.AI;

public class SupportAgent(
    IChatClient chatClient,
    IOptions<AiOptions> aiOptions,
    TicketResolutionTools tools) : AIAgent 
{
    readonly IList<AITool> _agentTools =
    [
        AIFunctionFactory.Create(tools.UpdateTicketStatusAsync, name: nameof(tools.UpdateTicketStatusAsync)),
        AIFunctionFactory.Create(tools.SearchKnowledgeBaseAsync, name: nameof(tools.SearchKnowledgeBaseAsync))
    ];

    readonly int _maxOutputTokens = aiOptions.Value.MaxOutputTokens;

    public new string Id { get; } = "did:mesh:customer-support-agent-001";
    public override string Name { get; } = "Support Agent";
    public override string Description { get; } = "Handles initial triage and ticket assignment logic.";

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var chatOptions = new ChatOptions
        {
            MaxOutputTokens = _maxOutputTokens,
            AdditionalProperties = new()
            {
                ["AgentId"] = this.Id,
                ["ToolName"] = "AgentRunExecution"
            },
            Tools = _agentTools
        };

        var response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);

        return new AgentResponse
        {
            Messages = response.Messages
        };
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatOptions = new ChatOptions
        {           
            MaxOutputTokens = _maxOutputTokens,
            AdditionalProperties = new()
            {
                ["AgentId"] = this.Id,
                ["ToolName"] = "AgentStreamingExecution"
            },
            Tools = _agentTools
        };

        var stream = chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken);
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            var responseUpdate = new AgentResponseUpdate();

            if (!string.IsNullOrEmpty(update.Text))
            {
                responseUpdate.Contents.Clear();
                responseUpdate.Contents.Add(new TextContent(update.Text));
            }

            yield return responseUpdate;
        }
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default)
    {
        AgentSession session = new CustomAgentSession();
        return ValueTask.FromResult(session);
    }

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(session, options);
        using var document = JsonDocument.Parse(json);
        return ValueTask.FromResult(document.RootElement.Clone());
    }

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement jsonElement,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        AgentSession session = JsonSerializer.Deserialize<CustomAgentSession>(jsonElement.GetRawText(), options) ?? new CustomAgentSession();
        return ValueTask.FromResult(session);
    }
}

public class CustomAgentSession : AgentSession { }
