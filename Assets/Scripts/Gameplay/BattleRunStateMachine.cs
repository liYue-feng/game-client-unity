using System;

namespace Game.Gameplay
{
    public enum BattleRunState
    {
        Running,
        Victory,
        Defeat,
        Restarting,
        Disposed
    }

    public enum BattleRunOutcome
    {
        None,
        Victory,
        Defeat
    }

    public sealed class BattleRunStateMachine : IDisposable
    {
        public BattleRunStateMachine()
        {
            State = BattleRunState.Running;
            Outcome = BattleRunOutcome.None;
        }

        public BattleRunState State { get; private set; }

        public BattleRunOutcome Outcome { get; private set; }

        public bool TryComplete(BattleRunOutcome outcome)
        {
            if (State != BattleRunState.Running ||
                (outcome != BattleRunOutcome.Victory && outcome != BattleRunOutcome.Defeat))
            {
                return false;
            }

            Outcome = outcome;
            State = outcome == BattleRunOutcome.Victory
                ? BattleRunState.Victory
                : BattleRunState.Defeat;
            return true;
        }

        public bool BeginRestart()
        {
            if (State != BattleRunState.Victory && State != BattleRunState.Defeat)
            {
                return false;
            }

            State = BattleRunState.Restarting;
            return true;
        }

        public void Dispose()
        {
            State = BattleRunState.Disposed;
        }
    }
}
