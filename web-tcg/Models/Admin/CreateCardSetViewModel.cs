using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Admin
{
    public class CreateCardSetViewModel
    {
        [Required(ErrorMessage = "Le nom de l'extension est obligatoire.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères.")]
        [Display(Name = "Nom de l'extension")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le code de l'extension est obligatoire.")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "Le code doit contenir entre 2 et 20 caractères.")]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "Extension active")]
        public bool IsActive { get; set; }

        [Required(ErrorMessage = "La date de sortie est obligatoire.")]
        [Display(Name = "Date de sortie")]
        [DataType(DataType.Date)]
        public DateTime ReleaseDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "L'image du booster est obligatoire.")]
        [StringLength(500, ErrorMessage = "L'URL de l'image ne peut pas dépasser 500 caractères.")]
        [RegularExpression(@"^/uploads/cardsets/[a-f0-9]{32}\.(png|jpg|webp)$", ErrorMessage = "Importez une image du booster avant de créer l’extension.")]
        [Display(Name = "Image du booster")]
        public string BoosterImageUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le prix du booster est obligatoire.")]
        [Range(0, 999999999, ErrorMessage = "Le prix doit être compris entre 0 et 999 999 999.")]
        [Display(Name = "Prix du booster")]
        public decimal BoosterPrice { get; set; }
    }
}
