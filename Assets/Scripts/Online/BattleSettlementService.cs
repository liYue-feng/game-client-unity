using System;
using Game.Gameplay;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class BattleSettlementService
    {
        private readonly NetworkClient _client;

        public BattleSettlementService(NetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public CombatResultReq CreateRequest(BattleRunOutcome outcome, CombatResultData data)
        {
            data = data ?? new CombatResultData();
            return new CombatResultReq
            {
                RunId = Guid.NewGuid().ToString("N"),
                DungeonLevel = 1,
                Score = Math.Max(0, data.killCount) * 100L,
                Kills = Math.Max(0, data.killCount),
                DurationMs = Math.Max(0, data.survivalTime) * 1000L,
                StyleId = 1,
                Outcome = outcome == BattleRunOutcome.Victory
                    ? BattleOutcome.Victory
                    : BattleOutcome.Defeat,
                PlayerLevel = Math.Max(1, data.playerLevel)
            };
        }

        public bool Send(CombatResultReq request)
        {
            return request != null && _client.Send(MsgID.CombatResultReq, request);
        }
    }
}
