using System.Runtime.CompilerServices;
using Datateal.Ui.Shared.Ai;
using Microsoft.Extensions.AI;

namespace Datateal.Ui.Server.Application.Ai;

/// <summary>
/// Orchestrates context assembly and AI provider streaming.
/// </summary>
public class AiChatService(IContextAssembler contextAssembler, IAiProviderFactory providerFactory)
    : IAiChatService
{
    public async IAsyncEnumerable<string> StreamChatAsync(
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = await contextAssembler.AssembleAsync(request, cancellationToken);
        var client = providerFactory.Create(request.Provider, request.ApiKey, request.Endpoint, request.Model);
        var options = new ChatOptions { ModelId = request.Model };

        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            var text = update.Text;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }
}
