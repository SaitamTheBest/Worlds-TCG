using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using web_tcg.Models;
using web_tcg.Services;

namespace web_tcg.Controllers;

[Route("Auth")]
public sealed class PasswordRecoveryController(PasswordRecoveryService recovery, ILogger<PasswordRecoveryController> logger) : Controller
{
    [HttpGet("ForgotPassword")]
    public IActionResult ForgotPassword() => View("~/Views/Auth/ForgotPassword.cshtml",new ForgotPasswordViewModel());

    [HttpPost("ForgotPassword")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid) return View("~/Views/Auth/ForgotPassword.cshtml",model);
        try { await recovery.RequestAsync(model.Email,ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex,"Échec du relais mail pour une demande de réinitialisation."); }
        // Même réponse pour un compte absent et une demande acceptée.
        ViewBag.Confirmation = PasswordRecoveryService.Confirmation;
        ModelState.Clear();
        return View("~/Views/Auth/ForgotPassword.cshtml",new ForgotPasswordViewModel());
    }

    [HttpGet("ResetPassword")]
    public IActionResult ResetPassword(string? userId, string? token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            ViewBag.InvalidLink = true;
        return View("~/Views/Auth/ResetPassword.cshtml",new ResetPasswordViewModel { UserId=userId ?? "",Token=token ?? "" });
    }

    [HttpPost("ResetPassword")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("password-recovery")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (ModelState.IsValid)
        {
            var result = await recovery.ResetAsync(model.UserId,model.Token,model.Password);
            if (result.Succeeded) return RedirectToAction(nameof(ResetComplete));
            if (result.Errors.Any(e => e.Code == "InvalidToken")) ModelState.AddModelError("", "Ce lien est invalide, expiré ou déjà utilisé. Demandez un nouveau lien.");
            else ModelState.AddModelError("", "Le mot de passe ne respecte pas les règles du compte. Choisissez un autre mot de passe.");
        }
        return View("~/Views/Auth/ResetPassword.cshtml",model);
    }

    [HttpGet("ResetComplete")]
    public IActionResult ResetComplete() => View("~/Views/Auth/ResetComplete.cshtml");
}
