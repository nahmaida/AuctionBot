using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem;

/// <summary>
/// Этапы создания объявления + bid для ввода суммы ставки
/// </summary>
public enum PostStep
{
    name,
    desc,
    img,
    price,
    duration,
    bid,
    confirm,
    none
}

/// <summary>
/// Helper для временного хранения данных при создании лота
/// </summary>
public class PostFlow
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageId { get; set; }
    public decimal? Price { get; set; }
    public double? Duration { get; set; }
}

/// <summary>
/// Обработчик бота. Отвечает за прием сообщений, интерфейс бота и тд
/// "Класс бога" но из-за приколов телеграма разделять слишком долго
/// </summary>
public class BotHandler
{
    // тут заменить
    private readonly string token = "BOT_TOKEN";

    private readonly ReaderWriterLockSlim _rwl = new();

    // chatId -> PostFlow
    private readonly Dictionary<long, PostFlow> _postFlows = new();

    // chatId -> PostStep
    private readonly Dictionary<long, PostStep> _postSteps = new();

    // chatId -> auctionItemId
    private readonly Dictionary<long, Guid> _pendingBids = new();

    private readonly object _stateLock = new();
    private TelegramBotClient Client { get; set; }
    public List<UserAccount> Users { get; set; }
    public AuctionHouse House { get; }

    public BotHandler(AuctionHouse house)
    {
        Client = new TelegramBotClient(token);
        Users = new List<UserAccount>();
        House = house;
        House.AuctionEnded += EndAuctionAsync;
    }

    public void Start()
    {
        Client.StartReceiving(HandleUpdateAsync, HandleError);
    }

