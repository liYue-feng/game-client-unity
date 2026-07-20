using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace Game.Protocol
{
    public static class ProtocolMessageRegistry
    {
        private sealed class Entry
        {
            public Entry(Type messageType, Func<byte[], IMessage> parse)
            {
                MessageType = messageType;
                Parse = parse;
            }

            public Type MessageType { get; }

            public Func<byte[], IMessage> Parse { get; }
        }

        private static readonly Dictionary<ushort, Entry> Entries = new Dictionary<ushort, Entry>
        {
            { MsgID.LoginReq, Create(LoginReq.Parser) },
            { MsgID.LoginResp, Create(LoginResp.Parser) },
            { MsgID.HeartbeatReq, Create(HeartbeatReq.Parser) },
            { MsgID.HeartbeatResp, Create(HeartbeatResp.Parser) },
            { MsgID.SaveArchiveReq, Create(SaveArchiveReq.Parser) },
            { MsgID.SaveArchiveResp, Create(SaveArchiveResp.Parser) },
            { MsgID.LoadArchiveReq, Create(LoadArchiveReq.Parser) },
            { MsgID.LoadArchiveResp, Create(LoadArchiveResp.Parser) },
            { MsgID.GetRankReq, Create(GetRankReq.Parser) },
            { MsgID.GetRankResp, Create(GetRankResp.Parser) },
            { MsgID.SubmitScoreReq, Create(SubmitScoreReq.Parser) },
            { MsgID.SubmitScoreResp, Create(SubmitScoreResp.Parser) },
            { MsgID.CombatResultReq, Create(CombatResultReq.Parser) },
            { MsgID.CombatResultResp, Create(CombatResultResp.Parser) },
            { MsgID.GetEnemyConfigsReq, Create(GetEnemyConfigsReq.Parser) },
            { MsgID.GetEnemyConfigsResp, Create(GetEnemyConfigsResp.Parser) },
            { MsgID.GetDungeonConfigReq, Create(GetDungeonConfigReq.Parser) },
            { MsgID.GetDungeonConfigResp, Create(GetDungeonConfigResp.Parser) },
            { MsgID.GetStyleConfigsReq, Create(GetStyleConfigsReq.Parser) },
            { MsgID.GetStyleConfigsResp, Create(GetStyleConfigsResp.Parser) },
            { MsgID.UnlockStyleReq, Create(UnlockStyleReq.Parser) },
            { MsgID.UnlockStyleResp, Create(UnlockStyleResp.Parser) },
            { MsgID.GetPlayerStatsReq, Create(GetPlayerStatsReq.Parser) },
            { MsgID.GetPlayerStatsResp, Create(GetPlayerStatsResp.Parser) },
            { MsgID.UpdatePlayerStatsReq, Create(UpdatePlayerStatsReq.Parser) },
            { MsgID.UpdatePlayerStatsResp, Create(UpdatePlayerStatsResp.Parser) },
            { MsgID.CreateOrderReq, Create(CreateOrderReq.Parser) },
            { MsgID.CreateOrderResp, Create(CreateOrderResp.Parser) },
            { MsgID.PayResultNotify, Create(PayResultNotify.Parser) },
            { MsgID.GMCommandReq, Create(GMCommandReq.Parser) },
            { MsgID.GMCommandResp, Create(GMCommandResp.Parser) },
            { MsgID.Error, Create(ErrorResp.Parser) }
        };

        public static int Count => Entries.Count;

        public static bool TryGetMessageType(ushort msgID, out Type messageType)
        {
            if (Entries.TryGetValue(msgID, out var entry))
            {
                messageType = entry.MessageType;
                return true;
            }

            messageType = null;
            return false;
        }

        public static bool IsRegistered<T>(ushort msgID) where T : class, IMessage<T>
        {
            return TryGetMessageType(msgID, out var messageType) && messageType == typeof(T);
        }

        public static bool TryParse(ushort msgID, byte[] body, out IMessage message)
        {
            message = null;
            if (!Entries.TryGetValue(msgID, out var entry))
            {
                return false;
            }

            try
            {
                message = entry.Parse(body ?? Array.Empty<byte>());
                return true;
            }
            catch (InvalidProtocolBufferException)
            {
                return false;
            }
        }

        public static bool TryParse<T>(ushort msgID, byte[] body, out T message) where T : class, IMessage<T>
        {
            message = null;
            if (!IsRegistered<T>(msgID) || !TryParse(msgID, body, out var parsed))
            {
                return false;
            }

            message = parsed as T;
            return message != null;
        }

        private static Entry Create<T>(MessageParser<T> parser) where T : class, IMessage<T>
        {
            return new Entry(typeof(T), body => parser.ParseFrom(body));
        }
    }
}
