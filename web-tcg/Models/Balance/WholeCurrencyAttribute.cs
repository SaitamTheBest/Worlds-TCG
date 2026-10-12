using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Balance;

public sealed class WholeCurrencyAttribute : ValidationAttribute
{
    public WholeCurrencyAttribute() : base("Le montant doit être un nombre entier, sans décimales.") { }
    public override bool IsValid(object? value) => value == null || value is decimal amount && amount == decimal.Truncate(amount);
}
