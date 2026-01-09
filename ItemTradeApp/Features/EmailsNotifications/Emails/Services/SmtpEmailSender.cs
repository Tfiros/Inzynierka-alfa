using System.Net;
using System.Net.Mail;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Contracts;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Settings;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.EmaillsNotifications.Emails.Services;

public sealed class SmtpEmailSender(
    IOptions<SmtpEmailOptions> smtpOpt) : IEmailSender
{
    public async Task SendAsync(EmailMessage msg, CancellationToken ct)
    {
        var smtp = smtpOpt.Value;

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.EnableSsl,
            Credentials = new NetworkCredential(smtp.Username, smtp.Password),
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(smtp.SenderEmail, smtp.SenderName),
            Subject = msg.Subject,
            Body = msg.HtmlBody,
            IsBodyHtml = true
        };

        mail.To.Add(new MailAddress(msg.To));

        await client.SendMailAsync(mail);
    }
}