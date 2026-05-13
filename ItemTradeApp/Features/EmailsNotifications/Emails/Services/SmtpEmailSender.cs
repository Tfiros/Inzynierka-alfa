using System.Net;
using System.Net.Mail;
using ItemTradeApp.Features.EmailsNotifications.Emails.Contracts;
using ItemTradeApp.Features.EmailsNotifications.Emails.Settings;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.EmailsNotifications.Emails.Services;

public sealed class SmtpEmailSender(
    IOptions<SmtpEmailOptions> smtpOpt) : IEmailSender
{
    public async Task SendAsync(EmailMessage msg, CancellationToken ct)
    {
        var smtp = smtpOpt.Value;

        using var client = new SmtpClient(smtp.Host, smtp.Port);

        client.EnableSsl = smtp.EnableSsl;
        client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);

        using var mail = new MailMessage();

        mail.From = new MailAddress(smtp.SenderEmail, smtp.SenderName);
        mail.Subject = msg.Subject;
        mail.Body = msg.HtmlBody;
        mail.IsBodyHtml = true;

        mail.To.Add(new MailAddress(msg.To));

        await client.SendMailAsync(mail);
    }
}