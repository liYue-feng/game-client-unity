using Game.Online;
using Game.Protocol;
using NUnit.Framework;

namespace Game.Tests.EditMode.Online
{
    public sealed class PlayerProgressStateTests
    {
        [Test]
        public void FromArchiveAndToArchiveDeepCopyEveryProgressField()
        {
            var source = new PlayerArchive
            {
                SchemaVersion = 2,
                Gold = 7,
                Exp = 11,
                BestScore = 123,
                TotalKills = 17,
                TotalGames = 3,
                HighestClearedDungeon = 4,
                TalentPoints = 5,
                UnlockedStyles = { 1, 3 },
                LastStyleId = 3
            };

            var progress = PlayerProgressState.FromArchive(source);
            source.Gold = 99;
            source.UnlockedStyles[0] = 99;

            Assert.That(progress.SchemaVersion, Is.EqualTo(2));
            Assert.That(progress.Gold, Is.EqualTo(7));
            Assert.That(progress.Exp, Is.EqualTo(11));
            Assert.That(progress.BestScore, Is.EqualTo(123));
            Assert.That(progress.TotalKills, Is.EqualTo(17));
            Assert.That(progress.TotalGames, Is.EqualTo(3));
            Assert.That(progress.HighestClearedDungeon, Is.EqualTo(4));
            Assert.That(progress.TalentPoints, Is.EqualTo(5));
            Assert.That(progress.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(progress.LastStyleId, Is.EqualTo(3));

            var output = progress.ToArchive();
            output.Gold = 55;
            output.UnlockedStyles[0] = 55;
            Assert.That(progress.Gold, Is.EqualTo(7));
            Assert.That(progress.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(progress.ToArchive().Gold, Is.EqualTo(7));
            Assert.That(progress.ToArchive().UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
        }
    }
}
