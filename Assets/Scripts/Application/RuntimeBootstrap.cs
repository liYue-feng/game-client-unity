using UnityEngine;

namespace Game
{
    internal static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            GameApplication.ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateApplication()
        {
            EnsureApplication();
        }

        internal static void EnsureApplication()
        {
            if (GameApplication.HasInstance)
            {
                return;
            }

            new GameObject("[GameApplication]").AddComponent<GameApplication>();
        }
    }
}
