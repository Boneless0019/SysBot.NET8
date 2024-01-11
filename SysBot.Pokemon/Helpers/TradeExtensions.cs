using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Base;

namespace SysBot.Pokemon;

public interface ITradePartner
{
    public uint TID7 { get; }
    public uint SID7 { get; }
    public string OT { get; }
    public int Game { get; }
    public int Gender { get; }
    public int Language { get; }
}

public class TradeExtensions<T> where T : PKM, new()
{
    public static bool CanUsePartnerDetails(RoutineExecutor<PokeBotState> executor, T pk, SaveFile sav, ITradePartner partner, PokeTradeDetail<T> trade, PokeTradeHubConfig config, out T res)
    {
        void Log(string msg) => executor.Log(msg);

        res = (T)pk.Clone();

        res.OT_Name = partner.OT;
        res.OT_Gender = partner.Gender;
        res.TrainerTID7 = partner.TID7;
        res.TrainerSID7 = partner.SID7;
        res.Language = partner.Language;
        res.Version = partner.Game;

        if (!pk.IsNicknamed)
            res.ClearNickname();

        if (pk.IsShiny)
            res.PID = (uint)((res.TID16 ^ res.SID16 ^ (res.PID & 0xFFFF) ^ pk.ShinyXor) << 16) | (res.PID & 0xFFFF);

        if (!pk.ChecksumValid)
            res.RefreshChecksum();

        var la = new LegalityAnalysis(res);
        if (!la.Valid)
        {
            res.Version = pk.Version;

            if (!pk.ChecksumValid)
                res.RefreshChecksum();

            la = new LegalityAnalysis(res);

            if (!la.Valid)
            {
                if (!config.Legality.ForceTradePartnerInfo)
                {
                    Log("Can not apply Partner details:");
                    Log(la.Report());
                    return false;
                }

                Log("Trying to force Trade Partner Info discarding the game version...");
                res.Version = pk.Version;

                if (!pk.ChecksumValid)
                    res.RefreshChecksum();

                la = new LegalityAnalysis(res);
                if (!la.Valid)
                {
                    Log("Can not apply Partner details:");
                    Log(la.Report());
                    return false;
                }
            }
        }

        Log($"Applying trade partner details: {partner.OT} ({(partner.Gender == 0 ? "M" : "F")}), " +
                $"TID: {partner.TID7:000000}, SID: {partner.SID7:0000}, {(LanguageID)partner.Language} ({(GameVersion)res.Version})");

        return true;
    }

    private static bool HasSetDetails(PokeTradeHubConfig config, PKM set, ITrainerInfo fallback)
    {
        var set_trainer = new SimpleTrainerInfo((GameVersion)set.Version)
        {
            OT = set.OT_Name,
            TID16 = set.TID16,
            SID16 = set.SID16,
            Gender = set.OT_Gender,
            Language = set.Language,
        };

        var def_trainer = new SimpleTrainerInfo((GameVersion)fallback.Game)
        {
            OT = config.Legality.GenerateOT,
            TID16 = config.Legality.GenerateTID16,
            SID16 = config.Legality.GenerateSID16,
            Gender = config.Legality.GenerateGenderOT,
            Language = (int)config.Legality.GenerateLanguage,
        };

        var alm_trainer = config.Legality.GeneratePathTrainerInfo != string.Empty ?
            TrainerSettings.GetSavedTrainerData(fallback.Generation, (GameVersion)fallback.Game, fallback, (LanguageID)fallback.Language) : null;

        return !IsEqualTInfo(set_trainer, def_trainer) && !IsEqualTInfo(set_trainer, alm_trainer);
    }

    private static bool IsEqualTInfo(ITrainerInfo trainerInfo, ITrainerInfo? compareInfo)
    {
        if (compareInfo is null)
            return false;

        if (!trainerInfo.OT.Equals(compareInfo.OT))
            return false;

        if (trainerInfo.Gender != compareInfo.Gender)
            return false;

        if (trainerInfo.Language != compareInfo.Language)
            return false;

        if (trainerInfo.TID16 != compareInfo.TID16)
            return false;

        if (trainerInfo.SID16 != compareInfo.SID16)
            return false;

        return true;
    }

