using System.Collections;
using System.Linq;
using System.Reflection;
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
