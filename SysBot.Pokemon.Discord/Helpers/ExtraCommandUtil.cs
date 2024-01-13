using Discord;
using Discord.Commands;
using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using PKHeX.Core;

namespace SysBot.Pokemon.Discord
{
    public class ExtraCommandUtil<T> where T : PKM, new()
    {
        private static TradeQueueInfo<T> Info => SysCord<T>.Runner.Hub.Queues.Info;
        private static readonly PokeTradeHubConfig Config = Info.Hub.Config;
        private static readonly Dictionary<ulong, ReactMessageContents> ReactMessageDict = new();
        private static bool DictWipeRunning = false;

        private class ReactMessageContents
        {
            public List<string> Pages { get; set; } = new();
            public EmbedBuilder Embed { get; set; } = new();
            public ulong MessageID { get; set; }
            public DateTime EntryTime { get; set; }
        }

        private static async Task DictWipeMonitor()
        {
            DictWipeRunning = true;
            while (true)
            {
                await Task.Delay(10_000).ConfigureAwait(false);
                for (int i = 0; i < ReactMessageDict.Count; i++)
                {
                    var entry = ReactMessageDict.ElementAt(i);
                    var delta = (DateTime.Now - entry.Value.EntryTime).TotalSeconds;
                    if (delta > 90.0)
                        ReactMessageDict.Remove(entry.Key);
                }
            }
        }

        public async Task<bool> ReactionVerification(SocketCommandContext ctx)
        {
            var sw = new Stopwatch();
            IEmote reaction = new Emoji("👍");
            var msg = await ctx.Channel.SendMessageAsync($"{ctx.User.Username}, please react to the attached emoji in order to confirm you're not using a script.").ConfigureAwait(false);
            await msg.AddReactionAsync(reaction).ConfigureAwait(false);

            sw.Start();
            while (sw.ElapsedMilliseconds < 20_000)
            {
                await msg.UpdateAsync().ConfigureAwait(false);
                var react = msg.Reactions.FirstOrDefault(x => x.Value.ReactionCount > 1 && x.Value.IsMe);
                if (react.Key == default)
                    continue;

                if (react.Key.Name == reaction.Name)
                {
                    var reactUsers = await msg.GetReactionUsersAsync(reaction, 100).FlattenAsync().ConfigureAwait(false);
                    var usr = reactUsers.FirstOrDefault(x => x.Id == ctx.User.Id && !x.IsBot);
                    if (usr == default)
                        continue;

                    await msg.AddReactionAsync(new Emoji("✅")).ConfigureAwait(false);
                    return false;
                }
            }
            await msg.AddReactionAsync(new Emoji("❌")).ConfigureAwait(false);
            return true;
        }

        public async Task EmbedUtil(SocketCommandContext ctx, string name, string value, EmbedBuilder? embed = null)
        {
            embed ??= new EmbedBuilder { Color = GetBorderColor(false) };

            var splitName = name.Split(new string[] { "&^&" }, StringSplitOptions.None);
            var splitValue = value.Split(new string[] { "&^&" }, StringSplitOptions.None);

            for (int i = 0; i < splitName.Length; i++)
            {
                embed.AddField(x =>
                {
                    x.Name = splitName[i];
                    x.Value = splitValue[i];
                    x.IsInline = false;
                });
            }
            await ctx.Message.Channel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        private static List<string> SpliceAtWord(string entry, int start, int length)
        {
            int counter = 0;
            List<string> list = new();
            var temp = entry.Contains(',') ? entry.Split(',').Skip(start) : entry.Contains('|') ? entry.Split('|').Skip(start) : entry.Split('\n').Skip(start);

            if (entry.Length < length)
            {
                list.Add(entry ?? "");
                return list;
            }

            foreach (var line in temp)
            {
                counter += line.Length + 2;
                if (counter < length)
                    list.Add(line.Trim());
                else break;
            }
            return list;
        }
        public Color GetBorderColor(bool gift, PKM? pkm = null)
        {
            bool swsh = typeof(T) == typeof(PK8);
            if (pkm is null && swsh)
                return gift ? Color.Purple : Color.Blue;
            else if (pkm is null && !swsh)
                return gift ? Color.DarkPurple : Color.DarkBlue;
            else if (pkm is not null && swsh)
                return (pkm.IsShiny && pkm.FatefulEncounter) || pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.LightOrange : Color.Teal;
            else if (pkm is not null && !swsh)
                return (pkm.IsShiny && pkm.FatefulEncounter) || pkm.ShinyXor == 0 ? Color.Gold : pkm.IsShiny ? Color.DarkOrange : Color.DarkTeal;
            throw new NotImplementedException();
        }
    }
}
