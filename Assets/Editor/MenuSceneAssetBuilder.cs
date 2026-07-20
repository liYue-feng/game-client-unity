using System.IO;
using System.Text;
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

        var scene = File.Exists(MenuScenePath)
            ? EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var presentationRoot = NormalizeSceneRoots(scene);
        EnsureSingleSetup(presentationRoot);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
        NormalizeTrailingWhitespace(MenuScenePath);
        AssetDatabase.ImportAsset(MenuScenePath, ImportAssetOptions.ForceUpdate);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(BattleScenePath, true)
        };
        AssetDatabase.SaveAssets();
    }

    private static GameObject NormalizeSceneRoots(Scene scene)
    {
        GameObject presentationRoot = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (presentationRoot == null && root.name == "[MenuScene]")
            {
                presentationRoot = root;
                continue;
            }

            Object.DestroyImmediate(root);
        }

        if (presentationRoot == null)
        {
            presentationRoot = new GameObject("[MenuScene]");
            SceneManager.MoveGameObjectToScene(presentationRoot, scene);
        }

        return presentationRoot;
    }

    private static void EnsureSingleSetup(GameObject presentationRoot)
    {
        var setups = presentationRoot.GetComponents<MenuSceneSetup>();
        if (setups.Length == 0)
        {
            presentationRoot.AddComponent<MenuSceneSetup>();
            return;
        }

        for (var index = 1; index < setups.Length; index++)
        {
            Object.DestroyImmediate(setups[index]);
        }
    }

    private static void NormalizeTrailingWhitespace(string assetPath)
    {
        var fullPath = Path.GetFullPath(assetPath);
        var lines = File.ReadAllLines(fullPath);
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd(' ', '\t');
        }

        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }
}
