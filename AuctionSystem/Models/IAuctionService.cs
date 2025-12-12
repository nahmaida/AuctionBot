using AuctionSystem.Domain;

namespace AuctionSystem.Models
{
    public interface IAuctionService
    {
        bool TryGetItem(Guid itemId, out AuctionItem? auctionItem);
        List<AuctionItem> GetActiveItems();
        List<AuctionItem> GetWonItems(long chatId);
        void AddItem(AuctionItem item);
    }
}
