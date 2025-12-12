using AuctionSystem.Domain;
using AuctionSystem.Models;
using Telegram.Bot.Types;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    public class ViewWonCommand : ITelegramCommand
    {
        public string Name => "Выигранные лоты";

        private readonly IAuctionService _auctionService;
        private readonly IMessageSender _sender;

        public ViewWonCommand(IAuctionService auctionService, IMessageSender sender)
        {
            _auctionService = auctionService;
            _sender = sender;
        }

        /// <summary>
        /// Выводим выигранные пользователем лоты
        /// </summary>
        public async Task ExecuteAsync(Message message)
        {
            long chatId = message.Chat.Id;

            var won = _auctionService.GetWonItems(chatId);
            if (won.Count == 0)
            {
                await _sender.SendMessage(chatId, "Вы пока ничего не выиграли!");
                return;
            }

            await _sender.SendMessage(chatId, "✅Ваши выигрыши:");
            foreach (AuctionItem item in won)
            {
                string caption = item.GetCaption();
                await _sender.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromFileId(item.ImageId),
                    caption: caption
                );
            }
        }
    }
}
