using System.ComponentModel;
using System.Threading.Tasks;

namespace Api.AI;

public class TicketResolutionTools
{
    // Natural semantic description explaining actions to Ollama/Llama3.2.
    [Description("Updates the status of a resolved customer ticket with the provided resolution notes.")]
    public async Task<string> UpdateTicketStatusAsync(long ticketId, string statusResolution)
    {
        // Internal secure database logic runs here...
        return "Status successfully written.";
    }

    // Clean description telling the model how and when to look up knowledge entries.
    [Description("Searches internal policy and product support documentation using semantic keyword phrases.")]
    public async Task<string> SearchKnowledgeBaseAsync(string searchTopic)
    {
        // Your pgvector retrieval code runs safely here...
        return "Context text payload...";
    }
}
