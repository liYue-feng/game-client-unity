using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

public static class GameRuntimeSettingsAssetCreator
{
    private const string AssetPath = "Assets/Resources/GameRuntimeSettings.asset";

    [MenuItem("Game/Create Default Runtime Settings")]
    public static void CreateDefaultAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<GameRuntimeSettings>(AssetPath) != null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
    }
}
