using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MenuSceneAssetBuilder
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";

    [MenuItem("Game/Build Menu Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MenuScenePath));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var presentationRoot = new GameObject("[MenuScene]");
        presentationRoot.AddComponent<MenuSceneSetup>();
        EditorSceneManager.SaveScene(scene, MenuScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(BattleScenePath, true)
        };
        AssetDatabase.SaveAssets();
    }
}