    public static void EggTrade(PKM pk, IBattleTemplate template)
    {
        pk.IsNicknamed = true;
        pk.Nickname = pk.Language switch
        {
            1 => "タマゴ",
            3 => "Œuf",
            4 => "Uovo",
            5 => "Ei",
            7 => "Huevo",
            8 => "알",
            9 or 10 => "蛋",
            _ => "Egg",
        };

        pk.IsEgg = true;
        pk.Egg_Location = pk switch
        {
            PB8 => 60010,
            PK9 => 30023,
            _ => 60002, //PK8
        };

        pk.MetDate = DateOnly.FromDateTime(DateTime.Today);
        pk.EggMetDate = pk.MetDate;
        pk.HeldItem = 0;
        pk.CurrentLevel = 1;
        pk.EXP = 0;
        pk.Met_Level = 1;
        pk.Met_Location = pk switch
        {
            PB8 => 65535,
            PK9 => 0,
            _ => 30002, //PK8
        };

        pk.CurrentHandler = 0;
        pk.OT_Friendship = 1;
        pk.HT_Name = "";
        pk.HT_Friendship = 0;
        pk.ClearMemories();
        pk.StatNature = pk.Nature;
        pk.SetEVs(new int[] { 0, 0, 0, 0, 0, 0 });

        pk.SetMarking(0, 0);
        pk.SetMarking(1, 0);
        pk.SetMarking(2, 0);
        pk.SetMarking(3, 0);
        pk.SetMarking(4, 0);
        pk.SetMarking(5, 0);

        pk.ClearRelearnMoves();

        if (pk is PK8 pk8)
        {
            pk8.HT_Language = 0;
            pk8.HT_Gender = 0;
            pk8.HT_Memory = 0;
            pk8.HT_Feeling = 0;
            pk8.HT_Intensity = 0;
            pk8.DynamaxLevel = pk8.GetSuggestedDynamaxLevel(pk8, 0);
        }
        else if (pk is PB8 pb8)
        {
            pb8.HT_Language = 0;
            pb8.HT_Gender = 0;
            pb8.HT_Memory = 0;
            pb8.HT_Feeling = 0;
            pb8.HT_Intensity = 0;
            pb8.DynamaxLevel = pb8.GetSuggestedDynamaxLevel(pb8, 0);
        }
        else if (pk is PK9 pk9)
        {
            pk9.HT_Language = 0;
            pk9.HT_Gender = 0;
            pk9.HT_Memory = 0;
            pk9.HT_Feeling = 0;
            pk9.HT_Intensity = 0;
            pk9.Obedience_Level = 1;
            pk9.Version = 0;
            pk9.BattleVersion = 0;
            pk9.TeraTypeOverride = (MoveType)19;
        }

        pk = TrashBytes(pk);
        var la = new LegalityAnalysis(pk);
        var enc = la.EncounterMatch;
        pk.SetSuggestedRibbons(template, enc, true);
        pk.SetSuggestedMoves();
        la = new LegalityAnalysis(pk);
        enc = la.EncounterMatch;
        pk.CurrentFriendship = enc is IHatchCycle h ? h.EggCycles : pk.PersonalInfo.HatchCycles;

        Span<ushort> relearn = stackalloc ushort[4];
        la.GetSuggestedRelearnMoves(relearn, enc);
        pk.SetRelearnMoves(relearn);

        pk.Move1_PPUps = pk.Move2_PPUps = pk.Move3_PPUps = pk.Move4_PPUps = 0;
        pk.SetMaximumPPCurrent(pk.Moves);
        pk.SetSuggestedHyperTrainingData();
    }

    public static PKM TrashBytes(PKM pkm, LegalityAnalysis? la = null)
    {
        var pkMet = (T)pkm.Clone();
        if (pkMet.Version is not (int)GameVersion.GO)
            pkMet.MetDate = DateOnly.FromDateTime(DateTime.Now);

        var analysis = new LegalityAnalysis(pkMet);
        var pkTrash = (T)pkMet.Clone();
        if (analysis.Valid)
        {
            pkTrash.IsNicknamed = true;
            pkTrash.Nickname = "MANUMANUMANU";
            pkTrash.SetDefaultNickname(la ?? new LegalityAnalysis(pkTrash));
        }

        if (new LegalityAnalysis(pkTrash).Valid)
            pkm = pkTrash;
        else if (analysis.Valid)
            pkm = pkMet;
        return pkm;
    }

    /// <summary>
    /// Asynchronously fetches the image URL for a given item name from specified sources.
    /// If the item image is not found, returns a default 'no image' URL.
    /// </summary>
    /// <param name="itemName">The name of the item to fetch the image for.</param>
    /// <returns>The URL of the item image or a default image URL if not found.</returns>
    public static async Task<string> ItemImg(string itemName)
    {
        // Sanitize the item name to remove any non-word characters and convert to lower case.
        string sanitizedItemName = Regex.Replace(itemName, @"[^\w\.\-]+", "").ToLower();

        // Define a list of URL patterns where the item images can be found.
        var urlPatterns = new List<string>
    {
        "https://www.serebii.net/itemdex/sprites/pgl/{0}.png",
        "https://www.serebii.net/itemdex/sprites/sv/{0}.png",
        "https://www.serebii.net/itemdex/sprites/legends/{0}.png",
        "https://www.serebii.net/itemdex/sprites/{0}.png",
    };

        // Check each URL pattern to find a valid image URL.
        foreach (var pattern in urlPatterns)
        {
            string testUrl = string.Format(pattern, sanitizedItemName);
            if (await IsUrlValid(testUrl))
            {
                return testUrl;
            }
        }

        // Return a default image URL if no valid image URL is found.
        return "https://sysbots.net/wp-content/uploads/2023/12/No-image-found.jpg";
    }

