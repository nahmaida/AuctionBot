using AuctionSystem.Models;

namespace AuctionSystem.Application
{
    public class InMemoryConversationStateStore : IConversationStateStore
    {
        private readonly Dictionary<long, ConversationState> _state = new();
        private readonly object _lock = new();

        private ConversationState GetOrCreate(long chatId)
        {
            if (!_state.TryGetValue(chatId, out var conv))
            {
                conv = new ConversationState();
                _state[chatId] = conv;
            }

            return conv;
        }

        public PostStep GetStep(long chatId)
        {
            lock (_lock)
            {
                return _state.TryGetValue(chatId, out var conv) ? conv.Step : PostStep.none;
            }
        }

        public void SetStep(long chatId, PostStep step)
        {
            lock (_lock)
            {
                var conv = GetOrCreate(chatId);
                conv.Step = step;
            }
        }

        public PostFlow? GetFlow(long chatId)
        {
            lock (_lock)
            {
                return _state.TryGetValue(chatId, out var conv) ? conv.Flow : null;
            }
        }

        public void SetFlow(long chatId, PostFlow flow)
        {
            lock (_lock)
            {
                var conv = GetOrCreate(chatId);
                conv.Flow = flow;
            }
        }

        public void ClearFlow(long chatId)
        {
            lock (_lock)
            {
                if (_state.TryGetValue(chatId, out var conv))
                {
                    conv.Flow = new PostFlow();
                    conv.Step = PostStep.none;
                }
            }
        }

        public bool TryGetPendingBid(long chatId, out Guid itemId)
        {
            lock (_lock)
            {
                if (_state.TryGetValue(chatId, out var conv) &&
                    conv.PendingBidItemId.HasValue)
                {
                    itemId = conv.PendingBidItemId.Value;
                    return true;
                }

                itemId = Guid.Empty;
                return false;
            }
        }

        public void SetPendingBid(long chatId, Guid itemId)
        {
            lock (_lock)
            {
                var conv = GetOrCreate(chatId);
                conv.PendingBidItemId = itemId;
            }
        }

        public void ClearPendingBid(long chatId)
        {
            lock (_lock)
            {
                if (_state.TryGetValue(chatId, out var conv))
                {
                    conv.PendingBidItemId = null;
                }
            }
        }
    }
}
