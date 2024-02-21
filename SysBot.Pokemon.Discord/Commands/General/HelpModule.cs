using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private IUserMessage _helpMessage;
        private int _currentPage = 1;
        private readonly List<IEmote> _pageEmojis = new List<IEmote>() { new Emoji("⬅️"), new Emoji("➡️") }; // Left and Right Arrow Emojis

        public HelpModule(CommandService service)
        {
            _service = service;
        }

        [Command("help")]
        [Summary("Lists available commands.")]
        public async Task HelpAsync()
        {
            await ShowHelpPage(_currentPage);
        }

        private async Task ShowHelpPage(int pageNumber)
        {
            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Title = "Help Page",
            };

            switch (pageNumber)
            {
                case 1:
                    builder.Description = "BatchEditingModule:\n- batchinfo\n- batchvalidate\n\nLegalityCheckModule:\n- lc\n- lcv\n\nHelloModule:\n- hello";
                    break;
                case 2:
                    builder.Description = "HelpModule:\n- help\n\nPingModule:\n- ping\n\nThankfulModule:\n- thankyou";
                    break;
                // Add cases for other pages...
                default:
                    builder.Description = "Invalid page number.";
                    break;
            }

            if (_helpMessage == null)
            {
                _helpMessage = await ReplyAsync("Help has arrived!", false, builder.Build());
                foreach (var emoji in _pageEmojis)
                {
                    await _helpMessage.AddReactionAsync(emoji);
                }

                var client = Context.Client as DiscordSocketClient;
                client.ReactionAdded += HandleReactionAsync;
                client.InteractionCreated += HandleInteractionAsync;
            }
            else
            {
                await _helpMessage.ModifyAsync(msg =>
                {
                    msg.Embed = builder.Build();
                    msg.Content = "Help has arrived!";
                });
            }
        }

        private Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction)
        {
            throw new NotImplementedException();
        }

        private async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (reaction.UserId == Context.Client.CurrentUser.Id || reaction.MessageId != _helpMessage.Id)
                return;

            var emoji = reaction.Emote;

            if (_pageEmojis.Contains(emoji))
            {
                int newPage = _currentPage;

                if (emoji.Name == "⬅️" && _currentPage > 1)
                    newPage--;
                else if (emoji.Name == "➡️" && _currentPage < MaxPageNumber()) // Define MaxPageNumber() method to return the total number of pages
                    newPage++;

                if (newPage != _currentPage)
                {
                    _currentPage = newPage;
                    await ShowHelpPage(_currentPage);
                }

                await _helpMessage.RemoveReactionAsync(emoji, reaction.User.Value);
            }
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            if (interaction is SocketMessageComponent component)
            {
                if (component.Data.CustomId == "declineBtn" && component.Message.Id == _helpMessage.Id)
                {
                    var builder = new EmbedBuilder
                    {
                        Color = new Color(114, 137, 218),
                        Title = "Help Page",
                        Description = "Your new content here"
                    };

                    await component.Message.ModifyAsync(msg => msg.Embed = builder.Build());
                }
            }
        }

        private int MaxPageNumber()
        {
            // Return the total number of pages
            return 2; // Adjust this according to the total number of pages
        }
    }
}
