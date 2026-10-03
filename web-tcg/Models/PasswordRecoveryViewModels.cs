using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Veuillez saisir votre adresse email.")]
    [EmailAddress(ErrorMessage = "Veuillez saisir une adresse email valide.")]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] [StringLength(4096)] public string Token { get; set; } = string.Empty;
    [Required(ErrorMessage = "Veuillez saisir un nouveau mot de passe.")]
    [StringLength(128, MinimumLength = 6, ErrorMessage = "Le mot de passe doit contenir entre 6 et 128 caractères.")]
    [DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Veuillez confirmer le mot de passe.")]
    [Compare(nameof(Password), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    [DataType(DataType.Password)] public string ConfirmPassword { get; set; } = string.Empty;
}
