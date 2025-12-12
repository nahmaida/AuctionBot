namespace AuctionSystem.Application
{
    public class ConversationState
    {
        public PostStep Step { get; set; }
        public PostFlow Flow { get; set; } = new();
        public Guid? PendingBidItemId { get; set; }
    }

}
