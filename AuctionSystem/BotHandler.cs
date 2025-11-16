using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem
{
    public class PostFlow
    {
        public string Step { get; set; } = "name"; // "name", "desc", "img", "price", "duration"
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageId { get; set; }
        public decimal? Price { get; set; }
        public double? Duration { get; set; }
    }

    public class BotHandler
    {
        private readonly string token = "8474493428:AAFczNluN_mNrzle4uOttUklYpgN1h39ybA";
        private readonly ReaderWriterLockSlim _rwl;
        private readonly Dictionary<long, PostFlow> _postFlows;
        private readonly object _postLock;
        private TelegramBotClient Client { get; set; }
        public List<UserAccount> Users { get; set; }
        public AuctionHouse House { get; }

        public BotHandler(AuctionHouse house)
        {
            Client = new TelegramBotClient(token);
            Users = new List<UserAccount>();
            House = house;
            _rwl = new ReaderWriterLockSlim();
            _postFlows = new Dictionary<long, PostFlow>();
            _postLock = new object();
        }

        public void Start()
        {
            Client.StartReceiving(HandleUpdateAsync, HandleError);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is { } message)
            {
                await OnMessage(update);
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await OnCallback(callbackQuery);
            }
        }

        private Task HandleError(ITelegramBotClient bot, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            string errorText;

            if (exception is ApiRequestException apiEx)
            {
                errorText = $"Telegram API Error [{apiEx.ErrorCode}] {apiEx.Message}";
            }
            else
            {
                errorText = $"Unhandled exception ({source}): {exception}";
            }

            Console.WriteLine(errorText);

            return Task.CompletedTask;
        }

        private async Task OnMessage(Update update)
        {
            var message = update.Message;
            if (message == null) return;

            Chat chat = message.Chat;

            PostFlow? flow;
            lock (_postLock)
            {
                _postFlows.TryGetValue(chat.Id, out flow);
            }

            if (flow != null)
            {
                await ContinuePostFlow(message, flow);
                return;
            }

            switch (message.Text?.ToLower())
            {
                case "/start":
                    await HandleStart(message.Chat);
                    break;

                case "/view":
                    await HandleView(message.Chat);
                    break;

                case "/post":
                    await HandlePost(message.Chat);
                    break;

                default:
                    await Client.SendMessage(message.Chat, "Неверная команда!");
                    break;
            }
        }

        private async Task HandleStart(Chat chat)
        {
            if (Users.Any(user => user.Id == chat.Id))
            {
                await Client.SendMessage(chat, "Вы уже зарегестрированы!");
                return;
            }

            UserAccount user = new UserAccount(chat.Id, chat.Username ?? "Аноним");
            _rwl.EnterWriteLock();
            try
            {
                Users.Add(user);
            }
            finally
            {
                _rwl.ExitWriteLock();
            }

            await Client.SendMessage(chat, "Добро пожаловать в Gambling empire!");
        }

        private async Task HandleView(Chat chat)
        {
            if (House.AuctionItems.Count == 0)
            {
                await Client.SendMessage(chat, "Пока никаких обьявлений!");
                return;
            }

            foreach (AuctionItem item in House.AuctionItems)
            {
                if (!item.IsActive) continue;

                var caption = item.GetCaption();

                // Отправляем фото с описанием
                Message message = await Client.SendPhoto(
                    chatId: chat.Id,
                    photo: InputFile.FromFileId(item.ImageId),
                    caption: caption,
                    parseMode: ParseMode.Html
                );
            }
        }

        private async Task HandlePost(Chat chat)
        {
            lock (_postLock)
            {
                _postFlows[chat.Id] = new PostFlow { Step = "name" };
            }

            await Client.SendMessage(chat, "<b>Введите название:</b>", parseMode: ParseMode.Html);
        }

        private async Task ContinuePostFlow(Message message, PostFlow flow)
        {
            Chat chat = message.Chat;
            switch (flow.Step)
            {
                case "name":
                    if (message.Text == null)
                    {
                        await Client.SendMessage(chat, "Неверное название, попробуйте ещё раз.");
                        break;
                    }
                    flow.Name = message.Text;
                    flow.Step = "desc";
                    await Client.SendMessage(chat, "<b>Введите описание:</b>", parseMode: ParseMode.Html);
                    break;

                case "desc":
                    if (message.Text == null)
                    {
                        await Client.SendMessage(chat, "Неверное описание, попробуйте ещё раз.");
                        break;
                    }
                    flow.Description = message.Text;
                    flow.Step = "img";
                    await Client.SendMessage(chat, "<b>Отправьте фото:</b>", parseMode: ParseMode.Html);
                    break;

                case "img":
                    if (message.Photo == null)
                    {
                        await Client.SendMessage(chat, "<b>Отправьте фото:</b>", parseMode: ParseMode.Html);
                        return;
                    }
                    flow.ImageId = message.Photo[^1].FileId;
                    flow.Step = "price";
                    await Client.SendMessage(chat, "<b>Начальная цена (0-1.000.000):</b>", parseMode: ParseMode.Html);
                    break;

                case "price":
                    if (!decimal.TryParse(message.Text, out var price) || price < 0 || price > 1000000)
                    {
                        await Client.SendMessage(chat, "Неверный формат цены, попробуйте ещё раз.");
                        return;
                    }

                    flow.Price = price;
                    flow.Step = "duration";
                    await Client.SendMessage(chat, "<b>Длительность в минутах (1-1440):</b>", parseMode: ParseMode.Html);
                    break;

                case "duration":
                    if (!double.TryParse(message.Text, out var duration) || duration < 1 || duration > 1440)
                    {
                        await Client.SendMessage(chat, "Неверное время, попробуйте ещё раз.");
                        return;
                    }
                    flow.Duration = duration;

                    string caption = $"<b>Имя:</b> {flow.Name}\n\n" +
                                      $"Описание: {flow.Description}\n\n" +
                                      $"💰 <b>Наибольшая ставка:</b> (Вы): {flow.Price}\n" +
                                      $"👤 <b>Создатель:</b> (Вы)\n";

                    // Принять/отменить предпросмотр
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                    new []
                        {
                            InlineKeyboardButton.WithCallbackData("✅ Принять", "post_accept"),
                            InlineKeyboardButton.WithCallbackData("🗑 Отменить", "post_discard")
                        }
                    });

                    // Отправляем предпросмотр
                    await Client.SendPhoto(
                        chatId: chat.Id,
                        photo: InputFile.FromFileId(flow.ImageId!),
                        caption: caption,
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard
                    );

                    flow.Step = "confirm";
                    break;

                case "confirm":
                    // Пользователь должен нажать кнопку
                    await Client.SendMessage(chat, "Нажмите кнопку 'Принять' или 'Отменить' под предпросмотром.");
                    break;
            }
        }

        private async Task OnCallback(CallbackQuery query)
        {
            var chatId = query.Message!.Chat.Id;
            var data = query.Data;

            if (data == "post_accept" || data == "post_discard")
            {
                PostFlow flow;
                lock (_postLock)
                {
                    _postFlows.TryGetValue(chatId, out flow!);
                }

                if (flow == null)
                {
                    await Client.AnswerCallbackQuery(query.Id, "Нет активного лота для подтверждения.");
                    return;
                }

                if (data == "post_discard")
                {
                    lock (_postLock)
                    {
                        _postFlows.Remove(chatId);
                    }

                    await Client.AnswerCallbackQuery(query.Id, "Лот отменён.");
                    await Client.SendMessage(chatId, "Создание лота отменено.");
                    return;
                }

                // Принимаем
                var chat = query.Message.Chat;
                var creator = new UserAccount(chat.Id, chat.Username ?? "Аноним");

                AuctionItem item = new AuctionItem(
                    name: flow.Name!,
                    description: flow.Description!,
                    imageId: flow.ImageId!,
                    initialPrice: flow.Price!.Value,
                    creator: creator,
                    duration: TimeSpan.FromMinutes(flow.Duration ?? 1)
                );

                House.AddAuctionItem(item);

                lock (_postLock)
                {
                    _postFlows.Remove(chatId);
                }

                await Client.AnswerCallbackQuery(query.Id, "Лот создан!");
                await Client.SendMessage(chatId, "Лот успешно создан и добавлен в аукцион.");
            }
        }
    }
}