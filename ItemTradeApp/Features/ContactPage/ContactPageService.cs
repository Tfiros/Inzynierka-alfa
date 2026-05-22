using System.Text.RegularExpressions;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.ContactPage.DTOs;
using ItemTradeApp.Features.Shared.Emails.Settings;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ItemTradeApp.Features.ContactPage;

public interface IContactPageService
{
    Task<Result<string>> SendAsync(ContactDTO? request, CancellationToken ct);
}

public sealed class ContactPageService(
    IOptions<SmtpEmailOptions> smtpOptions) : IContactPageService
{
    public async Task<Result<string>> SendAsync(ContactDTO? request, CancellationToken ct)
    {
        var validationResult = ValidateRequest(request);
        if (!validationResult.IsSuccess)
            return validationResult;

        var smtpSettings = smtpOptions.Value;

        try
        {
            var email = BuildMessage(request!, smtpSettings);

            var secureSocketOptions = smtpSettings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                smtpSettings.Host,
                smtpSettings.Port,
                secureSocketOptions,
                ct);

            await smtp.AuthenticateAsync(
                smtpSettings.Username,
                smtpSettings.Password,
                ct);

            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);

            return Result<string>.Success(null, "Wiadomość została wysłana.");
        }
        catch
        {
            return Result<string>.InternalServerError("Nie udało się wysłać wiadomości.");
        }
    }

    private static Result<string> ValidateRequest(ContactDTO? request)
    {
        if (request is null)
            return Result<string>.BadRequest("Brak danych formularza.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<string>.BadRequest("Imię i nazwisko jest wymagane.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<string>.BadRequest("Email jest wymagany.");

        var email = request.Email.Trim();

        var emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        if (!emailRegex.IsMatch(email))
            return Result<string>.BadRequest("Email ma nieprawidłowy format.");

        if (string.IsNullOrWhiteSpace(request.Subject))
            return Result<string>.BadRequest("Temat jest wymagany.");

        if (string.IsNullOrWhiteSpace(request.Message))
            return Result<string>.BadRequest("Wiadomość jest wymagana.");

        var message = request.Message.Trim();

        if (message.Length < 20)
            return Result<string>.BadRequest("Wiadomość musi mieć co najmniej 20 znaków.");

        return Result<string>.Success(null);
    }

    private static MimeMessage BuildMessage(ContactDTO request, SmtpEmailOptions smtp)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
            throw new InvalidOperationException("Brak konfiguracji SMTP host.");

        if (smtp.Port <= 0)
            throw new InvalidOperationException("Brak poprawnej konfiguracji SMTP port.");

        if (string.IsNullOrWhiteSpace(smtp.SenderName))
            throw new InvalidOperationException("Brak konfiguracji SMTP sender name.");

        if (string.IsNullOrWhiteSpace(smtp.SenderEmail))
            throw new InvalidOperationException("Brak konfiguracji SMTP sender email.");

        if (!MailboxAddress.TryParse(smtp.SenderEmail, out _))
            throw new InvalidOperationException("Nieprawidłowy sender email.");

        if (string.IsNullOrWhiteSpace(smtp.Username))
            throw new InvalidOperationException("Brak konfiguracji SMTP username.");

        if (string.IsNullOrWhiteSpace(smtp.Password))
            throw new InvalidOperationException("Brak konfiguracji SMTP password.");

        var email = new MimeMessage();

        email.From.Add(new MailboxAddress(smtp.SenderName, smtp.SenderEmail));
        email.To.Add(MailboxAddress.Parse(smtp.SenderEmail));
        email.ReplyTo.Add(new MailboxAddress(request.Name, request.Email));
        email.Subject = $"Formularz kontaktowy: {request.Subject}";

        email.Body = new TextPart("plain")
        {
            Text =
$"""
Nowa wiadomość z formularza kontaktowego

Imię: {request.Name}
Email: {request.Email}
Temat: {request.Subject}

Wiadomość:
{request.Message}
"""
        };

        return email;
    }
}