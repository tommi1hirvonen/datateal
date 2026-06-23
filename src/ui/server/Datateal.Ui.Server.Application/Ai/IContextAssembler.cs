using Datateal.Ui.Shared.Ai;
using Microsoft.Extensions.AI;

namespace Datateal.Ui.Server.Application.Ai;

public interface IContextAssembler
{
    /// <summary>
    /// Assembles a list of <see cref="ChatMessage"/> objects from the request context,
    /// including system prompt with Datateal-specific instructions, catalog schemas,
    /// package list, and the conversation history.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> AssembleAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}
