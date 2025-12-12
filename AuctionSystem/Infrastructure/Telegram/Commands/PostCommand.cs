using AuctionSystem.Application;
using AuctionSystem.Models;
using Telegram.Bot.Types;

namespace AuctionSystem.Infrastructure.Telegram.Commands
{
    public class PostCommand : ITelegramCommand
    {
        public string Name => "Выставить на аукцион";

        private readonly IConversationStateStore _state;
        private readonly IMessageSender _sender;

        public PostCommand(IConversationStateStore state, IMessageSender sender)
        {
            _state = state;
            _sender = sender;
        }

        /// <summary>
        /// Создания лота
        /// Создаём PostFlow и переводим шаг в name только для данного chat.Id.
        /// </summary>
        public async Task ExecuteAsync(Message message)
        {
            long chatId = message.Chat.Id;

            _state.SetFlow(chatId, new PostFlow());
            _state.SetStep(chatId, PostStep.name);

            await _sender.SendMessage(chatId, "Введите название:");
        }
    }
}
