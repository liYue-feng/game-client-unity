using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    public sealed class OnlineStartupAndMenuTests
    {
        [UnityTest]
        public IEnumerator MenuScene_ProvidesOnlyLocalPresentationAndNavigatesBetweenMenuAndBattle()
        {
            yield return WaitForScene("BattleScene");
            yield return null;
            yield return LoadScene("MenuScene");
            yield return null;

            Assert.That(GameObject.Find("[MenuScene]"), Is.Not.Null);
            Assert.That(FindSceneObjects("MenuCanvas"), Has.Count.EqualTo(1));
            Assert.That(FindSceneObjects("BtnStart"), Has.Count.EqualTo(1));
            Assert.That(FindSceneObjects("BtnSettings"), Has.Count.EqualTo(1));
            foreach (var prohibitedComponent in new[] { "LoginManager", "ArchiveManager", "GameBootstrap" })
            {
                Assert.That(FindComponents(prohibitedComponent), Is.Empty,
                    $"MenuScene presentation must not create {prohibitedComponent}.");
            }

            FindSceneObjects("BtnStart").Single().GetComponent<Button>().onClick.Invoke();
            yield return WaitForScene("BattleScene");

            var transition = GameObject.Find("[SceneTransitionManager]")?.GetComponent("SceneTransitionManager");
            Assert.That(transition, Is.Not.Null);
            transition.GetType().GetField("transitionDuration", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(transition, 0f);
            transition.GetType().GetMethod("GoToMainMenu", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(transition, null);
            yield return WaitForScene("MenuScene");
            yield return null;

            Assert.That(FindSceneObjects("MenuCanvas"), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedOnlineStartupBeforeHostInstallation_KeepsBattleStartDisabled()
        {
            yield return WaitForScene("BattleScene");
            yield return WaitForApplicationState("Ready");

            var settings = Resources.Load("GameRuntimeSettings");
            var runtimeModeField = FindField(settings, "_runtimeMode");
            var onlineSceneField = FindField(settings, "_onlineStartupSceneName");
            var editorIdentityField = FindField(settings, "_editorLoginIdentity");
            var originalRuntimeMode = runtimeModeField.GetValue(settings);
            var originalOnlineScene = onlineSceneField.GetValue(settings);
            var originalEditorIdentity = editorIdentityField.GetValue(settings);
            var application = FindApplication();
            var applicationAssembly = application.GetType().Assembly;
            var observedState = string.Empty;
            var hostWasMissing = false;
            var startWasInteractable = true;

            try
            {
                runtimeModeField.SetValue(settings, System.Enum.Parse(runtimeModeField.FieldType, "Online"));
                onlineSceneField.SetValue(settings, "MenuScene");
                editorIdentityField.SetValue(settings, " ");
                ShutdownApplication(application);
                yield return null;

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Initialization failed at Settings\\.Validate:.*EditorLoginIdentity"));
                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("InvalidOperationException: EditorLoginIdentity cannot be null or whitespace in Online mode"));
                InvokeEnsureApplication(applicationAssembly);
                yield return WaitForApplicationState("Failed");
                yield return LoadScene("MenuScene");
                yield return null;

                observedState = GetApplicationState();
                hostWasMissing = GameObject.Find("[OnlineSessionHost]") == null;
                startWasInteractable = FindSceneObjects("BtnStart").Single().GetComponent<Button>().interactable;
            }
            finally
            {
                runtimeModeField.SetValue(settings, originalRuntimeMode);
                onlineSceneField.SetValue(settings, originalOnlineScene);
                editorIdentityField.SetValue(settings, originalEditorIdentity);
                ShutdownApplication(FindApplication());
                InvokeEnsureApplication(applicationAssembly);
            }

            yield return WaitForScene("BattleScene");
            yield return WaitForApplicationState("Ready");
            Assert.That(observedState, Is.EqualTo("Failed"));
            Assert.That(hostWasMissing, Is.True);
            Assert.That(startWasInteractable, Is.False,
                "A failed Online startup without an installed host must not fall back to Offline battle access.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            const int maxFrames = 240;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Timed out waiting for {sceneName}; active scene is {SceneManager.GetActiveScene().name}.");
        }

        private static IEnumerator WaitForApplicationState(string expectedState)
        {
            const int maxFrames = 240;
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (GetApplicationState() == expectedState)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Timed out waiting for GameApplication state {expectedState}; current state is {GetApplicationState()}.");
        }

        private static Component FindApplication()
        {
            return GameObject.Find("[GameApplication]")?
                .GetComponents<Component>()
                .Single(component => component != null && component.GetType().Name == "GameApplication");
        }

        private static string GetApplicationState()
        {
            var application = FindApplication();
            return application?.GetType()
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(application)
                ?.ToString();
        }

        private static void ShutdownApplication(Component application)
        {
            application?.GetType()
                .GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(application, null);
        }

        private static void InvokeEnsureApplication(Assembly applicationAssembly)
        {
            applicationAssembly.GetType("Game.RuntimeBootstrap")
                ?.GetMethod("EnsureApplication", BindingFlags.Static | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }

        private static FieldInfo FindField(Object target, string fieldName)
        {
            var field = target?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field {fieldName}.");
            return field;
        }

        private static System.Collections.Generic.List<GameObject> FindSceneObjects(string name)
        {
            var activeScene = SceneManager.GetActiveScene();
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == activeScene && item.name == name)
                .ToList();
        }

        private static System.Collections.Generic.List<Component> FindComponents(string typeName)
        {
            return Resources.FindObjectsOfTypeAll<Component>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && item.GetType().Name == typeName)
                .ToList();
        }
    }
}
