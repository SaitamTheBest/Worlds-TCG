using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Admin
{
    public class CreateRegionViewModel
    {
        [Required(ErrorMessage = "Le nom de la région est obligatoire.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères.")]
        [Display(Name = "Nom de la région")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le code couleur est obligatoire.")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Le code couleur doit être au format #FFFFFF.")]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "La couleur doit être au format hexadécimal #FFFFFF.")]
        [Display(Name = "Couleur")]
        public string Color { get; set; } = "#FFFFFF";

        [Required(ErrorMessage = "Le code de la région est obligatoire.")]
        [StringLength(10, MinimumLength = 1, ErrorMessage = "Le code doit contenir entre 1 et 10 caractères.")]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;
    }
}
