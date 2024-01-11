using PKHeX.Core;
using System;
using System.Buffers.Binary;

namespace SysBot.Pokemon
{
    public sealed class TradePartnerSV(TradeMyStatus info)
    {
        public TradeMyStatus Info { get; private set; } = info;

        public string TID7 { get => $"{Info.TID7:D6}"; }
        public string SID7 { get => $"{Info.SID7:D4}"; }
        public string TrainerName { get => Info.OT; }
    }

    public sealed class TradeMyStatus : ITradePartner
    {
        public readonly byte[] Data = new byte[0x30];

        public uint SID7 => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) / 1_000_000;
        public uint TID7 => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(0)) % 1_000_000;

        public int Game => Data[4];
        public int Gender => Data[5];
        public int Language => Data[6];

        public string OT => StringConverter8.GetString(Data.AsSpan(8, 24));
    }
}
