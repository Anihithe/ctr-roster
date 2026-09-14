using CtrRoster.Application.Common.Interfaces;

namespace CtrRoster.Domain.Tests.TestHelpers;

public class TestMessageRenderer : IDiscordMessageRenderer
{
    public List<Guid> QueuedSessionIds { get; } = [];

    public Task QueueMessageUpdateAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        QueuedSessionIds.Add(sessionId);
        return Task.CompletedTask;
    }
}