    private static async Task<bool> IsUrlValid(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await StaticHttpClient.Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"Exception occurred while checking URL: {url} - {ex.Message}");
            return false;
        }
    }

    public static class StaticHttpClient
    {
        public static readonly HttpClient Client = new();
    }



    private static async Task<bool> IsUrlValid(HttpClient client, string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogUtil.LogText($"Exception occurred while checking URL: {url} - {ex.Message}");
            return false;
        }
    }

    public static string PokeImg(PKM pkm, bool canGmax, bool fullSize)
    {
        // Base URL setup
        string baseUrl = fullSize
            ? "https://raw.githubusercontent.com/Poke-Legend/HomeImages/master/128x128/poke_capture_"
            : "https://raw.githubusercontent.com/Poke-Legend/HomeImages/master/128x128/poke_capture_";

        // Format species and form
        string speciesFormatted = pkm.Species.ToString("D4");
        int form = DetermineForm(pkm, canGmax);
        string formFormatted = form.ToString("D3");

        // Determine gender code
        string genderCode = DetermineGenderCode(pkm);

        // Special handling for Sneasel and Basculegion
        HandleSpecialSpecies(pkm, ref genderCode, ref form);

        // Construct the image URL
        string shinyCode = pkm.IsShiny ? "r" : "n";
        string gmaxCode = canGmax ? "g" : "n";
        string alcremieSuffix = pkm.Species == (int)Species.Alcremie && !canGmax ? $"0000000{pkm.Data[0xD0]}" : "00000000";

        return $"{baseUrl}{speciesFormatted}_{formFormatted}_{genderCode}_{gmaxCode}_{alcremieSuffix}_f_{shinyCode}.png";
    }

    private static string DetermineGenderCode(PKM pkm)
    {
        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && pkm.Form == 0)
            return pkm.Gender == 0 ? "md" : "fd";

        return pkm.PersonalInfo switch
        {
            { OnlyFemale: true } => "fo",
            { OnlyMale: true } => "mo",
            { Genderless: true } => "uk",
            _ => "mf"
        };
    }

    private static int DetermineForm(PKM pkm, bool canGmax)
    {
        return pkm.Species switch
        {
            (int)Species.Sinistea or (int)Species.Polteageist or (int)Species.Rockruff or (int)Species.Mothim => 0,
            (int)Species.Alcremie when pkm.IsShiny || canGmax => 0,
            _ => pkm.Form,
        };
    }

    private static void HandleSpecialSpecies(PKM pkm, ref string genderCode, ref int form)
    {
        if (pkm.Species == (ushort)Species.Sneasel || pkm.Species == (ushort)Species.Basculegion)
        {
            genderCode = pkm.Gender == 0 ? "md" : "fd";
            form = pkm.Species == (ushort)Species.Basculegion ? pkm.Gender : form;
        }
    }

    public static string FormOutput(ushort species, byte form, out string[] formString)
    {
        var strings = GameInfo.GetStrings("en");
        formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(T) == typeof(PK9) ? EntityContext.Gen9 : EntityContext.Gen4);
        if (formString.Length is 0)
            return string.Empty;

        formString[0] = "";
        if (form >= formString.Length)
            form = (byte)(formString.Length - 1);
        return formString[form].Contains('-') ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
    }

    public static bool HasMark(IRibbonIndex pk, out RibbonIndex result)
    {
        result = default;
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                result = mark;
                return true;
            }
        }
        return false;
    }

    public static readonly string[] MarkTitle =
    [
        " the Peckish"," the Sleepy"," the Dozy"," the Early Riser"," the Cloud Watcher"," the Sodden"," the Thunderstruck"," the Snow Frolicker"," the Shivering"," the Parched"," the Sandswept"," the Mist Drifter",
        " the Chosen One"," the Catch of the Day"," the Curry Connoisseur"," the Sociable"," the Recluse"," the Rowdy"," the Spacey"," the Anxious"," the Giddy"," the Radiant"," the Serene"," the Feisty"," the Daydreamer",
        " the Joyful"," the Furious"," the Beaming"," the Teary-Eyed"," the Chipper"," the Grumpy"," the Scholar"," the Rampaging"," the Opportunist"," the Stern"," the Kindhearted"," the Easily Flustered"," the Driven",
        " the Apathetic"," the Arrogant"," the Reluctant"," the Humble"," the Pompous"," the Lively"," the Worn-Out", " of the Distant Past", " the Twinkling Star", " the Paldea Champion", " the Great", " the Teeny", " the Treasure Hunter",
        " the Reliable Partner", " the Gourmet", " the One-in-a-Million", " the Former Alpha", " the Unrivaled", " the Former Titan",
    ];
}
