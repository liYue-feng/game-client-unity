using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 存档管理面板 — 显示存档槽位，支持加载/删除
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("引用")]
    public ArchiveManager archiveManager;

    private Font _font;
    private GameObject _root;

    public static SaveSlotUI Show(ArchiveManager manager = null)
    {
        var go = new GameObject("SaveSlotUI_Runtime");
        var ui = go.AddComponent<SaveSlotUI>();
        ui.archiveManager = manager;
        ui.BuildUI();
        return ui;
    }

    public void Close() { Destroy(gameObject); }

    void BuildUI()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("SaveCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        var maskGo = new GameObject("Mask");
        maskGo.transform.SetParent(canvasGo.transform, false);
        var mask = maskGo.AddComponent<RawImage>();
        mask.texture = MakeTex(4, 4);
        mask.color = new Color(0, 0, 0, 0.5f);
        var mr = maskGo.GetComponent<RectTransform>();
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.sizeDelta = Vector2.zero;

        _root = CreatePanel(canvasGo.transform, "SaveRoot", 600, 700);
        _root.transform.localPosition = Vector3.zero;

        var title = CreateText(_root.transform, "T", "存档管理", 48, TextAnchor.MiddleCenter);
        title.color = new Color(0.08f, 0.08f, 0.08f);
        title.rectTransform.anchoredPosition = new Vector2(0, 270);
        title.rectTransform.sizeDelta = new Vector2(400, 60);

        // 3个存档槽
        for (int i = 0; i < 3; i++)
        {
            var slotIdx = i;
            var y = 160 - i * 120;
            CreateSaveSlot(slotIdx, y);
        }

        CreateBtn(_root.transform, "Close", "关闭", Close, new Vector2(0, -280));
    }

    void CreateSaveSlot(int index, float y)
    {
        // 槽位面板
        var slotGo = new GameObject($"Slot_{index}");
        slotGo.transform.SetParent(_root.transform, false);
        var slotRt = slotGo.AddComponent<RectTransform>();
        slotRt.anchoredPosition = new Vector2(0, y);
        slotRt.sizeDelta = new Vector2(480, 90);
        slotGo.AddComponent<InkPanel>();

        // 槽位名
        var nameText = CreateText(slotGo.transform, "Name", $"存档槽 {index + 1}", 28, TextAnchor.MiddleLeft);
        nameText.rectTransform.anchoredPosition = new Vector2(-160, 15);
        nameText.rectTransform.sizeDelta = new Vector2(200, 40);

        // 存档信息
        var infoText = CreateText(slotGo.transform, "Info", "空", 22, TextAnchor.MiddleLeft);
        infoText.rectTransform.anchoredPosition = new Vector2(-160, -20);
        infoText.rectTransform.sizeDelta = new Vector2(200, 30);
        infoText.color = new Color(0.4f, 0.4f, 0.4f);

        // 加载按钮
        var loadGo = new GameObject("Load");
        loadGo.transform.SetParent(slotGo.transform, false);
        var loadRt = loadGo.AddComponent<RectTransform>();
        loadRt.anchoredPosition = new Vector2(140, 0);
        loadRt.sizeDelta = new Vector2(90, 50);
        var loadIb = loadGo.AddComponent<InkButton>();
        loadIb.buttonText = "加载"; loadIb.fontSize = 22;
        loadGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            Debug.Log($"[SaveSlot] 加载存档槽 {index + 1}");
            archiveManager?.LoadArchive(index);
        });

        // 删除按钮
        var delGo = new GameObject("Delete");
        delGo.transform.SetParent(slotGo.transform, false);
        var delRt = delGo.AddComponent<RectTransform>();
        delRt.anchoredPosition = new Vector2(230, 0);
        delRt.sizeDelta = new Vector2(90, 50);
        var delIb = delGo.AddComponent<InkButton>();
        delIb.buttonText = "删除"; delIb.fontSize = 22;
        delIb.inkColor = new Color(0.75f, 0.2f, 0.2f);
        delGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            Debug.Log($"[SaveSlot] 删除存档槽 {index + 1}");
            archiveManager?.DeleteArchive(index);
        });
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
}