using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 水墨风格背包UI：战斗中按Tab键查看已收集的被动道具。
/// 宣纸背景 + 墨色边框 + 朱砂红稀有标记。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("布局")]
    public int itemsPerRow = 4;
    public float slotSize = 80f;
    public float spacing = 12f;
    public Vector2 offset = new Vector2(40, -60); // 从左上角偏移

    [Header("颜色")]
    public Color panelBg = new Color(0.96f, 0.94f, 0.89f, 0.92f); // 宣纸半透
    public Color slotBg = new Color(0.92f, 0.90f, 0.85f, 0.7f);    // 浅宣纸
    public Color inkText = new Color(0.12f, 0.12f, 0.12f);          // 墨黑
    public Color vermillion = new Color(0.75f, 0.15f, 0.15f);       // 朱砂红（稀有）
    public Color cyanInk = new Color(0.2f, 0.45f, 0.55f);           // 花青
    public Color goldInk = new Color(0.65f, 0.5f, 0.15f);           // 藤黄

    private Canvas _canvas;
    private GameObject _panel;
    private GameObject[] _slotGOs = new GameObject[Inventory.MaxSlots];
    private Text[] _itemTexts = new Text[Inventory.MaxSlots];
    private bool _isVisible;

    void Start()
    {
        BuildUI();
        _panel.SetActive(false);
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("InventoryCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRT = _panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 1);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = offset;
        panelRT.sizeDelta = new Vector2(
            itemsPerRow * (slotSize + spacing) + spacing * 2,
            Mathf.Ceil(Inventory.MaxSlots / (float)itemsPerRow) * (slotSize + spacing) + spacing * 2
        );

        var panelImg = _panel.AddComponent<RawImage>();
        panelImg.texture = CreatePanelTex((int)panelRT.sizeDelta.x, (int)panelRT.sizeDelta.y);
        panelImg.color = panelBg;

        // 标题
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_panel.transform, false);
        var titleRT = titleGo.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1);
        titleRT.anchoredPosition = new Vector2(0, 8);
        titleRT.sizeDelta = new Vector2(0, 28);
        var titleText = titleGo.AddComponent<Text>();
        titleText.text = "—— 行 囊 ——";
        titleText.fontSize = 18;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = inkText;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 槽位
        int rows = Mathf.CeilToInt(Inventory.MaxSlots / (float)itemsPerRow);
        for (int i = 0; i < Inventory.MaxSlots; i++)
        {
            int row = i / itemsPerRow;
            int col = i % itemsPerRow;
            float x = spacing + col * (slotSize + spacing);
            float y = -(spacing + 30 + row * (slotSize + spacing));

            var slotGo = new GameObject($"Slot_{i}");
            slotGo.transform.SetParent(_panel.transform, false);
            var slotRT = slotGo.AddComponent<RectTransform>();
            slotRT.anchorMin = new Vector2(0, 1);
            slotRT.anchorMax = new Vector2(0, 1);
            slotRT.pivot = new Vector2(0, 1);
            slotRT.anchoredPosition = new Vector2(x, y);
            slotRT.sizeDelta = new Vector2(slotSize, slotSize);

            var slotImg = slotGo.AddComponent<RawImage>();
            slotImg.texture = CreateSlotTex((int)slotSize, (int)slotSize);
            slotImg.color = slotBg;

            // 道具名称
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(slotGo.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta = new Vector2(slotSize - 8, slotSize - 8);
            var txt = textGo.AddComponent<Text>();
            txt.fontSize = 11;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = inkText;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = "";

            _slotGOs[i] = slotGo;
            _itemTexts[i] = txt;
        }

        Inventory.Instance.OnItemChanged += OnInventoryChanged;
    }

    void Toggle()
    {
        if (_isVisible) Hide();
        else Show();
    }

    public void Show()
    {
        _isVisible = true;
        _panel.SetActive(true);
        RefreshAllSlots();
        AudioManager.Instance.PlaySFX("ui_click");
    }

    public void Hide()
    {
        _isVisible = false;
        _panel.SetActive(false);
        AudioManager.Instance.PlaySFX("ui_cancel");
    }

    void OnInventoryChanged(int slot, PassiveItem item)
    {
        if (_isVisible) RefreshSlot(slot);
    }

    void RefreshAllSlots()
    {
        for (int i = 0; i < Inventory.MaxSlots; i++)
            RefreshSlot(i);
    }

    void RefreshSlot(int index)
    {
        var item = Inventory.Instance.Items[index];
        if (item != null)
        {
            _itemTexts[index].text = $"{item.displayName}\nLv.{item.currentLevel}";
            // 按品类着色
            switch (item.category)
            {
                case "attack":
                    _itemTexts[index].color = vermillion;
                    break;
                case "defense":
                    _itemTexts[index].color = cyanInk;
                    break;
                case "speed":
                case "utility":
                    _itemTexts[index].color = goldInk;
                    break;
                default:
                    _itemTexts[index].color = inkText;
                    break;
            }
            _slotGOs[index].SetActive(true);
        }
        else
        {
            _itemTexts[index].text = "——";
            _itemTexts[index].color = new Color(0.7f, 0.68f, 0.62f);
            _slotGOs[index].SetActive(true);
        }
    }

    Texture2D CreatePanelTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new Color32(245, 240, 232, 235);
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    Texture2D CreateSlotTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var bg = new Color32(235, 230, 217, 200);
        var border = new Color32(100, 90, 70, 180);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = y * w + x;
                bool isBorder = x < 2 || x >= w - 2 || y < 2 || y >= h - 2;
                if (isBorder)
                {
                    var noise = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                    colors[idx] = noise > 0.35f ? border : bg;
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

    void OnDestroy()
    {
        if (Inventory.Instance != null)
            Inventory.Instance.OnItemChanged -= OnInventoryChanged;
    }
}
