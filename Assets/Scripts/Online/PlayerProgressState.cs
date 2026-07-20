using System;
using System.Collections.Generic;
using Game.Protocol;

namespace Game.Online
{
    public sealed class PlayerProgressState
    {
        private readonly int[] _unlockedStyles;

        private PlayerProgressState(PlayerArchive archive)
        {
            archive = archive ?? new PlayerArchive();
            SchemaVersion = archive.SchemaVersion;
            Gold = archive.Gold;
            Exp = archive.Exp;
            BestScore = archive.BestScore;
            TotalKills = archive.TotalKills;
            TotalGames = archive.TotalGames;
            HighestClearedDungeon = archive.HighestClearedDungeon;
            TalentPoints = archive.TalentPoints;
            _unlockedStyles = archive.UnlockedStyles == null
                ? Array.Empty<int>()
                : new List<int>(archive.UnlockedStyles).ToArray();
            LastStyleId = archive.LastStyleId;
        }

        public static PlayerProgressState Empty { get; } = new PlayerProgressState(new PlayerArchive());

        public int SchemaVersion { get; }
        public int Gold { get; }
        public int Exp { get; }
        public long BestScore { get; }
        public long TotalKills { get; }
        public long TotalGames { get; }
        public int HighestClearedDungeon { get; }
        public int TalentPoints { get; }
        public IReadOnlyList<int> UnlockedStyles => Array.AsReadOnly(_unlockedStyles);
        public int LastStyleId { get; }

        public static PlayerProgressState FromArchive(PlayerArchive archive)
        {
            return new PlayerProgressState(archive);
        }

        public PlayerArchive ToArchive()
        {
            var archive = new PlayerArchive
            {
                SchemaVersion = SchemaVersion,
                Gold = Gold,
                Exp = Exp,
                BestScore = BestScore,
                TotalKills = TotalKills,
                TotalGames = TotalGames,
                HighestClearedDungeon = HighestClearedDungeon,
                TalentPoints = TalentPoints,
                LastStyleId = LastStyleId
            };
            archive.UnlockedStyles.Add(_unlockedStyles);
            return archive;
        }
    }
}
