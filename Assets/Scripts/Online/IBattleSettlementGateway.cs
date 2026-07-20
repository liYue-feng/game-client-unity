using System;
using Game.Gameplay;
using Game.Protocol;

namespace Game.Online
{
    public enum BattleSettlementState
    {
        Pending,
        Saved,
        Failed
    }

    public sealed class BattleSettlementResult
    {
        public BattleSettlementState State { get; internal set; }
        public int RewardGold { get; internal set; }
        public int RewardExp { get; internal set; }
        public bool Duplicate { get; internal set; }
        public PlayerArchive Archive { get; internal set; }
    }

    public interface IBattleSettlementGateway
    {
        void Settle(BattleRunOutcome outcome, CombatResultData data, Action<BattleSettlementResult> completed);
        bool Retry();
    }

    public sealed class OfflineBattleSettlementGateway : IBattleSettlementGateway
    {
        public void Settle(BattleRunOutcome outcome, CombatResultData data, Action<BattleSettlementResult> completed)
        {
            completed?.Invoke(new BattleSettlementResult { State = BattleSettlementState.Saved });
        }

        public bool Retry()
        {
            return false;
        }
    }
}
