using AuctionSystem.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AuctionSystem.Infrastructure.Telegram
{
    public class TelegramBotHost : IBotHost
    {
        private TelegramBotClient Client { get; set; }
        private TelegramUpdateRouter UpdateRouter { get; set; }

        public TelegramBotHost(TelegramBotClient client, TelegramUpdateRouter updateRouter)
        {
            Client = client;
            UpdateRouter = updateRouter;
        }

        public void Start()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
                DropPendingUpdates = true
            };
            Client.StartReceiving(HandleUpdate, HandleError, receiverOptions);
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
        /// Обработчик сообщений: направляет в OnMessage или OnCallback
        /// </summary>
        private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            await UpdateRouter.Route(update);
        }

    }
}
