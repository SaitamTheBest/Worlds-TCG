using System.Text;
using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;

namespace web_tcg.Services;

public sealed class MailOptions
{
    public string Host { get; set; } = "smtp";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Worlds TCG";
}

public interface ITransactionalEmailSender
{
    Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken ct = default);
}

public sealed class TransactionalEmailSender(IOptions<MailOptions> settings) : ITransactionalEmailSender
{
    public async Task SendPasswordResetAsync(string recipient, string resetUrl, CancellationToken ct = default)
    {
        var options = settings.Value;
        using var client = new SmtpClient(options.Host,options.Port) { EnableSsl=options.EnableSsl,UseDefaultCredentials=false,Timeout=15000 };
        if (!string.IsNullOrWhiteSpace(options.Username)) client.Credentials = new NetworkCredential(options.Username,options.Password);
        var safeUrl = HtmlEncoder.Default.Encode(resetUrl);
        using var message = new MailMessage { From=new MailAddress(options.FromAddress,options.FromName),Subject="Réinitialisez votre mot de passe Worlds TCG",IsBodyHtml=true,
            Body=$"<div style='font-family:Arial;background:#111820;color:#f5f5f5;padding:32px;border-radius:12px'><h1 style='color:#f4c430'>Worlds TCG</h1><h2>Un nouveau mot de passe</h2><p>Vous avez demandé à réinitialiser votre mot de passe. Ce lien est valable 30 minutes et ne fonctionne qu’une fois.</p><p><a style='display:inline-block;padding:14px 20px;background:#f4c430;color:#111;text-decoration:none;border-radius:8px' href='{safeUrl}'>Choisir mon mot de passe</a></p><p>Si le bouton ne fonctionne pas : <a href='{safeUrl}'>{safeUrl}</a></p><p>Si vous n’êtes pas à l’origine de cette demande, ignorez ce message.</p></div>" };
        message.To.Add(new MailAddress(recipient));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString($"Worlds TCG — Réinitialisez votre mot de passe : {resetUrl}\nLien valable 30 minutes et une seule fois. Ignorez ce message si vous n'avez pas fait cette demande.",Encoding.UTF8,"text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Body,Encoding.UTF8,"text/html"));
        message.Body = string.Empty;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message,timeout.Token);
    }
}
