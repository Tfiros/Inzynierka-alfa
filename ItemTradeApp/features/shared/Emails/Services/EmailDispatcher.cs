using System.Threading.Channels;
using ItemTradeApp.Features.Shared.Emails.Contracts;

namespace ItemTradeApp.Features.Shared.Emails.Services;

public interface IEmailDispatcher
{
    ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default);
}
public sealed class EmailDispatcher(Channel<EmailJob> channel) : IEmailDispatcher
{
    public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default)
        => channel.Writer.WriteAsync(job, ct);
}