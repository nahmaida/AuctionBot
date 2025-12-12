using AuctionSystem.Domain;
using AuctionSystem.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    internal class StartCommand : ITelegramCommand
    {
        public string Name => "/start";

        private readonly IUserService _users;
        private readonly IMessageSender _sender;

        public StartCommand(IUserService users, IMessageSender sender)
        {
            _users = users;
            _sender = sender;
        }

        /// <summary>
        /// Регистрация пользователя и вывод стартового меню
        /// </summary>

        public async Task ExecuteAsync(Message message)
        {
            Chat chat = message.Chat;
            long chatId = chat.Id;

            if (_users.TryGetUser(chatId, out var _))
            {
                await _sender.SendMessage(chatId, "Вы уже зарегистрированы!");
                return;
            }

            UserAccount user = new UserAccount(chatId, chat.Username ?? "Аноним");
            _users.AddUser(user);

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

            await _sender.SendMessage(
                chatId,
                "Добро пожаловать в Gambling Empire, аукцион номер 1 в П312💹\n\n💰Стартовый баланс: 10000₽",
                replyMarkup: replyKeyboard
            );
        }
    }
}
