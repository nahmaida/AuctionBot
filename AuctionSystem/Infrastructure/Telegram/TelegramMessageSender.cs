using AuctionSystem.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem.Infrastructure.Telegram
{
    public class TelegramMessageSender : IMessageSender
    {
        private TelegramBotClient Client { get; set; }

        public TelegramMessageSender(TelegramBotClient client)
        {
            Client = client;
        }

        public async Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null)
        {
            await Client.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup
            );
        }

        public async Task SendPhoto(long chatId, InputFile photo, string caption, ReplyMarkup? replyMarkup = null)
        {
            await Client.SendPhoto(
                chatId: chatId,
                photo: photo,
                caption: caption,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup
            );
        }

        public async Task AnswerCallbackQuery(string queryId, string message)
        {
            await Client.AnswerCallbackQuery(queryId, message);
        }
    }
}