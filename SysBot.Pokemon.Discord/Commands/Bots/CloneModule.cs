using Discord;
using Discord.Commands;
using PKHeX.Core;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System;
using System.Threading.Tasks;
using static PKHeX.Core.AutoMod.Aesthetics;
using static PKHeX.Core.AutoMod.Aesthetics.PersonalColor;

namespace SysBot.Pokemon.Discord
{
    [Summary("Queues new Clone trades")]
    public class CloneModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CloneAsync(int code)
        {


            var iconURL = Context.User.GetAvatarUrl();
            var cloneMessage = $" You have been added to the Pokémon **Clone** queue. \n Check your DM's for further instructions.";
            var embedCloneMessage = new EmbedBuilder()
            {

                Author = new EmbedAuthorBuilder()
                {
                    Name = Context.User.Username,
                    IconUrl = iconURL
                },
                Color = new Color(0xFFFFFF)
        }
            .WithDescription(cloneMessage)
            .WithImageUrl("https://sysbots.net/wp-content/uploads/2023/12/render10square-removebg-preview.png")
            .WithThumbnailUrl("https://sysbots.net/wp-content/uploads/2023/08/logosysflyout.png")
            .WithCurrentTimestamp()
            .Build();

            await Context.Channel.SendMessageAsync(null, false, embedCloneMessage);

            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone).ConfigureAwait(false);
        }

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CloneAsync([Summary("Trade Code")][Remainder] string code)
        {
            int tradeCode = Util.ToInt32(code);
            var sig = Context.User.GetFavor();
            await QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Clone, PokeTradeType.Clone).ConfigureAwait(false);
        }

        [Command("clone")]
        [Alias("c")]
        [Summary("Clones the Pokémon you show via Link Trade.")]
        [RequireQueueRole(nameof(DiscordManager.RolesTrade))]
        public async Task CloneAsync()
        {
            var code = Info.GetRandomTradeCode();
            await CloneAsync(code).ConfigureAwait(false);
        }

        [Command("cloneList")]
        [Alias("cl", "cq")]
        [Summary("Prints the users in the Clone queue.")]
        [RequireSudo]
        public async Task GetListAsync()
        {
            string msg = Info.GetTradeList(PokeRoutineType.Clone);
            var embed = new EmbedBuilder();
            embed.AddField(x =>
            {
                x.Name = "Pending Trades";
                x.Value = msg;
                x.IsInline = false;
            });
            await ReplyAsync("These are the users who are currently waiting:", embed: embed.Build()).ConfigureAwait(false);
        }
    }
}
