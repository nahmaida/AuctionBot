namespace AuctionSystem
{
    public class UserAccount
    {
        public long Id { get; set; }
        public string Username { get; set; }
        public decimal Balance { get; set; }

        public UserAccount(long id, string username)
        {
            Id = id;
            Username = username;
            Balance = 1000m;
        }
    }
}
