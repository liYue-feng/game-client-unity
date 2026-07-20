using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Online
{
    public sealed class MenuSceneBuildSettingsTests
    {
        [Test]
        public void EnabledBuildScenes_ListMenuBeforeBattleWithoutDuplicates()
        {
            var enabledPaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(enabledPaths, Is.EqualTo(new[]
            {
                "Assets/Scenes/MenuScene.unity",
                "Assets/Scenes/BattleScene.unity"
            }));
        }
    }
}
