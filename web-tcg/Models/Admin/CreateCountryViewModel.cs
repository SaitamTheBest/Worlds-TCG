using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace web_tcg.Models.Admin
{
    public class CreateCountryViewModel
    {
        [Required(ErrorMessage = "Le nom du pays est obligatoire.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 100 caractères.")]
        [Display(Name = "Nom du pays")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Le code du pays est obligatoire.")]
        [StringLength(3, MinimumLength = 2, ErrorMessage = "Le code doit contenir entre 2 et 3 caractères.")]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "La région est obligatoire.")]
        [Display(Name = "Région")]
        public int? RegionId { get; set; }

        public IEnumerable<SelectListItem> Regions { get; set; }
            = Enumerable.Empty<SelectListItem>();
    }
}
