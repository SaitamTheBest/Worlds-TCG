using Microsoft.EntityFrameworkCore;
using web_tcg.Data;
using web_tcg.Data.Entities;

namespace web_tcg.Services
{
    public class CardCollectionService
    {
        private readonly ApplicationDbContext _context;

        public CardCollectionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddCardAsync(string userId, int cardId)
        {
            var existingCard = _context.UserCardCollections.Local
                .FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.CardId == cardId);

            if (existingCard != null)
            {
                existingCard.Quantity++;
                return;
            }

            existingCard = await _context.UserCardCollections
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.CardId == cardId);

            if (existingCard != null)
            {
                existingCard.Quantity++;
                return;
            }

            _context.UserCardCollections.Add(new UserCardCollection
            {
                UserId = userId,
                CardId = cardId,
                Quantity = 1
            });
        }
    }
}
