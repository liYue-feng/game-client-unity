using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 水墨风格天赋树UI：显示四分支天赋树，点击升级。
/// 在主菜单或Lobby中打开。
/// </summary>
public class TalentTreeUI : MonoBehaviour
{
    [Header("布局")]
    public float nodeWidth = 140f;
    public float nodeHeight = 100f;
    public float hSpacing = 40f;
    public float vSpacing = 30f;

    /// <summary>关闭事件</summary>
    public event System.Action OnClose;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _pointsText;
    private List<GameObject> _nodeObjects = new List<GameObject>();

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("TalentCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 全屏遮罩
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayRT = overlay.AddComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        var overlayImg = overlay.AddComponent<RawImage>();
        overlayImg.texture = CreateBgTex(4, 4);
        overlayImg.color = new Color(0.96f, 0.94f, 0.89f, 0.97f); // 宣纸半透明

        // 标题
        var title = CreateText(canvasGo.transform, "天赋树", 42, TextAnchor.UpperCenter,
            ShuiMoPalette.Vermillion, FontStyle.Bold);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0.5f, 1f);
        titleRT.anchorMax = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -50);
        titleRT.sizeDelta = new Vector2(300, 60);

        // 天赋点显示
        _pointsText = CreateText(canvasGo.transform, "", 24, TextAnchor.UpperRight,
            ShuiMoPalette.InkBlack, FontStyle.Normal);
        var ptRT = _pointsText.GetComponent<RectTransform>();
        ptRT.anchorMin = new Vector2(1f, 1f);
        ptRT.anchorMax = new Vector2(1f, 1f);
        ptRT.anchoredPosition = new Vector2(-40, -50);
        ptRT.sizeDelta = new Vector2(300, 40);

        // 关闭按钮
        var closeBtn = CreateButton(canvasGo.transform, "关闭", new Vector2(120, 50));
        var closeRT = closeBtn.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(0f, 1f);
        closeRT.anchorMax = new Vector2(0f, 1f);
        closeRT.anchoredPosition = new Vector2(60, -60);
        closeBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            OnClose?.Invoke();
            if (gameObject != null) Destroy(gameObject);
        });

        // 重置按钮
        var resetBtn = CreateButton(canvasGo.transform, "重置天赋", new Vector2(140, 50));
        var resetRT = resetBtn.GetComponent<RectTransform>();
        resetRT.anchorMin = new Vector2(0f, 1f);
        resetRT.anchorMax = new Vector2(0f, 1f);
        resetRT.anchoredPosition = new Vector2(200, -60);
        resetBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            TalentManager.Instance.ResetAll();
            RefreshNodes();
        });

        // 节点容器
        _panel = new GameObject("NodeContainer");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(900, 600);

        BuildTalentNodes();
        RefreshNodes();

        TalentManager.Instance.OnTalentChanged += RefreshNodes;
    }

    void BuildTalentNodes()
    {
        var branches = new Dictionary<string, (string label, Vector2 pos, List<TalentNode> nodes)>
        {
            ["sword"] = ("剑 · 道", new Vector2(-400, 120), new List<TalentNode>()),
            ["shield"] = ("盾 · 守", new Vector2(-400, -200), new List<TalentNode>()),
            ["speed"] = ("风 · 行", new Vector2(200, 120), new List<TalentNode>()),
            ["wisdom"] = ("智 · 悟", new Vector2(200, -200), new List<TalentNode>())
        };

        // 分类天赋
        foreach (var talent in TalentManager.Instance.AllTalents)
        {
            if (branches.ContainsKey(talent.branch))
                branches[talent.branch].nodes.Add(talent);
        }

        foreach (var kv in branches)
        {
            var branch = kv.Key;
            var (label, startPos, nodes) = kv.Value;

            // 分支标题
            var title = CreateText(_panel.transform, label, 28, TextAnchor.MiddleLeft,
                GetBranchColor(branch), FontStyle.Bold);
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchoredPosition = startPos + new Vector2(-50, 40);
            titleRT.sizeDelta = new Vector2(200, 40);

            // 分支节点（从上往下排列）
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var pos = startPos + new Vector2(0, -i * (nodeHeight + vSpacing));

                var nodeGo = CreateTalentNodeUI(node, pos);
                nodeGo.name = $"TalentNode_{node.id}";
                _nodeObjects.Add(nodeGo);
            }
        }
    }

    GameObject CreateTalentNodeUI(TalentNode node, Vector2 position)
    {
        var go = new GameObject($"Node_{node.id}");
        go.transform.SetParent(_panel.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(nodeWidth, nodeHeight);

        // 背景
        var bg = go.AddComponent<RawImage>();
        bg.texture = CreateNodeTex((int)nodeWidth, (int)nodeHeight);
        bg.color = ShuiMoPalette.RicePaper;

        // 按钮
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (TalentManager.Instance.TryUpgradeTalent(node.id))
            {
                AudioManager.Instance.PlaySFX("ui_confirm");
            }
            else
            {
                AudioManager.Instance.PlaySFX("ui_cancel");
            }
        });

        // 名称
        var nameTxt = CreateText(go.transform, node.displayName, 22, TextAnchor.UpperCenter,
            ShuiMoPalette.InkBlack, FontStyle.Bold);
        var nameRT = nameTxt.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 1f);
        nameRT.anchorMax = new Vector2(0.5f, 1f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0, -12);
        nameRT.sizeDelta = new Vector2(nodeWidth - 10, 30);

        // 描述
        var descTxt = CreateText(go.transform, node.description, 14, TextAnchor.MiddleCenter,
            ShuiMoPalette.InkMedium, FontStyle.Normal);
        var descRT = descTxt.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0, 0);
        descRT.anchorMax = new Vector2(1, 0.5f);
        descRT.offsetMin = new Vector2(5, 5);
        descRT.offsetMax = new Vector2(-5, 5);

        return go;
    }

    void RefreshNodes()
    {
        _pointsText.text = $"天赋点: {TalentManager.Instance.AvailablePoints}";

        foreach (var go in _nodeObjects)
        {
            var nodeId = go.name.Replace("TalentNode_", "");
            var node = TalentManager.Instance.AllTalents.Find(t => t.id == nodeId);
            if (node == null) continue;

            var bg = go.GetComponent<RawImage>();
            var btn = go.GetComponent<Button>();

            // 颜色状态
            if (node.IsMaxLevel)
            {
                bg.color = ShuiMoPalette.Gamboge; // 金色=满级
                btn.interactable = false;
            }
            else if (node.IsUnlocked)
            {
                bg.color = node.currentLevel >= 3
                    ? ShuiMoPalette.Vermillion
                    : new Color(0.85f, 0.7f, 0.5f); // 暖色=已解锁
                btn.interactable = TalentManager.Instance.AvailablePoints >= node.costPerLevel;
            }
            else if (TalentManager.Instance.CanUnlock(node.id))
            {
                bg.color = ShuiMoPalette.RicePaper;
                btn.interactable = TalentManager.Instance.AvailablePoints >= node.costPerLevel;
            }
            else
            {
                bg.color = ShuiMoPalette.AgedPaper;
                btn.interactable = false; // 前置未满足
            }
        }
    }

    Color GetBranchColor(string branch)
    {
        switch (branch)
        {
            case "sword": return ShuiMoPalette.Vermillion;
            case "shield": return ShuiMoPalette.Indigo;
            case "speed": return ShuiMoPalette.Gamboge;
            case "wisdom": return ShuiMoPalette.FlowerBlue;
            default: return ShuiMoPalette.InkBlack;
        }
    }

    Text CreateText(Transform parent, string text, int fontSize,
        TextAnchor align, Color color, FontStyle style)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = color;
        txt.fontStyle = style;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    GameObject CreateButton(Transform parent, string text, Vector2 size)
    {
        var go = new GameObject("Btn_" + text);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        var bg = go.AddComponent<RawImage>();
        bg.texture = CreateNodeTex((int)size.x, (int)size.y);
        bg.color = ShuiMoPalette.RicePaper;

        go.AddComponent<Button>();
        var outline = go.AddComponent<Outline>();
        outline.effectColor = ShuiMoPalette.InkBlack;
        outline.effectDistance = new Vector2(1, -1);

        var txtObj = new GameObject("Label");
        txtObj.transform.SetParent(go.transform, false);
        var txt = txtObj.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = ShuiMoPalette.InkBlack;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var txtRT = txtObj.GetComponent<RectTransform>();
        txtRT.sizeDelta = size;

        return go;
    }

    Texture2D CreateNodeTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var bg = new Color32(245, 240, 232, 255);
        var ink = new Color32(70, 60, 50, 200);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = y * w + x;
                bool isBorder = x < 3 || x >= w - 3 || y < 3 || y >= h - 3;
                if (isBorder)
                {
                    var noise = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    colors[idx] = noise > 0.3f ? ink : bg;
                }
                else
                {
                    colors[idx] = bg;
                }
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    Texture2D CreateBgTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color32(245, 240, 232, 248);
        tex.SetPixels32(colors);
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (TalentManager.Instance != null)
            TalentManager.Instance.OnTalentChanged -= RefreshNodes;
    }
}