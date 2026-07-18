using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Game.Core;

/// <summary>
/// 加载界面：场景切换时的水墨过渡。
/// 淡入淡出 + 墨点动画 + 提示文字。
/// </summary>
public class LoadingScreen : MonoBehaviour, IGameService
{
    private static LoadingScreen _instance;
    public static LoadingScreen Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("[LoadingScreen] Service is not installed by GameApplication.");
            }

            return _instance;
        }
    }

    public string ServiceName => nameof(LoadingScreen);

    private Canvas _canvas;
    private Image _overlay;
    private Text _hintText;
    private Text _titleText;
    private bool _initialized;

    private static readonly string[] Hints =
    {
        "墨色浸染，剑气如虹",
        "见招拆招，以静制动",
        "一招一式，皆是修行",
        "行云流水，意在笔先",
        "大巧若拙，重剑无锋",
        "丹青不渝，武道无极"
    };

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    internal static LoadingScreen Install(Transform parent)
    {
        if (_instance != null)
        {
            return _instance;
        }

        var serviceObject = new GameObject("[LoadingScreen]");
        serviceObject.transform.SetParent(parent, false);
        return serviceObject.AddComponent<LoadingScreen>();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        BuildUI();
        _canvas.enabled = false;
        _initialized = true;
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        StopAllCoroutines();
        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        _canvas = null;
        _overlay = null;
        _hintText = null;
        _titleText = null;
        _initialized = false;
    }

    internal static void ResetStaticState()
    {
        _instance = null;
    }

    private void OnDestroy()
    {
        Shutdown();
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    private void BuildUI()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        // 全屏遮罩（宣纸色）
        var bgGo = new GameObject("Overlay");
        bgGo.transform.SetParent(transform, false);
        _overlay = bgGo.AddComponent<Image>();
        _overlay.color = new Color(0.96f, 0.94f, 0.89f, 1f);
        _overlay.rectTransform.anchorMin = Vector2.zero;
        _overlay.rectTransform.anchorMax = Vector2.one;
        _overlay.rectTransform.sizeDelta = Vector2.zero;

        // 标题
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(transform, false);
        _titleText = titleGo.AddComponent<Text>();
        _titleText.text = "剑";
        _titleText.fontSize = 80;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(0.1f, 0.1f, 0.1f);
        _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.55f);
        _titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
        _titleText.rectTransform.sizeDelta = new Vector2(200, 100);
        _titleText.rectTransform.anchoredPosition = Vector2.zero;

        // 提示文字
        var hintGo = new GameObject("Hint");
        hintGo.transform.SetParent(transform, false);
        _hintText = hintGo.AddComponent<Text>();
        _hintText.fontSize = 24;
        _hintText.alignment = TextAnchor.MiddleCenter;
        _hintText.color = new Color(0.3f, 0.3f, 0.3f);
        _hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hintText.rectTransform.anchorMin = new Vector2(0.5f, 0.42f);
        _hintText.rectTransform.anchorMax = new Vector2(0.5f, 0.42f);
        _hintText.rectTransform.sizeDelta = new Vector2(600, 40);
        _hintText.rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>显示加载界面，自动在loadOp完成后隐藏</summary>
    public void Show(AsyncOperation loadOp = null)
    {
        _hintText.text = Hints[Random.Range(0, Hints.Length)];
        _canvas.enabled = true;
        StartCoroutine(LoadRoutine(loadOp));
    }

    /// <summary>显示加载界面，手动控制隐藏</summary>
    public void ShowAndWait(float minDuration = 0.5f)
    {
        _hintText.text = Hints[Random.Range(0, Hints.Length)];
        _canvas.enabled = true;

        if (minDuration > 0)
        {
            StartCoroutine(AutoHideRoutine(minDuration));
        }
    }

    /// <summary>隐藏加载界面</summary>
    public void Hide()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator LoadRoutine(AsyncOperation op)
    {
        yield return null; // 等一帧让Canvas渲染

        if (op != null)
        {
            while (!op.isDone)
            {
                yield return null;
            }
        }

        yield return FadeOutRoutine();
    }

    private IEnumerator AutoHideRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return FadeOutRoutine();
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = 1f - elapsed / duration;
            _overlay.color = new Color(0.96f, 0.94f, 0.89f, a);
            _titleText.color = new Color(0.1f, 0.1f, 0.1f, a);
            _hintText.color = new Color(0.3f, 0.3f, 0.3f, a);
            yield return null;
        }

        _canvas.enabled = false;
    }
}
