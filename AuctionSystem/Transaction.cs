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

        public bool TryProcess()
        {
            if (User.Balance < Amount)
            {
                return false;
            }

            User.Balance += Amount;
            return true;
        }
    }
}
