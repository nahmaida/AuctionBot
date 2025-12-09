using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace AuctionSystem.Models
{
    internal interface IMessageSender
    {
        Task AnswerCallbackQuery(string queryId, string message);
        Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null);
        Task SendPhoto(long chatId, InputFile photo, string caption, ReplyMarkup? replyMarkup = null);
    }
}