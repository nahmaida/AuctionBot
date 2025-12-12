using AuctionSystem.Application;
using AuctionSystem.Domain;
using AuctionSystem.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem.Infrastructure.Telegram
{
    public class TelegramUpdateRouter
    {
        private readonly IAuctionService auctionService;
        private readonly IUserService userService;
        private readonly IConversationStateStore state;
        private readonly IMessageSender sender;

        public TelegramUpdateRouter(
            IMessageSender sender,
            IAuctionService auctionService,
            IUserService userService,
            IConversationStateStore state)
        {
            this.sender = sender;
            this.auctionService = auctionService;
            this.userService = userService;
            this.state = state;
        }

        public async Task Route(Update update)
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
        /// Обработка нажатий кнопок ТОЛЬКО для текущего chatId.
        /// Запрос от пользователи (нажатая кнопка)
        /// </summary>
        private async Task OnCallback(CallbackQuery query)
        {
            var chatId = query.Message!.Chat.Id;
            var data = query.Data;
            if (data == null) return;

            if (data == "post_accept" || data == "post_discard")
            {
                var flow = state.GetFlow(chatId);

                if (flow == null)
                {
                    await sender.AnswerCallbackQuery(query.Id, "Нет активного лота для подтверждения.");
                    return;
                }

                if (data == "post_discard")
                {
                    state.ClearFlow(chatId);
                    state.SetStep(chatId, PostStep.none);

                    await sender.AnswerCallbackQuery(query.Id, "Лот отменён.");
                    await sender.SendMessage(chatId, "Создание лота отменено.");
                    return;
                }

                // Принять (создать лот)
                var chat = query.Message.Chat;
                var creator = new UserAccount(chatId, chat.Username ?? "Аноним");
                var item = new AuctionItem(
                    name: flow.Name!,
                    description: flow.Description!,
                    imageId: flow.ImageId!,
                    initialPrice: flow.Price!.Value,
                    creator: creator,
                    duration: TimeSpan.FromMinutes(flow.Duration ?? 1)
                );

                auctionService.AddItem(item);

                state.ClearFlow(chatId);
                state.SetStep(chatId, PostStep.none);

                await sender.AnswerCallbackQuery(query.Id, "Лот создан!");
                await sender.SendMessage(chatId, "Лот успешно создан и добавлен в аукцион.");
                return;
            }

            if (data.StartsWith("make_bid"))
            {
                // Берем id товара из команды
                var parts = data.Split(':');
                if (!(parts.Length == 2 && Guid.TryParse(parts[1], out Guid itemId)))
                {
                    await sender.AnswerCallbackQuery(query.Id, "Неверные данные кнопки.");
                    return;
                }

                if (!auctionService.TryGetItem(itemId, out AuctionItem? item))
                {
                    await sender.AnswerCallbackQuery(query.Id, "Лот не найден.");
                    return;
                }

                // Переводим в режим ввода суммы ставки
                await sender.AnswerCallbackQuery(query.Id, "Вы выбрали лот для ставки.");

                state.SetPendingBid(chatId, itemId);
                state.SetStep(chatId, PostStep.bid);

                await sender.SendMessage(
                    chatId,
                    $"Введите ставку (мин.: {item.CurrentPrice * AuctionItem.MinBidMultiplier}₽)"
                );
            }
        }

        /// <summary>
        /// Обработка входящих сообщений
        /// ВАЖНО: все состояние проверяется ТОЛЬКО для текущего чата
        /// </summary>
        /// <param name="update">Обновление от бота</param>
        private async Task OnMessage(Update update)
        {
            var message = update.Message;
            if (message == null) return;

            Chat chat = message.Chat;
            long chatId = chat.Id;

            // Получаем состояние чата (каждый вызов сам по себе потокобезопасен)
            PostFlow? flow = state.GetFlow(chatId);
            PostStep stepForChat = state.GetStep(chatId);
            Guid bidItemId;
            bool hasBid = state.TryGetPendingBid(chatId, out bidItemId);

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
                    await HandleViewBalance(chatId);
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
                    await sender.SendMessage(chatId, "Неверная команда!");
                    break;
            }
        }

        private async Task HandleViewBalance(long chatId)
        {
            if (!userService.TryGetUser(chatId, out UserAccount? user))
            {
                await sender.SendMessage(chatId, "Сначала зарегистрируйтесь! (/start)");
                return;
            }

            decimal balance = user.Balance;
            await sender.SendMessage(chatId, $"💰Баланс: {balance}₽");
        }

        /// <summary>
        /// Обработка ввода суммы ставки
        /// </summary>
        /// <param name="message">Сообщение с суммой</param>
        /// <param name="bidItemId">Id лота для ставки</param>
        private async Task HandleBidAmount(Message message, Guid bidItemId)
        {
            Chat chat = message.Chat;
            long chatId = chat.Id;

            if (!decimal.TryParse(message.Text, out var bidAmount) || bidAmount <= 0)
            {
                await sender.SendMessage(chatId, "Неверный формат суммы, введите число.");
                return;
            }

            if (!auctionService.TryGetItem(bidItemId, out AuctionItem? item))
            {
                await sender.SendMessage(chatId, "Неверный товар, повторите попытку.");

                // Сбрасываем режим ставки только для этого чата
                state.ClearPendingBid(chatId);
                state.SetStep(chatId, PostStep.none);

                return;
            }

            if (!userService.TryGetUser(chatId, out UserAccount? user))
            {
                await sender.SendMessage(chatId, "Сначала зарегистрируйтесь! (/start)");

                state.ClearPendingBid(chatId);
                state.SetStep(chatId, PostStep.none);

                return;
            }

            bool success = item.TryPlaceBid(user, bidAmount, out string error);

            // Сбрасываем состояние ставки для этого чата
            state.ClearPendingBid(chatId);
            state.SetStep(chatId, PostStep.none);

            if (!success)
            {
                await sender.SendMessage(chatId, error);
                return;
            }

            await sender.SendMessage(
                item.Creator.Id,
                $"⚠️Новая ставка на ваш лот {item.Name}\n\n@{user.Username}: {bidAmount}"
            );
            await sender.SendMessage(chatId, "🎉Успешно!");
        }

        /// <summary>
        /// Регистрация пользователя и вывод стартового меню
        /// </summary>
        /// <param name="chat">Текущий чат</param>
        private async Task HandleStart(Chat chat)
        {
            long chatId = chat.Id;

            if (userService.TryGetUser(chatId, out var _))
            {
                await sender.SendMessage(chatId, "Вы уже зарегистрированы!");
                return;
            }

            UserAccount user = new UserAccount(chatId, chat.Username ?? "Аноним");
            userService.AddUser(user);

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

            await sender.SendMessage(
                chatId,
                "Добро пожаловать в Gambling Empire, аукцион номер 1 в П312💹\n\n💰Стартовый баланс: 10000₽",
                replyMarkup: replyKeyboard
            );
        }

        /// <summary>
        /// Вывод активных лотов и кнопки для ставки
        /// Текущий чат
        /// </summary>
        private async Task HandleView(Chat chat)
        {
            long chatId = chat.Id;

            List<AuctionItem> activeItems = auctionService.GetActiveItems();
            if (activeItems.Count == 0)
            {
                await sender.SendMessage(chatId, "Пока никаких объявлений!");
                return;
            }

            foreach (AuctionItem item in activeItems)
            {
                if (!item.IsActive) continue;

                string caption = item.GetCaption();
                var keyboard = new InlineKeyboardMarkup(
                    new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "✅Сделать ставку",
                                $"make_bid:{item.Id}"
                            )
                        }
                    }
                );

                await sender.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromFileId(item.ImageId),
                    caption: caption,
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
            long chatId = chat.Id;

            var won = auctionService.GetWonItems(chatId);
            if (won.Count == 0)
            {
                await sender.SendMessage(chatId, "Вы пока ничего не выиграли!");
                return;
            }

            await sender.SendMessage(chatId, "✅Ваши выигрыши:");
            foreach (AuctionItem item in won)
            {
                string caption = item.GetCaption();
                await sender.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromFileId(item.ImageId),
                    caption: caption
                );
            }
        }

        /// <summary>
        /// Создания лота
        /// Создаём PostFlow и переводим шаг в name только для данного chat.Id.
        /// </summary>
        /// <param name="chat">Текущий чат</param>
        private async Task HandlePost(Chat chat)
        {
            long chatId = chat.Id;

            state.SetFlow(chatId, new PostFlow());
            state.SetStep(chatId, PostStep.name);

            await sender.SendMessage(chatId, "Введите название:");
        }

        /// <summary>
        /// Продолжение создания лота по текущему шагу
        /// </summary>
        /// <param name="message">Сообщение с данными</param>
        /// <param name="flow">Хранилище данных</param>
        /// <param name="step">Текущий шаг</param>
        private async Task ContinuePostFlow(Message message, PostFlow flow, PostStep step)
        {
            Chat chat = message.Chat;
            long chatId = chat.Id;

            // Максимальный баланс пользователей для проверки цены
            // (Не принимаем если никто не может поставить)
            decimal maxUserBalance = userService.GetMaxBidAmount(chatId);

            switch (step)
            {
                case PostStep.name:
                    if (message.Text == null)
                    {
                        await sender.SendMessage(chatId, "Неверное название, попробуйте ещё раз.");
                        return;
                    }

                    flow.Name = message.Text;
                    state.SetStep(chatId, PostStep.desc);

                    await sender.SendMessage(chatId, "Введите описание:");
                    break;

                case PostStep.desc:
                    if (message.Text == null)
                    {
                        await sender.SendMessage(chatId, "Неверное описание, попробуйте ещё раз.");
                        return;
                    }

                    flow.Description = message.Text;
                    state.SetStep(chatId, PostStep.img);

                    await sender.SendMessage(chatId, "Отправьте фото:");
                    break;

                case PostStep.img:
                    if (message.Photo == null)
                    {
                        await sender.SendMessage(chatId, "Отправьте фото:");
                        return;
                    }

                    // Берем самое большое фото
                    flow.ImageId = message.Photo[^1].FileId;
                    state.SetStep(chatId, PostStep.price);

                    await sender.SendMessage(chatId, $"Начальная цена (0-{maxUserBalance}):");
                    break;

                case PostStep.price:
                    if (!decimal.TryParse(message.Text, out var price) ||
                        price < 0 ||
                        price > maxUserBalance)
                    {
                        await sender.SendMessage(chatId, "Неверная цена, попробуйте ещё раз.");
                        return;
                    }

                    flow.Price = price;
                    state.SetStep(chatId, PostStep.duration);

                    await sender.SendMessage(chatId, "Длительность в минутах (1-1440):");
                    break;

                case PostStep.duration:
                    if (!double.TryParse(message.Text, out var duration) ||
                        duration < 1 ||
                        duration > 1440)
                    {
                        await sender.SendMessage(chatId, "Неверное время, попробуйте ещё раз.");
                        return;
                    }

                    flow.Duration = duration;

                    string caption =
                        $"Имя: {flow.Name}\n\n" +
                        $"Описание: {flow.Description}\n\n" +
                        $"💰 Наибольшая ставка: (Вы): {flow.Price}₽\n" +
                        $"👤 Создатель: (Вы)\n";

                    var keyboard = new InlineKeyboardMarkup(
                        new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("✅ Принять", "post_accept"),
                                InlineKeyboardButton.WithCallbackData("🗑 Отменить", "post_discard")
                            }
                        }
                    );

                    await sender.SendPhoto(
                        chatId: chatId,
                        photo: InputFile.FromFileId(flow.ImageId!),
                        caption: caption,
                        replyMarkup: keyboard
                    );

                    state.SetStep(chatId, PostStep.confirm);
                    break;

                case PostStep.confirm:
                    await sender.SendMessage(
                        chatId,
                        "Нажмите кнопку 'Принять' или 'Отменить' под предпросмотром."
                    );
                    break;
            }
        }
    }
}
