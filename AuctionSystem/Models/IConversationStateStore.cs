using AuctionSystem.Application;

namespace AuctionSystem.Models
{
    public interface IConversationStateStore
    {
        PostStep GetStep(long chatId);
        void SetStep(long chatId, PostStep step);

        PostFlow? GetFlow(long chatId);
        void SetFlow(long chatId, PostFlow flow);
        void ClearFlow(long chatId);

        bool TryGetPendingBid(long chatId, out Guid itemId);
        void SetPendingBid(long chatId, Guid itemId);
        void ClearPendingBid(long chatId);
    }

}
