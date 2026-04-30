using System.Threading.Channels;
using ItemTradeApp.Features.Shared.Emails.Contracts;
using ItemTradeApp.Features.Shared.Notifications.Repositories;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.Emails.Services;

public sealed class EmailBackgroundService(
    Channel<EmailJob> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailBackgroundService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var job in channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var repo = scope.ServiceProvider.GetRequiredService<IEmailOutboxRepository>();
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserIdentityRepository>();
                var user = await userRepo.GetUserByIdAsync(job.UserId, ct);
                if (user is null || string.IsNullOrWhiteSpace(user.Email))
                    continue;

                await sender.SendAsync(
                    new EmailMessage(user.Email, job.Subject, job.HtmlBody, job.TextBody),
                    ct);

                var now = DateTimeOffset.UtcNow;

                await repo.SaveSentAsync(new Persistence.Models.Emails
                {
                    UserId = job.UserId,
                    Subject = job.Subject,
                    Body = job.HtmlBody,
                    CreatedAt = now,
                    SentAt = now
                }, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Email job failed.");
            }
        }
    }
}