    /// <summary>
    /// Обработчик сообщений: направляет в OnMessage или OnCallback
    /// </summary>
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        // сообщение
        if (update.Message is { } message)
        {
            await OnMessage(update);
        }
        // нажатие на кнопку
        else if (update.CallbackQuery is { } callbackQuery)
        {
            await OnCallback(callbackQuery);
        }
    }

    /// <summary>
    /// Лог ошибок
    /// </summary>
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

    /// <summary>
    /// Обработка входящих сообщений
    /// ВАЖНО: все состояние проверяется ТОЛЬКО для текущего чата
    /// </summary>
    /// <param name="update">Обновление от бота</param>
    /// <returns></returns>
    private async Task OnMessage(Update update)
    {
        var message = update.Message;
        if (message == null) return;

        Chat chat = message.Chat;

        // Получаем состояние чата
        PostFlow? flow = null;
        PostStep stepForChat = PostStep.none;
        Guid bidItemId = Guid.Empty;
        bool hasBid = false;

        lock (_stateLock)
        {
            _postFlows.TryGetValue(chat.Id, out flow);
            _postSteps.TryGetValue(chat.Id, out stepForChat);
            hasBid = _pendingBids.TryGetValue(chat.Id, out bidItemId);
        }

        // Проверяем, создаем ли мы лот
        if (stepForChat != PostStep.none && stepForChat != PostStep.bid && flow != null)
        {
            await ContinuePostFlow(message, flow, stepForChat);
            return;
        }

        // Проверяем, делаем ли мы ставку
        if (stepForChat == PostStep.bid && hasBid)
        {
            await HandleBidAmount(message, bidItemId);
            return;
        }

        // читаем команды
        switch (message.Text)
        {
            case "/start":
                await HandleStart(message.Chat);
                break;

            case "Баланс":
                {
                    UserAccount? user = Users.FirstOrDefault(user => user.Id == chat.Id);
                    if (user == null)
                    {
                        await Client.SendMessage(message.Chat, "Сначала зарегистрируйтесь! (/start)", parseMode: ParseMode.Html);
                        break;
                    }

                    decimal balance = user.Balance;
                    await Client.SendMessage(message.Chat, $"💰Баланс: {balance}₽", parseMode: ParseMode.Html);
                }
                break;

            case "Просмотреть лоты":
                await HandleView(message.Chat);
                break;

            case "Выставить на аукцион":
                await HandlePost(message.Chat);
                break;

            case "Выигранные лоты":
                await HandleViewWon(message.Chat);
                break;

            default:
                await Client.SendMessage(message.Chat, "Неверная команда!");
                break;
        }
    }

    /// <summary>
    /// Обработка ввода суммы ставки
    /// </summary>
    /// <param name="message">Сообщение с суммой</param>
    /// <param name="bidItemId">Id лота для ставки</param>
    private async Task HandleBidAmount(Message message, Guid bidItemId)
    {
        Chat chat = message.Chat;

        if (!decimal.TryParse(message.Text, out var bidAmount) || bidAmount <= 0)
        {
            await Client.SendMessage(message.Chat, "Неверный формат суммы, введите число.");
            return;
        }

        AuctionItem? item = House.GetActiveItems().FirstOrDefault(item => item.Id == bidItemId);
        if (item == null)
        {
            await Client.SendMessage(message.Chat, "Неверный товар, повторите попытку.");
            // Сбрасываем режим ставки только для этого чата
            lock (_stateLock)
            {
                _pendingBids.Remove(chat.Id);
                _postSteps[chat.Id] = PostStep.none;
            }
            return;
        }

        UserAccount? user = Users.FirstOrDefault(user => user.Id == chat.Id);
        if (user == null)
        {
            await Client.SendMessage(message.Chat, "Сначала зарегистрируйтесь! (/start)");
            lock (_stateLock)
            {
                _pendingBids.Remove(chat.Id);
                _postSteps[chat.Id] = PostStep.none;
            }
            return;
        }

        bool success = item.TryPlaceBid(user, bidAmount, out string error);

        // Сбрасываем состояние ставки для этого чата
        lock (_stateLock)
        {
            _pendingBids.Remove(chat.Id);
            _postSteps[chat.Id] = PostStep.none;
        }

        if (!success)
        {
            await Client.SendMessage(message.Chat, error);
            return;
        }

        await Client.SendMessage(item.Creator.Id, $"⚠️Новая ставка на ваш лот <b>{item.Name}</b>\n\n@{user.Username}: {bidAmount}", parseMode: ParseMode.Html);
        await Client.SendMessage(message.Chat, "🎉Успешно!");
    }

    /// <summary>
    /// Регистрация пользователя и вывод стартового меню
    /// </summary>
    /// <param name="chat">Текущий чат</param>
    private async Task HandleStart(Chat chat)
    {
        if (Users.Any(user => user.Id == chat.Id))
        {
            await Client.SendMessage(chat, "Вы уже зарегистрированы!");
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

        // Приветствие с меню
        var replyKeyboard = new ReplyKeyboardMarkup(
            new[]
            {
                new KeyboardButton[]
                {
                    new KeyboardButton("Баланс"),
                    new KeyboardButton("Выигранные лоты")
                },
                new KeyboardButton[]
                {
                    new KeyboardButton("Выставить на аукцион")
                },
                new KeyboardButton[]
                {
                    new KeyboardButton("Просмотреть лоты")
                }
            }
        )
        {
            ResizeKeyboard = true
        };

        await Client.SendMessage(
            chat,
            "Добро пожаловать в Gambling Empire, аукцион номер 1 в П312💹\n\n💰Стартовый баланс: 10000₽",
            replyMarkup: replyKeyboard,
            parseMode: ParseMode.Html
        );
    }

    /// <summary>
    /// Вывод активных лотов и кнопки для ставки
    /// <param name="chat">Текущий чат</param>
    /// </summary>
    private async Task HandleView(Chat chat)
    {
        List<AuctionItem> activeItems = House.GetActiveItems();

        if (activeItems.Count() == 0)
        {
            await Client.SendMessage(chat, "Пока никаких объявлений!");
            return;
        }

        foreach (AuctionItem item in activeItems)
        {
            if (!item.IsActive) continue;

            string caption = item.GetCaption();
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("✅Сделать ставку", $"make_bid:{item.Id}")
                }
            });

            await Client.SendPhoto(
                chatId: chat.Id,
                photo: InputFile.FromFileId(item.ImageId),
                caption: caption,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard
            );
        }
    }

    /// <summary>
    /// Выводим выигранные пользователем лоты
    /// </summary>
    /// <param name="chat">Текущий чат</param>
    private async Task HandleViewWon(Chat chat)
    {
        var won = House.GetWonItems(chat.Id);

        if (won.Count() == 0)
        {
            await Client.SendMessage(chat, "Вы пока ничего не выиграли!");
            return;
        }

        await Client.SendMessage(chat, "✅Ваши выигрыши:");

        foreach (AuctionItem item in won)
        {
            string caption = item.GetCaption();
            await Client.SendPhoto(
                chatId: chat.Id,
                photo: InputFile.FromFileId(item.ImageId),
                caption: caption,
                parseMode: ParseMode.Html
            );
        }
    }

    /// <summary>
    /// Создания лота
    /// Создаём PostFlow и переводим шаг в name только для данного chat.Id.
    /// <param name="chat">Текущий чат</param>
    /// </summary>
    private async Task HandlePost(Chat chat)
    {
        lock (_stateLock)
        {
            _postFlows[chat.Id] = new PostFlow();
            _postSteps[chat.Id] = PostStep.name;
        }

        await Client.SendMessage(chat, "Введите название:", parseMode: ParseMode.Html);
    }

    /// <summary>
    /// Продолжение создания лота по текущему шагу
    /// </summary>
    /// <param name="message">Сообщение с данными</param>
    /// <param name="flow">Хранилище данных</param>
    /// <param name="step">Текущий шаг</param>
    /// <returns></returns>
    private async Task ContinuePostFlow(Message message, PostFlow flow, PostStep step)
    {
        Chat chat = message.Chat;

        // Максимальный баланс пользователей для проверки цены
        // (Не принимаем если никто не может поставить)
        decimal maxUserBalance = Users.Max(u => u.Id == chat.Id ? u.Balance : 0);

        switch (step)
        {
            case PostStep.name:
                if (message.Text == null)
                {
                    await Client.SendMessage(chat, "Неверное название, попробуйте ещё раз.");
                    return;
                }

                flow.Name = message.Text;

                lock (_stateLock)
                {
                    _postSteps[chat.Id] = PostStep.desc;
                }

                await Client.SendMessage(chat, "Введите описание:", parseMode: ParseMode.Html);
                break;

            case PostStep.desc:
                if (message.Text == null)
                {
                    await Client.SendMessage(chat, "Неверное описание, попробуйте ещё раз.");
                    return;
                }

                flow.Description = message.Text;

                lock (_stateLock)
                {
                    _postSteps[chat.Id] = PostStep.img;
                }

                await Client.SendMessage(chat, "Отправьте фото:", parseMode: ParseMode.Html);
                break;

            case PostStep.img:
                if (message.Photo == null)
                {
                    await Client.SendMessage(chat, "Отправьте фото:", parseMode: ParseMode.Html);
                    return;
                }

                // Берем самое большое фото
                flow.ImageId = message.Photo[^1].FileId;

                lock (_stateLock)
                {
                    _postSteps[chat.Id] = PostStep.price;
                }

                await Client.SendMessage(chat, $"Начальная цена (0-{maxUserBalance}):", parseMode: ParseMode.Html);
                break;

            case PostStep.price:
                if (!decimal.TryParse(message.Text, out var price) || price < 0 || price > maxUserBalance)
                {
                    await Client.SendMessage(chat, "Неверная цена, попробуйте ещё раз.");
                    return;
                }

                flow.Price = price;

                lock (_stateLock)
                {
                    _postSteps[chat.Id] = PostStep.duration;
                }

                await Client.SendMessage(chat, "Длительность в минутах (1-1440):", parseMode: ParseMode.Html);
                break;

            case PostStep.duration:
                if (!double.TryParse(message.Text, out var duration) || duration < 1 || duration > 1440)
                {
                    await Client.SendMessage(chat, "Неверное время, попробуйте ещё раз.");
                    return;
                }

                flow.Duration = duration;

                string caption = $"Имя: {flow.Name}\n\n" +
                                 $"Описание: {flow.Description}\n\n" +
                                 $"💰 Наибольшая ставка: (Вы): {flow.Price}₽\n" +
                                 $"👤 Создатель: (Вы)\n";

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("✅ Принять", "post_accept"),
                        InlineKeyboardButton.WithCallbackData("🗑 Отменить", "post_discard")
                    }
                });

                await Client.SendPhoto(
                    chatId: chat.Id,
                    photo: InputFile.FromFileId(flow.ImageId!),
                    caption: caption,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard
                );

                lock (_stateLock)
                {
                    _postSteps[chat.Id] = PostStep.confirm;
                }

                break;

            case PostStep.confirm:
                await Client.SendMessage(chat, "Нажмите кнопку 'Принять' или 'Отменить' под предпросмотром.");
                break;
        }
    }

    /// <summary>
    /// Обработка нажатий кнопок ТОЛЬКО для текущего chatId.
    /// </summary>
    /// <param name="query">Запрос от пользователи (нажатая кнопка)</param>
    /// <returns></returns>
    private async Task OnCallback(CallbackQuery query)
    {
        var chatId = query.Message!.Chat.Id;
        var data = query.Data;
        if (data == null) return;

        if (data == "post_accept" || data == "post_discard")
        {
            PostFlow? flow;
            lock (_stateLock)
            {
                _postFlows.TryGetValue(chatId, out flow);
            }

            if (flow == null)
            {
                await Client.AnswerCallbackQuery(query.Id, "Нет активного лота для подтверждения.");
                return;
            }

            if (data == "post_discard")
            {
                lock (_stateLock)
                {
                    _postFlows.Remove(chatId);
                    _postSteps[chatId] = PostStep.none;
                }

                await Client.AnswerCallbackQuery(query.Id, "Лот отменён.");
                await Client.SendMessage(chatId, "Создание лота отменено.");
                return;
            }

            // Принять (создать лот)
            var chat = query.Message.Chat;
            var creator = new UserAccount(chat.Id, chat.Username ?? "Аноним");
            var item = new AuctionItem(
                name: flow.Name!,
                description: flow.Description!,
                imageId: flow.ImageId!,
                initialPrice: flow.Price!.Value,
                creator: creator,
                duration: TimeSpan.FromMinutes(flow.Duration ?? 1)
            );

            House.AddAuctionItem(item);

            lock (_stateLock)
            {
                _postFlows.Remove(chatId);
                _postSteps[chatId] = PostStep.none;
            }

            await Client.AnswerCallbackQuery(query.Id, "Лот создан!");
            await Client.SendMessage(chatId, "Лот успешно создан и добавлен в аукцион.");
            return;
        }

        if (data.StartsWith("make_bid"))
        {
            // Берем id товара из команды
            var parts = data.Split(':');
            if (!(parts.Length == 2 && Guid.TryParse(parts[1], out Guid itemId)))
            {
                await Client.AnswerCallbackQuery(query.Id, "Неверные данные кнопки.");
                return;
            }

            var item = House.GetActiveItems().FirstOrDefault(x => x.Id == itemId);
            if (item == null)
            {
                await Client.AnswerCallbackQuery(query.Id, "Лот не найден.");
                return;
            }

            // Переводим в режим ввода суммы ставки
            await Client.AnswerCallbackQuery(query.Id, "Вы выбрали лот для ставки.");

            lock (_stateLock)
            {
                _pendingBids[chatId] = itemId;
                _postSteps[chatId] = PostStep.bid;
            }

            await Client.SendMessage(chatId, $"Введите ставку (мин.: {item.CurrentPrice * AuctionItem.MinBidMultiplier}₽)", parseMode: ParseMode.Html);
        }
    }

    /// <summary>
    /// Уведомляем победителя лота
    /// </summary>
    /// <param name="item">Завершенный лот</param>
    public async Task EndAuctionAsync(AuctionItem item)
    {
        _rwl.EnterWriteLock();
        try
        {
            item.EndAuction();

            UserAccount winner = item.HighestBidder;
            // Уведомляем создателя
            await Client.SendMessage(
                chatId: item.Creator.Id,
                text: $"🎉Аукцион по вашему лоту <b>\"{item.Name}\"</b> завершен!\n\n<b>Итоговая цена:</b> {item.CurrentPrice}₽\n<b>Победитель:</b> @{winner.Username}",
                parseMode: ParseMode.Html
            );

            // Уведомляем победителя, если он не создатель
            if (winner != null && winner.Id != item.Creator.Id)
            {
                await Client.SendMessage(
                    chatId: item.HighestBidder.Id,
                    text: $"🎉Вы выиграли аукцион по лоту <b>\"{item.Name}\"</b> за <b>{item.CurrentPrice}₽</b>\nСвяжитесь с владельцем: <b>@{item.Creator.Username}</b>",
                    parseMode: ParseMode.Html
                );
            }
        }
        finally
        {
            _rwl.ExitWriteLock();
        }
    }
}