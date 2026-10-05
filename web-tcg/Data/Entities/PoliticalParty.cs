using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;
namespace web_tcg.Data.Entities;
public sealed class PoliticalParty
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public Country Country { get; set; } = null!;
    public PoliticalOrientation Orientation { get; set; }
    [Required, MaxLength(500)] public string ImageUrl { get; set; } = string.Empty;
}
