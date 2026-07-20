using UnityEngine;
using UnityEngine.UI;
using System;
using Game.Managers;

/// <summary>
/// 水墨风格排行榜面板 — 支持本地和服务器数据
/// </summary>
public class RankPanelUI : MonoBehaviour
{
    [Header("引用")]
    public RankManager rankManager;

    private Font _font;
    private GameObject _root;
    private Text _loadingText;
    private Texture2D _maskTexture;

    /// <summary> 显示排行榜（模态） </summary>
    public static RankPanelUI Show(RankManager manager = null)
    {
        var existing = FindObjectOfType<RankPanelUI>();
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject("RankPanelUI_Runtime");
        var ui = go.AddComponent<RankPanelUI>();
        ui.rankManager = manager;
        ui.BuildUI();
        return ui;
    }

    public void Close()
    {
        Destroy(gameObject);
    }

    void BuildUI()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Canvas
        var canvasGo = new GameObject("RankCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 遮罩
        var maskGo = new GameObject("Mask");
        maskGo.transform.SetParent(canvasGo.transform, false);
        var mask = maskGo.AddComponent<RawImage>();
        _maskTexture = MakeTex(4, 4);
        mask.texture = _maskTexture;
        mask.color = new Color(0, 0, 0, 0.5f);
        var mr = maskGo.GetComponent<RectTransform>();
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.sizeDelta = Vector2.zero;

        // 面板
        _root = CreatePanel(canvasGo.transform, "RankRoot", 700, 800);
        _root.transform.localPosition = Vector3.zero;

        // 标题
        var title = CreateText(_root.transform, "T", "排行榜", 52, TextAnchor.MiddleCenter);
        title.color = new Color(0.08f, 0.08f, 0.08f);
        title.rectTransform.anchoredPosition = new Vector2(0, 320);
        title.rectTransform.sizeDelta = new Vector2(400, 70);

        // 玩家排名
        var rankText = CreateText(_root.transform, "MyRank", "我的排名: —", 28, TextAnchor.MiddleCenter);
        rankText.color = new Color(0.65f, 0.15f, 0.15f);
        rankText.rectTransform.anchoredPosition = new Vector2(0, 250);
        rankText.rectTransform.sizeDelta = new Vector2(300, 40);

        // 加载状态
        _loadingText = CreateText(_root.transform, "EmptyState", "暂无排行数据", 24, TextAnchor.MiddleCenter);
        _loadingText.color = new Color(0.5f, 0.45f, 0.4f);
        _loadingText.rectTransform.anchoredPosition = new Vector2(0, 0);
        _loadingText.rectTransform.sizeDelta = new Vector2(200, 40);

        // 关闭按钮
        CreateBtn(_root.transform, "BtnCloseRank", "关闭", Close, new Vector2(0, -330));

        // 请求排行榜数据
        if (rankManager != null)
        {
            rankManager.OnRankDataReceived += OnRankData;
            rankManager.FetchRankList();
        }
    }

    void OnRankData(RankEntry[] entries)
    {
        if (_loadingText != null)
            _loadingText.text = "";

        // 动态生成排名列表
        float startY = 200;
        float spacing = 55;
        int count = Mathf.Min(entries.Length, 10);

        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            var rankStr = (i + 1).ToString();
            Color rankColor;

            if (i == 0) rankColor = new Color(0.8f, 0.55f, 0.1f);      // 金
            else if (i == 1) rankColor = new Color(0.6f, 0.6f, 0.6f);   // 银
            else if (i == 2) rankColor = new Color(0.6f, 0.4f, 0.2f);   // 铜
            else rankColor = new Color(0.15f, 0.15f, 0.15f);

            var lineText = CreateText(_root.transform, $"Rank_{i}",
                $"{rankStr}.  {entry.playerName}    Lv.{entry.level}    {entry.score}分",
                24, TextAnchor.MiddleLeft);
            lineText.color = rankColor;
            lineText.rectTransform.anchoredPosition = new Vector2(-150, startY - i * spacing);
            lineText.rectTransform.sizeDelta = new Vector2(550, 40);
        }
    }

    Texture2D MakeTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            tex.SetPixel(x, y, Color.black);
        tex.filterMode = FilterMode.Bilinear; tex.Apply(); return tex;
    }

    #region 工具

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
        return t;
    }

    void CreateBtn(Transform p, string n, string t, Action cb, Vector2 pos)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(200, 50);
        var ib = go.AddComponent<InkButton>();
        ib.buttonText = t; ib.fontSize = 28;
        go.GetComponent<Button>().onClick.AddListener(() => cb());
    }

    #endregion

    void OnDestroy()
    {
        if (rankManager != null)
            rankManager.OnRankDataReceived -= OnRankData;

        if (_maskTexture != null)
        {
            Destroy(_maskTexture);
            _maskTexture = null;
        }
    }
}

