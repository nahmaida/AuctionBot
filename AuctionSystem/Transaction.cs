namespace AuctionSystem
{
    public class Transaction
    {
        public UserAccount User { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    
        public Transaction(UserAccount user, decimal amount, DateTime timestamp)
        {
            User = user;
            Amount = amount;
            Timestamp = timestamp;
        }
    }
}
