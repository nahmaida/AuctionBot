namespace AuctionSystem
{
    public class Transaction
    {
        public UserAccount User { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
