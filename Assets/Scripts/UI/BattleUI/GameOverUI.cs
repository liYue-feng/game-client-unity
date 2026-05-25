using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 游戏结束界面 — 死亡覆层 + 结算按钮
/// 通过 GameOverUI.Show() 静态方法调用
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    public event Action OnRestart;
    public event Action OnReturnToMenu;

    private GameObject _overlay;
    private Text _resultText;
    private Font _font;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary> 显示游戏结束界面 </summary>
    public static void Show(bool victory, CombatResultData data = null)
    {
        var go = new GameObject("GameOverUI_Runtime");
        var ui = go.AddComponent<GameOverUI>();
        ui.BuildAndShow(victory, data);
    }

    /// <summary> 在已有 GameOverUI 实例上显示 </summary>
    public void DisplayGameOver(bool victory, CombatResultData data = null)
    {
        BuildAndShow(victory, data);
    }

    void BuildAndShow(bool victory, CombatResultData data)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas
        var canvasGo = new GameObject("OverlayCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // 最顶层
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 半透明覆层
        _overlay = new GameObject("DarkOverlay");
        _overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImg = _overlay.AddComponent<RawImage>();
        overlayImg.texture = MakeOverlayTex(4, 4);
        overlayImg.color = new Color(0, 0, 0, 0.7f);
        var ovRect = _overlay.GetComponent<RectTransform>();
        ovRect.anchorMin = Vector2.zero;
        ovRect.anchorMax = Vector2.one;
        ovRect.sizeDelta = Vector2.zero;

        // 结果面板
        var panel = CreatePanel(canvasGo.transform, "ResultPanel", 600, 500);
        panel.transform.localPosition = Vector3.zero;

        // 标题
        var title = CreateText(panel.transform, "Title",
            victory ? "胜  利" : "落  败", 72, TextAnchor.MiddleCenter);
        title.color = victory
            ? new Color(0.65f, 0.15f, 0.15f)   // 朱砂红（胜）
            : new Color(0.1f, 0.1f, 0.1f);      // 墨黑（败）
        title.rectTransform.anchoredPosition = new Vector2(0, 160);
        title.rectTransform.sizeDelta = new Vector2(400, 100);

        // 结算数据
        if (data != null)
        {
            var statsY = 80;
            CreateStatLine(panel.transform, "击杀数", data.killCount.ToString(), new Vector2(0, statsY));
            CreateStatLine(panel.transform, "获得经验", data.expGained.ToString(), new Vector2(0, statsY - 45));
            CreateStatLine(panel.transform, "最大连击", data.maxCombo.ToString(), new Vector2(0, statsY - 90));
            CreateStatLine(panel.transform, "存活时间",
                $"{data.survivalTime / 60:D2}:{data.survivalTime % 60:D2}", new Vector2(0, statsY - 135));
        }

        // 按钮
        CreateInkButton(panel.transform, "BtnRestart", "再来一局", () =>
        {
            OnRestart?.Invoke();
            SceneTransitionManager.Instance?.LoadScene("BattleScene");
        }, new Vector2(-140, -160));

        CreateInkButton(panel.transform, "BtnMenu", "返回主菜单", () =>
        {
            OnReturnToMenu?.Invoke();
            SceneTransitionManager.Instance?.LoadScene("MenuScene");
        }, new Vector2(140, -160));
    }

    Texture2D MakeOverlayTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.black);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    void CreateStatLine(Transform parent, string label, string value, Vector2 pos)
    {
        var go = new GameObject($"Stat_{label}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().anchoredPosition = pos;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 40);

        var txt = CreateText(go.transform, "T", $"{label}: {value}", 28, TextAnchor.MiddleCenter);
        txt.color = new Color(0.15f, 0.15f, 0.15f);
        txt.rectTransform.anchoredPosition = Vector2.zero;
        txt.rectTransform.sizeDelta = new Vector2(400, 40);
    }

    #region 工具方法

    GameObject CreatePanel(Transform p, string n, int w, int h)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        go.AddComponent<InkPanel>();
        return go;
    }

    Text CreateText(Transform p, string n, string c, int s, TextAnchor a)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = c; t.fontSize = s; t.alignment = a; t.font = _font;
        return t;
    }

    void CreateInkButton(Transform p, string n, string t, Action cb, Vector2 pos)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(240, 60);
        var ib = go.AddComponent<InkButton>();
        ib.buttonText = t; ib.fontSize = 28;
        go.GetComponent<Button>().onClick.AddListener(() => cb());
    }

    #endregion
}

/// <summary> 战斗结算数据 </summary>
public class CombatResultData
{
    public int killCount;
    public int expGained;
    public int maxCombo;
    public int survivalTime;
    public int playerLevel;
    public int bossKills;
    public int elementalUpgradeCount;
    public int summonUpgradeCount;
    public int styleSwitchCount;
    public int[] obtainedUpgradeIds;
}