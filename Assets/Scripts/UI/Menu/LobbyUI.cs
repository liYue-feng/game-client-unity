using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Lobby 界面 — 角色信息 + 准备 + 开始战斗
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("角色信息")]
    public string playerName = "侠客";
    public int playerLevel = 1;

    private Font _font;
    private Text _levelText;
    private Text _readyText;
    private bool _isReady;

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("LobbyCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 宣纸背景
        CreatePaperBackground(canvasGo.transform);

        // 顶部信息栏
        var topBar = CreatePanel(canvasGo.transform, "TopBar", 900, 120);
        topBar.transform.localPosition = new Vector3(0, 700, 0);

        var nameText = CreateText(topBar.transform, "Name", $"角色: {playerName}", 36, TextAnchor.MiddleLeft);
        nameText.rectTransform.anchoredPosition = new Vector2(-350, 0);
        nameText.rectTransform.sizeDelta = new Vector2(400, 60);

        _levelText = CreateText(topBar.transform, "Level", $"等级: {playerLevel}", 30, TextAnchor.MiddleLeft);
        _levelText.color = new Color(0.3f, 0.3f, 0.3f);
        _levelText.rectTransform.anchoredPosition = new Vector2(100, 0);
        _levelText.rectTransform.sizeDelta = new Vector2(200, 50);

        // 中央内容区
        var centerPanel = CreatePanel(canvasGo.transform, "Center", 700, 500);
        centerPanel.transform.localPosition = new Vector3(0, 50, 0);

        var infoTitle = CreateText(centerPanel.transform, "InfoTitle", "— 战前准备 —", 36, TextAnchor.MiddleCenter);
        infoTitle.color = new Color(0.4f, 0.35f, 0.3f);
        infoTitle.rectTransform.anchoredPosition = new Vector2(0, 180);
        infoTitle.rectTransform.sizeDelta = new Vector2(500, 60);

        // 已装备显示
        var equipText = CreateText(centerPanel.transform, "Equip", "当前装备: 长剑 / 布衣", 28, TextAnchor.MiddleCenter);
        equipText.color = new Color(0.35f, 0.3f, 0.25f);
        equipText.rectTransform.anchoredPosition = new Vector2(0, 100);
        equipText.rectTransform.sizeDelta = new Vector2(500, 50);

        // 升级列表
        var upgradesText = CreateText(centerPanel.transform, "Upgrades", "已获升级: 剑气 / 轻功", 28, TextAnchor.MiddleCenter);
        upgradesText.color = new Color(0.35f, 0.3f, 0.25f);
        upgradesText.rectTransform.anchoredPosition = new Vector2(0, 50);
        upgradesText.rectTransform.sizeDelta = new Vector2(500, 50);

        _readyText = CreateText(centerPanel.transform, "ReadyStatus", "未准备", 32, TextAnchor.MiddleCenter);
        _readyText.color = new Color(0.7f, 0.6f, 0.3f);
        _readyText.rectTransform.anchoredPosition = new Vector2(0, -30);
        _readyText.rectTransform.sizeDelta = new Vector2(300, 50);

        // 底部按钮
        CreateMenuButton(canvasGo.transform, "BtnReady", "准备", 40, OnReady, new Vector2(-150, -500));
        CreateMenuButton(canvasGo.transform, "BtnStart", "开始战斗", 40, OnStartBattle, new Vector2(150, -500));
        CreateSmallButton(canvasGo.transform, "BtnBack", "返回主菜单", () =>
        {
            SceneTransitionManager.Instance?.LoadScene("MenuScene");
        }, new Vector2(0, -620));
    }

    void OnReady()
    {
        _isReady = !_isReady;
        _readyText.text = _isReady ? "已准备 ✓" : "未准备";
        _readyText.color = _isReady
            ? new Color(0.15f, 0.55f, 0.25f)
            : new Color(0.7f, 0.6f, 0.3f);
    }

    void OnStartBattle()
    {
        Debug.Log("[Lobby] 开始战斗");
        SceneTransitionManager.Instance?.LoadScene("BattleScene");
    }

    public void UpdatePlayerInfo(string name, int level)
    {
        playerName = name;
        playerLevel = level;
    }

    #region UI工具

    void CreatePaperBackground(Transform parent)
    {
        var go = new GameObject("Background");
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<RawImage>();
        bg.texture = CreatePaperTex(1080, 1920);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
    }

    Texture2D CreatePaperTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var cs = new Color32[w * h];
        var paper = new Color32(245, 240, 232, 255);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                var g = (byte)(n * 25);
                cs[y * w + x] = new Color32((byte)(paper.r - g), (byte)(paper.g - g), (byte)(paper.b - g), 255);
            }
        tex.SetPixels32(cs);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    GameObject CreatePanel(Transform p, string n, int w, int h)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        go.AddComponent<InkPanel>(); return go;
    }

    Text CreateText(Transform p, string n, string c, int s, TextAnchor a)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = c; t.fontSize = s; t.alignment = a; t.font = _font;
        t.color = new Color(0.1f, 0.1f, 0.1f); return t;
    }

    void CreateMenuButton(Transform p, string n, string t, int fs, UnityEngine.Events.UnityAction cb, Vector2 pos)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(280, 70);
        var ib = go.AddComponent<InkButton>(); ib.buttonText = t; ib.fontSize = fs;
        go.GetComponent<Button>().onClick.AddListener(cb);
    }

    void CreateSmallButton(Transform p, string n, string btnText, UnityEngine.Events.UnityAction cb, Vector2 pos)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(200, 50);
        var ib = go.AddComponent<InkButton>(); ib.buttonText = btnText; ib.fontSize = 22;
        go.GetComponent<Button>().onClick.AddListener(cb);
    }

    #endregion
}