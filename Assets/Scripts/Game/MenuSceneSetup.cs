using UnityEngine;

public sealed class MenuSceneSetup : MonoBehaviour
{
    private const string PresentationRootName = "[MenuScene]";

    private void Awake()
    {
        var presentationRoot = FindPresentationRoot();
        if (presentationRoot == null)
        {
            gameObject.name = PresentationRootName;
            return;
        }

        if (presentationRoot != gameObject)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        var presentationRoot = FindPresentationRoot();
        if (presentationRoot == null)
        {
            gameObject.name = PresentationRootName;
            presentationRoot = gameObject;
        }

        if (presentationRoot != gameObject)
        {
            return;
        }

        var menuUis = GetComponents<MainMenuUI>();
        if (menuUis.Length == 0)
        {
            gameObject.AddComponent<MainMenuUI>();
            return;
        }

        for (var index = 1; index < menuUis.Length; index++)
        {
            Destroy(menuUis[index]);
        }
    }

    private static GameObject FindPresentationRoot()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == PresentationRootName)
            {
                return root;
            }
        }

        return null;
    }
}
