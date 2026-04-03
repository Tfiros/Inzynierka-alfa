using System.Text.RegularExpressions;
using ItemTradeApp.Features.ContactPage.DTOs;
using MailKit.Security;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ItemTradeApp.Features.ContactPage;

public interface IContactPageService
{
    Task SendAsync(ContactDTO request, CancellationToken ct);
}

public sealed class ContactPageService(IConfiguration configuration) : IContactPageService
{
    public async Task SendAsync(ContactDTO request, CancellationToken ct)
    {
        ValidateRequest(request);
        var smtpSettings = GetSmtpSettings();

        var email = BuildMessage(request, smtpSettings);

        var secureSocketOptions = smtpSettings.EnableSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(smtpSettings.Host, smtpSettings.Port, secureSocketOptions, ct);
        await smtp.AuthenticateAsync(smtpSettings.Username, smtpSettings.Password, ct);
        await smtp.SendAsync(email, ct);
        await smtp.DisconnectAsync(true, ct);
    }

    private static void ValidateRequest(ContactDTO request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Imię i nazwisko jest wymagane.", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email jest wymagany.", nameof(request.Email));

        var email = request.Email.Trim();

        var emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        if (!emailRegex.IsMatch(email))
            throw new ArgumentException("Email ma nieprawidłowy format.", nameof(request.Email));

        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Temat jest wymagany.", nameof(request.Subject));

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Wiadomość jest wymagana.", nameof(request.Message));

        var message = request.Message.Trim();

        if (message.Length < 20)
            throw new ArgumentException("Wiadomość musi mieć co najmniej 20 znaków.", nameof(request.Message));
    }

    private SmptDTO GetSmtpSettings()
    {
        var host = configuration["Mails:Host"];
        var portValue = configuration["Mails:Port"];
        var senderName = configuration["Mails:SenderName"];
        var senderEmail = configuration["Mails:SenderEmail"];
        var username = configuration["Mails:Username"];
        var password = configuration["Mails:Password"];
        var enableSslValue = configuration["Mails:EnableSsl"];

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Brak konfiguracji: Mails:Host");

        if (string.IsNullOrWhiteSpace(portValue) || !int.TryParse(portValue, out var port))
            throw new InvalidOperationException("Brak lub nieprawidłowa konfiguracja: Mails:Port");

        if (string.IsNullOrWhiteSpace(senderName))
            throw new InvalidOperationException("Brak konfiguracji: Mails:SenderName");

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException("Brak konfiguracji: Mails:SenderEmail");

        if (!MailboxAddress.TryParse(senderEmail, out _))
            throw new InvalidOperationException("Nieprawidłowa konfiguracja: Mails:SenderEmail");

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Brak konfiguracji: Mails:Username");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Brak konfiguracji: Mails:Password");

        if (string.IsNullOrWhiteSpace(enableSslValue) || !bool.TryParse(enableSslValue, out var enableSsl))
            throw new InvalidOperationException("Brak lub nieprawidłowa konfiguracja: Mails:EnableSsl");

        return new SmptDTO(
            Host: host,
            Port: port,
            SenderName: senderName,
            SenderEmail: senderEmail,
            Username: username,
            Password: password,
            EnableSsl: enableSsl
        );
    }

    private static MimeMessage BuildMessage(ContactDTO request, SmptDTO smpt)
    {
        var email = new MimeMessage();

        email.From.Add(new MailboxAddress(smpt.SenderName, smpt.SenderEmail));
        email.To.Add(MailboxAddress.Parse(smpt.SenderEmail));
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