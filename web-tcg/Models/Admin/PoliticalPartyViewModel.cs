using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;
namespace web_tcg.Models.Admin;
public sealed class PoliticalPartyViewModel
{
    public int? Id { get; set; }
    [Required(ErrorMessage = "Le nom est obligatoire."), MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Range(1, int.MaxValue, ErrorMessage = "Sélectionnez un pays.")] public int CountryId { get; set; }
    [EnumDataType(typeof(PoliticalOrientation), ErrorMessage = "Sélectionnez une orientation valide.")]
    public PoliticalOrientation Orientation { get; set; } = PoliticalOrientation.Centre;
    [Required(ErrorMessage = "Ajoutez le logo du parti."), MaxLength(500)]
    [RegularExpression(@"^/uploads/parties/[a-f0-9]{32}\.(png|jpg|webp)$", ErrorMessage = "Téléversez un logo dans le stockage du site.")]
    public string ImageUrl { get; set; } = string.Empty;
}
