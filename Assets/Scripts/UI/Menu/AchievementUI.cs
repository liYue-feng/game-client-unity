using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 水墨风格成就UI：按分类展示所有成就，显示进度条和领取按钮。
/// 在主菜单中打开。
/// </summary>
public class AchievementUI : MonoBehaviour
{
    [Header("布局")]
    public float itemHeight = 80f;
    public float itemSpacing = 8f;
    public float panelWidth = 600f;

    public event System.Action OnClose;

    private Canvas _canvas;
    private GameObject _panel;
    private Text _summaryText;
    private string _currentCategory = "combat";
    private List<GameObject> _itemObjects = new List<GameObject>();

    void Start()
    {
        BuildUI();
        RefreshItems();
        AchievementManager.Instance.OnProgressUpdated += (a) => RefreshItems();
        AchievementManager.Instance.OnCompleted += (a) => RefreshItems();
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("AchievementCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 遮罩
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(canvasGo.transform, false);
        var ovRT = overlay.AddComponent<RectTransform>();
        ovRT.anchorMin = Vector2.zero;
        ovRT.anchorMax = Vector2.one;
        ovRT.sizeDelta = Vector2.zero;
        overlay.AddComponent<RawImage>().color = new Color(0.96f, 0.94f, 0.89f, 0.97f);

        // 标题
        var title = CreateText(canvasGo.transform, "成就录", 48, TextAnchor.UpperCenter,
            ShuiMoPalette.Vermillion, FontStyle.Bold);
        var tRT = title.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.5f, 1f); tRT.anchorMax = new Vector2(0.5f, 1f);
        tRT.anchoredPosition = new Vector2(0, -50); tRT.sizeDelta = new Vector2(300, 60);

        // 完成统计
        _summaryText = CreateText(canvasGo.transform, "", 22, TextAnchor.UpperRight,
            ShuiMoPalette.InkMedium, FontStyle.Normal);
        var sRT = _summaryText.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(1f, 1f); sRT.anchorMax = new Vector2(1f, 1f);
        sRT.anchoredPosition = new Vector2(-30, -55); sRT.sizeDelta = new Vector2(300, 40);

        // 关闭按钮
        var closeBtn = CreateButton(canvasGo.transform, "关闭", new Vector2(100, 45));
        var cRT = closeBtn.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(0f, 1f);
        cRT.anchoredPosition = new Vector2(50, -60);
        closeBtn.GetComponent<Button>().onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySFX("ui_cancel");
            OnClose?.Invoke();
            Destroy(gameObject);
        });

        // 分类标签栏
        var tabs = new[] {
            ("combat", "战斗"),
            ("collection", "收集"),
            ("survival", "生存"),
            ("mastery", "精通")
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            var (cat, label) = tabs[i];
            var tabBtn = CreateButton(canvasGo.transform, label, new Vector2(100, 40));
            var tabRT = tabBtn.GetComponent<RectTransform>();
            tabRT.anchorMin = new Vector2(0.5f, 1f); tabRT.anchorMax = new Vector2(0.5f, 1f);
            tabRT.anchoredPosition = new Vector2(-180 + i * 120, -110);
            tabBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                _currentCategory = cat;
                RefreshItems();
            });
        }

        // 列表容器
        _panel = new GameObject("ItemContainer");
        _panel.transform.SetParent(canvasGo.transform, false);
        var pRT = _panel.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.5f, 1f); pRT.anchorMax = new Vector2(0.5f, 1f);
        pRT.pivot = new Vector2(0.5f, 1f);
        pRT.anchoredPosition = new Vector2(0, -160);
        pRT.sizeDelta = new Vector2(panelWidth, 600);

        // ScrollRect
        var scroll = _panel.AddComponent<ScrollRect>();
        var vp = new GameObject("Viewport");
        vp.transform.SetParent(_panel.transform, false);
        var vpRT = vp.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.sizeDelta = Vector2.zero;
        vp.AddComponent<Mask>().showMaskGraphic = false;
        vp.AddComponent<Image>().color = new Color(0, 0, 0, 0);

        var content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        var ctRT = content.AddComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0, 1f); ctRT.anchorMax = new Vector2(1, 1f);
        ctRT.pivot = new Vector2(0.5f, 1f);
        ctRT.sizeDelta = new Vector2(0, 0);

        scroll.viewport = vpRT;
        scroll.content = ctRT;
        scroll.horizontal = false;
        scroll.vertical = true;
    }

    void RefreshItems()
    {
        var mgr = AchievementManager.Instance;
        _summaryText.text = $"已完成: {mgr.CompletedCount}/{mgr.AllAchievements.Count}";

        // 清除旧项
        foreach (var go in _itemObjects)
            if (go != null) Destroy(go);
        _itemObjects.Clear();

        var content = _panel.GetComponent<ScrollRect>().content;
        if (content == null) return;

        var items = mgr.GetByCategory(_currentCategory);
        float totalHeight = items.Count * (itemHeight + itemSpacing);
        content.sizeDelta = new Vector2(0, totalHeight);

        for (int i = 0; i < items.Count; i++)
        {
            var ach = items[i];
            var go = CreateAchievementItem(content, ach, i);
            _itemObjects.Add(go);
        }
    }

    GameObject CreateAchievementItem(Transform parent, Achievement ach, int index)
    {
        var go = new GameObject($"Ach_{ach.id}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1f); rt.anchorMax = new Vector2(1, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -index * (itemHeight + itemSpacing));
        rt.sizeDelta = new Vector2(0, itemHeight);

        // 背景
        var bg = go.AddComponent<RawImage>();
        if (ach.isCompleted)
            bg.color = new Color(0.92f, 0.88f, 0.78f, 0.8f);
        else
            bg.color = new Color(0.94f, 0.92f, 0.86f, 0.6f);

        // 边框（水墨感）
        var outline = go.AddComponent<Outline>();
        outline.effectColor = ach.isCompleted
            ? ShuiMoPalette.Gamboge
            : ShuiMoPalette.InkLight;
        outline.effectDistance = new Vector2(1, -1);

        // 名称
        var nameTxt = CreateText(go.transform, ach.displayName, 22, TextAnchor.UpperLeft,
            ach.isCompleted ? ShuiMoPalette.Vermillion : ShuiMoPalette.InkBlack, FontStyle.Bold);
        var nRT = nameTxt.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0, 1f); nRT.anchorMax = new Vector2(0, 1f);
        nRT.pivot = new Vector2(0, 1f);
        nRT.anchoredPosition = new Vector2(12, -10);
        nRT.sizeDelta = new Vector2(200, 30);

        // 描述
        var descTxt = CreateText(go.transform, ach.description, 15, TextAnchor.UpperLeft,
            ShuiMoPalette.InkMedium, FontStyle.Normal);
        var dRT = descTxt.GetComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0, 1f); dRT.anchorMax = new Vector2(0, 1f);
        dRT.pivot = new Vector2(0, 1f);
        dRT.anchoredPosition = new Vector2(12, -32);
        dRT.sizeDelta = new Vector2(300, 22);

        // 进度条背景
        var barBgGo = new GameObject("ProgressBg");
        barBgGo.transform.SetParent(go.transform, false);
        var bgRT = barBgGo.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0); bgRT.anchorMax = new Vector2(0, 0);
        bgRT.pivot = new Vector2(0, 0);
        bgRT.anchoredPosition = new Vector2(12, 10);
        bgRT.sizeDelta = new Vector2(panelWidth - 160, 14);
        barBgGo.AddComponent<Image>().color = ShuiMoPalette.AgedPaper;

        // 进度条
        var barGo = new GameObject("ProgressFill");
        barGo.transform.SetParent(barBgGo.transform, false);
        var fillRT = barGo.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0, 0); fillRT.anchorMax = new Vector2(0, 1);
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.sizeDelta = new Vector2(0, 0);
        var fillImg = barGo.AddComponent<Image>();
        fillImg.color = ach.isCompleted ? ShuiMoPalette.Gamboge : ShuiMoPalette.Vermillion;
        var fillWidth = (panelWidth - 160) * ach.Progress;
        fillRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fillWidth);

        // 进度文字
        var progTxt = CreateText(go.transform, ach.ProgressText, 14, TextAnchor.UpperRight,
            ShuiMoPalette.InkBlack, FontStyle.Normal);
        var pRT = progTxt.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0, 0); pRT.anchorMax = new Vector2(0, 0);
        pRT.pivot = new Vector2(0, 0);
        pRT.anchoredPosition = new Vector2(panelWidth - 140, 10);
        pRT.sizeDelta = new Vector2(120, 14);

        // 奖励文字
        var rewardTxt = CreateText(go.transform, $"奖励: {ach.rewardTalentPoints}天赋点", 13,
            TextAnchor.MiddleRight, ShuiMoPalette.InkLight, FontStyle.Normal);
        var rRT = rewardTxt.GetComponent<RectTransform>();
        rRT.anchorMin = new Vector2(1f, 1f); rRT.anchorMax = new Vector2(1f, 1f);
        rRT.pivot = new Vector2(1f, 1f);
        rRT.anchoredPosition = new Vector2(-12, -12);
        rRT.sizeDelta = new Vector2(130, 20);

        // 领取按钮（已完成但未领取）
        if (ach.isCompleted && !ach.isClaimed)
        {
            var claimBtn = CreateButton(go.transform, "领取", new Vector2(60, 28));
            var cbRT = claimBtn.GetComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(1f, 0.5f); cbRT.anchorMax = new Vector2(1f, 0.5f);
            cbRT.pivot = new Vector2(1f, 0.5f);
            cbRT.anchoredPosition = new Vector2(-12, 0);
            claimBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                AchievementManager.Instance.ClaimReward(ach.id);
                RefreshItems();
            });
        }

        return go;
    }

    #region 工具方法

    Text CreateText(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style)
    {
        var go = new GameObject("Text"); go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = text; txt.fontSize = size; txt.alignment = align;
        txt.color = color; txt.fontStyle = style;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    GameObject CreateButton(Transform parent, string text, Vector2 size)
    {
        var go = new GameObject("Btn_" + text); go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>(); rt.sizeDelta = size;
        var img = go.AddComponent<RawImage>();
        img.color = ShuiMoPalette.RicePaper;
        go.AddComponent<Button>();
        var ol = go.AddComponent<Outline>();
        ol.effectColor = ShuiMoPalette.InkBlack;
        ol.effectDistance = new Vector2(1, -1);

        var txtObj = new GameObject("Label"); txtObj.transform.SetParent(go.transform, false);
        var txt = txtObj.AddComponent<Text>();
        txt.text = text; txt.fontSize = 16; txt.alignment = TextAnchor.MiddleCenter;
        txt.color = ShuiMoPalette.InkBlack;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tRT = txtObj.GetComponent<RectTransform>(); tRT.sizeDelta = size;
        return go;
    }

    #endregion
}