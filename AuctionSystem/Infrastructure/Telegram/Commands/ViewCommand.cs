using AuctionSystem.Domain;
using AuctionSystem.Models;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    public class ViewCommand : ITelegramCommand
    {
        public string Name => "Просмотреть лоты";

        private readonly IAuctionService _auctionService;
        private readonly IMessageSender _sender;

        public ViewCommand(IAuctionService auctionService, IMessageSender sender)
        {
            _auctionService = auctionService;
            _sender = sender;
        }

        /// <summary>
        /// Вывод активных лотов и кнопки для ставки
        /// </summary>
        public async Task ExecuteAsync(Message message)
        {
            Chat chat = message.Chat;
            long chatId = chat.Id;

            List<AuctionItem> activeItems = _auctionService.GetActiveItems();
            if (activeItems.Count == 0)
            {
                await _sender.SendMessage(chatId, "Пока никаких объявлений!");
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

                await _sender.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromFileId(item.ImageId),
                    caption: caption,
                    replyMarkup: keyboard
                );
            }
        }
    }
}
