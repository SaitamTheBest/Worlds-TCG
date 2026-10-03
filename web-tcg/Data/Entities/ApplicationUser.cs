using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace web_tcg.Data.Entities
{
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Avatar affiché sur le profil et dans la topbar.
        /// </summary>
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Monnaie principale du joueur.
        /// Utilisée notamment pour acheter et ouvrir des boosters.
        /// </summary>
        public decimal Balance { get; set; } = 0m;

        /// <summary>
        /// Monnaie dédiée au marché des cartes.
        /// Elle permet d'acheter, vendre et défausser des cartes.
        /// </summary>
        public decimal CardBalance { get; set; } = 0m;

        /// <summary>
        /// Date et heure du dernier gain automatique de monnaie.
        /// </summary>
        public DateTime? LastBalanceRewardAt { get; set; }

        /// <summary>
        /// Expérience du joueur.
        /// </summary>
        public int XP { get; set; } = 0;

        /// <summary>
        /// Date de création du compte.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indique si ce compte est un bot contrôlé par l'IA
        /// (utilisé pour les parties contre l'ordinateur).
        /// </summary>
        public bool IsBot { get; set; } = false;

        /// <summary>
        /// Cartes choisies par l'utilisateur pour sa vitrine de profil.
        /// </summary>
        public ICollection<UserProfileShowcaseCard> ShowcaseCards { get; set; } =
            new List<UserProfileShowcaseCard>();
    }
}