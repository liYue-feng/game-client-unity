using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 场景切换管理器 — 水墨晕染过渡 + 场景生命周期管理
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("过渡设置")]
    public float transitionDuration = 0.8f;

    private Texture2D _overlayTex;
    private float _transitionProgress;
    private bool _isTransitioning;
    private string _targetScene;
    private Action _onComplete;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// 切换到目标场景（带水墨晕染过渡）
    /// </summary>
    public void LoadScene(string sceneName, Action onComplete = null)
    {
        if (_isTransitioning) return;
        _targetScene = sceneName;
        _onComplete = onComplete;
        StartCoroutine(TransitionRoutine());
    }

    /// <summary>返回主菜单</summary>
    public void GoToMainMenu()
    {
        LoadScene("MenuScene");
    }

    IEnumerator TransitionRoutine()
    {
        _isTransitioning = true;

        // 阶段1: 墨色淡入
        yield return StartCoroutine(FadeInk(0f, 1f));

        // 阶段2: 加载场景
        var asyncOp = SceneManager.LoadSceneAsync(_targetScene);
        asyncOp.allowSceneActivation = false;

        // 等场景加载完成
        while (asyncOp.progress < 0.9f)
            yield return null;

        asyncOp.allowSceneActivation = true;

        // 阶段3: 墨色淡出
        yield return StartCoroutine(FadeInk(1f, 0f));

        _isTransitioning = false;
        _onComplete?.Invoke();
        _onComplete = null;
    }

    IEnumerator FadeInk(float from, float to)
    {
        var elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _transitionProgress = Mathf.Lerp(from, to, elapsed / transitionDuration);
            OnGUI(); // 触发一帧渲染
            yield return null;
        }
        _transitionProgress = to;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneTransition] 场景加载完成: {scene.name}");
    }

    void OnGUI()
    {
        if (!_isTransitioning) return;

        if (_overlayTex == null)
            _overlayTex = MakeInkOverlay(4, 4);

        GUI.color = new Color(0, 0, 0, _transitionProgress);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);
        GUI.color = Color.white;
    }

    Texture2D MakeInkOverlay(int w, int h)
    {
        var tex = new Texture2D(w, h);
        tex.SetPixel(0, 0, Color.black);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_overlayTex != null) Destroy(_overlayTex);
    }
}