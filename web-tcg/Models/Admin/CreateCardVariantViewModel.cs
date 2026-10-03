using System.ComponentModel.DataAnnotations;
using web_tcg.Data.Enums;

namespace web_tcg.Models.Admin
{
    public class CreateCardVariantViewModel
    {
        /// <summary>
        /// Identifiant de la variante existante.
        /// Null lors de la création d'une nouvelle variante.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// Numéro permanent de la variante.
        /// Exemple :
        /// 1 => P01-70-1
        /// 2 => P01-70-2
        /// </summary>
        public int? VariantNumber { get; set; }

        [Required]
        public CardArtType ArtType { get; set; }

        [Required(ErrorMessage = "L'image de la variante est obligatoire.")]
        [MaxLength(500)]
        [RegularExpression(
            @"^/uploads/cards/[a-f0-9]{32}\.(png|jpg|webp)$",
            ErrorMessage = "L'illustration de la variante doit être un fichier enregistré dans Worlds TCG.")]
        public string ImageUrl { get; set; } = string.Empty;
    }
}
