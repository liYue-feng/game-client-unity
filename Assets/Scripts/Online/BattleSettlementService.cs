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
                SurvivalTime = Math.Max(0, data.survivalTime),
                StyleId = 1,
                Outcome = outcome == BattleRunOutcome.Victory
                    ? BattleOutcome.Victory
                    : BattleOutcome.Defeat,
                PlayerLevel = Math.Max(1, data.playerLevel)
            };
        }

        public bool Send(
            CombatResultReq request,
            Action<CombatResultResp> onSuccess,
            Action<string> onFailure,
            out uint seq)
        {
            if (request == null)
            {
                seq = 0;
                return false;
            }

            return _client.Request<CombatResultReq, CombatResultResp>(
                MsgID.CombatResultReq,
                MsgID.CombatResultResp,
                request,
                onSuccess,
                onFailure,
                out seq);
        }
    }
}
