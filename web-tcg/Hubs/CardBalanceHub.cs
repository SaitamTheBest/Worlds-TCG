using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using web_tcg.Data.Entities;

namespace web_tcg.Hubs
{
    [Authorize]
    public sealed class CardBalanceHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CardBalanceHub(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Retourne la valeur actuelle de la monnaie marché
        /// de l'utilisateur connecté.
        /// </summary>
        public async Task<decimal> GetCurrentBalance()
        {
            if (Context.User == null)
                throw new HubException(
                    "Utilisateur non authentifié.");

            var user = await _userManager.GetUserAsync(
                Context.User);

            if (user == null)
                throw new HubException(
                    "Utilisateur introuvable.");

            return user.CardBalance;
        }
    }
}