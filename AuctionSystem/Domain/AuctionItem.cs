namespace AuctionSystem.Domain
{
    public class AuctionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageId { get; set; }
        public decimal InitialPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public UserAccount Creator { get; set; }
        public UserAccount HighestBidder { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Transaction> BidHistory { get; set; } = new();
        private readonly ReaderWriterLockSlim _rwl = new();
        private readonly object _bidLock = new();

        // Минимальное увеличение ставки на аукционе
        // Сейчас - 5%
        public static readonly decimal MinBidMultiplier = 1.05m;

        public AuctionItem(string name, string description, string imageId, decimal initialPrice, UserAccount creator, TimeSpan duration)
        {
            Id = Guid.NewGuid();
            Name = name;
            Description = description;
            ImageId = imageId;
            InitialPrice = initialPrice;
            CurrentPrice = initialPrice;
            Creator = creator;
            HighestBidder = Creator;
            IsActive = true;
            CreatedAt = DateTime.Now;
            EndTime = CreatedAt.Add(duration);
        }

        /// <summary>
        /// Возвращает текст для обьявления о лоте
        /// </summary>
        public string GetCaption()
        {
            _rwl.EnterReadLock();

            try
            {
                return $"<b>Имя:</b> {Name}\n\n" +
                      $"<b>Описание:</b> {Description}\n\n" +
                      $"💰<b>Наибольшая ставка:</b> @{HighestBidder.Username}: {CurrentPrice}₽\n" +
                      $"👤<b>Создатель:</b> @{Creator.Username}\n" +
                      $"⏰<b>Заканичвается:</b> {EndTime:yyyy-MM-dd HH:mm}";
            }
            finally
            {
                _rwl.ExitReadLock();
            }
        }

        /// <summary>
        /// Пытается обновить текщую ставку
        /// </summary>
        /// <param name="bidder">Пользователь, делающий ставку</param>
        /// <param name="amount">Размер ставки</param>
        /// <param name="error">out для возникающих ошибок</param>
        /// <returns>True если успешно поставили, иначе false</returns>
        public bool TryPlaceBid(UserAccount bidder, decimal amount, out string error)
        {
            if (!IsActive)
            {
                error = "⚠️Аукцион по этому лоту уже завершён.";
                return false;
            }

            if (bidder.Id == Creator.Id)
            {
                error = "⚠️Вы не можете поставить на собственный лот!";
                return false;
            }

            if (amount > bidder.Balance)
            {
                error = $"⚠️У вас нет столько денег!";
                return false;
            }

            // используем lock, т.к. rwl слишком медленный когда много людей хотят поставить одновременно
            lock (_bidLock)
            {
                if (amount < CurrentPrice * MinBidMultiplier)
                {
                    error = $"⚠️Минимальная новая ставка: {CurrentPrice * MinBidMultiplier}₽";
                    return false;
                }

                if (HighestBidder != null && HighestBidder.Id != bidder.Id)
                {
                    // Возвращаем деньги предыдущему участнику
                    Transaction refund = new Transaction(HighestBidder, CurrentPrice, DateTime.Now);
                    if (!refund.TryProcess())
                    {
                        error = "⚠️Ошибка возврата средств предыдущему участнику!";
                        return false;
                    }
                    BidHistory.Add(refund);
                }

                // Списываем деньги у пользователя
                Transaction bid = new Transaction(bidder, -amount, DateTime.Now);
                if (!bid.TryProcess())
                {
                    error = "⚠️Ошибка списания средств! Проверьте ваш баланс.";
                    return false;
                }
                BidHistory.Add(bid);

                // Обновляем данные
                CurrentPrice = amount;
                HighestBidder = bidder;
                error = string.Empty;
            }

            return true;
        }

        /// <summary>
        /// Деактивирует лот
        /// </summary>
        public void EndAuction()
        {
            lock (_bidLock)
            {
                IsActive = false;

                Transaction payment = new Transaction(Creator, CurrentPrice, DateTime.Now);
                if (!payment.TryProcess())
                {
                    Console.WriteLine("⚠️Ошибка перевода средств создателю лота!");
                    return;
                }

                BidHistory.Add(payment);

                Console.WriteLine($"Завершен аукцион по лоту \"{Name}\"");
                Console.WriteLine($"Победитель: @{HighestBidder}, цена: {CurrentPrice}");
            }
        }
    }
}