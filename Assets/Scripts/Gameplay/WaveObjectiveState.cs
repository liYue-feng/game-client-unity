namespace Game.Gameplay
{
    public readonly struct WaveObjectiveState
    {
        public WaveObjectiveState(int zeroBasedWave, int totalWaves, int aliveEnemies)
        {
            TotalWaves = System.Math.Max(0, totalWaves);
            DisplayWave = TotalWaves == 0
                ? 0
                : System.Math.Min(TotalWaves, System.Math.Max(0, zeroBasedWave) + 1);
            AliveEnemies = System.Math.Max(0, aliveEnemies);
        }

        public int DisplayWave { get; }
        public int TotalWaves { get; }
        public int AliveEnemies { get; }
        public string WaveText => $"\u6ce2\u6b21 {DisplayWave}/{TotalWaves}";
        public string AliveText => $"\u5269\u4f59 {AliveEnemies}";
    }
}
