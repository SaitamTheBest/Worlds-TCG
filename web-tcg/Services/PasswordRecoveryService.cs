using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using web_tcg.Data.Entities;

namespace web_tcg.Services;

public sealed class PublicSiteOptions { public string BaseUrl { get; set; } = string.Empty; }

public sealed class PasswordRecoveryService(UserManager<ApplicationUser> users, ITransactionalEmailSender email, IOptions<PublicSiteOptions> site)
{
    public const string Confirmation = "Si un compte correspond à cette adresse, un lien de réinitialisation vous sera envoyé. Vérifiez aussi vos courriers indésirables.";

    public async Task RequestAsync(string address, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(address.Trim());
        if (user == null || user.IsBot || !await users.HasPasswordAsync(user)) return;
        // Le domaine est configuré par l'administrateur, jamais dérivé du Host envoyé par le navigateur.
        var origin = new Uri(site.Value.BaseUrl, UriKind.Absolute);
        if (origin.Scheme is not ("https" or "http")) throw new InvalidOperationException("URL publique non configurée.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var url = QueryHelpers.AddQueryString(new Uri(origin,"/Auth/ResetPassword").AbsoluteUri, new Dictionary<string,string?> {
            ["userId"] = user.Id, ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)) });
        await email.SendPasswordResetAsync(user.Email!,url,ct);
    }

    public async Task<IdentityResult> ResetAsync(string userId, string encodedToken, string password)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || user.IsBot) return InvalidLink();
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken)); }
        catch (FormatException) { return InvalidLink(); }
        var result = await users.ResetPasswordAsync(user,token,password);
        if (result.Succeeded) await users.ResetAccessFailedCountAsync(user);
        return result;
    }

    private static IdentityResult InvalidLink() => IdentityResult.Failed(new IdentityError { Code="InvalidToken",Description="Lien invalide ou expiré. Demandez un nouveau lien." });
}
