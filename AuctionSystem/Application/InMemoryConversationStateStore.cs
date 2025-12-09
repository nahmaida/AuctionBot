using AuctionSystem.Models;

namespace AuctionSystem.Application
{
    public class InMemoryConversationStateStore : IConversationStateStore
    {
        private readonly Dictionary<long, PostFlow> _postFlows = new();
        private readonly Dictionary<long, PostStep> _postSteps = new();
        private readonly Dictionary<long, Guid> _pendingBids = new();
        private readonly object _lock = new();

        public PostStep GetStep(long chatId)
        {
            lock (_lock)
                return _postSteps.TryGetValue(chatId, out var step) ? step : PostStep.none;
        }

        public void SetStep(long chatId, PostStep step)
        {
            lock (_lock)
                _postSteps[chatId] = step;
        }

        public PostFlow? GetFlow(long chatId)
        {
            lock (_lock)
                return _postFlows.TryGetValue(chatId, out var flow) ? flow : null;
        }

        public void SetFlow(long chatId, PostFlow flow)
        {
            lock (_lock)
                _postFlows[chatId] = flow;
        }

        public void ClearFlow(long chatId)
        {
            lock (_lock)
                _postFlows.Remove(chatId);
        }

        public bool TryGetPendingBid(long chatId, out Guid itemId)
        {
            lock (_lock)
                return _pendingBids.TryGetValue(chatId, out itemId);
        }

        public void SetPendingBid(long chatId, Guid itemId)
        {
            lock (_lock)
                _pendingBids[chatId] = itemId;
        }

        public void ClearPendingBid(long chatId)
        {
            lock (_lock)
                _pendingBids.Remove(chatId);
        }
    }
}
