using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 升级选择界面：水墨风格，3-4选1升级选项。
/// 参考：Level.cs (VampireSurvivors clone) 的4槽位+运气机制
/// 修复：升级逻辑统一走 UpgradeManager，不再重复直接修改属性。
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [Tooltip("根面板")]
    public GameObject panel;
    [Tooltip("升级选项容器")]
    public Transform optionContainer;
    [Tooltip("选项预制体（可选，运行时创建）")]
    public GameObject optionPrefab;

    /// <summary>升级选择事件，参数为选择的ItemData</summary>
    public event System.Action<ItemData> OnUpgradeSelected;

    private List<ItemData> _currentOptions = new List<ItemData>();
    private List<GameObject> _optionObjects = new List<GameObject>();
    private CharacterStats _playerStats;
    private UpgradeManager _upgradeManager;
    private bool _isOpen;

    /// <summary>是否打开中</summary>
    public bool IsOpen => _isOpen;

    private void Awake()
    {
        _upgradeManager = FindObjectOfType<UpgradeManager>();
        CreateUI();
    }

    public void Initialize(CharacterStats playerStats)
    {
        _playerStats = playerStats;
        if (_upgradeManager == null)
            _upgradeManager = FindObjectOfType<UpgradeManager>();
    }

    /// <summary>显示升级选择界面</summary>
    public void Show(List<ItemData> options)
    {
        if (_isOpen) return;

        _isOpen = true;
        _currentOptions = options;

        if (panel != null)
            panel.SetActive(true);

        Time.timeScale = 0f;
        RefreshOptions();
    }

    /// <summary>隐藏升级界面</summary>
    public void Hide()
    {
        _isOpen = false;
        if (panel != null)
            panel.SetActive(false);
        Time.timeScale = 1f;
        ClearOptions();
    }

    private void RefreshOptions()
    {
        ClearOptions();
        var inv = Inventory.Instance;

        for (int i = 0; i < _currentOptions.Count; i++)
        {
            var option = _currentOptions[i];
            var optionObj = CreateOptionButton(option, i, inv);
            _optionObjects.Add(optionObj);
        }
    }

    private void ClearOptions()
    {
        foreach (var obj in _optionObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _optionObjects.Clear();
    }

    /// <summary>创建水墨风格选项按钮</summary>
    private GameObject CreateOptionButton(ItemData item, int index, Inventory inv)
    {
        var go = new GameObject($"Option_{index}");
        go.transform.SetParent(optionContainer, false);

        // 水墨面板背景
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = PlaceholderSpriteFactory.CreateInkPanelSprite(280, 200);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = item.isWeapon ? new Color(0.92f, 0.88f, 0.80f, 0.95f) : ShuiMoPalette.RicePaper;

        var button = go.AddComponent<Button>();
        var capture = item; // 闭包捕获
        button.onClick.AddListener(() => OnOptionSelected(capture));

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280, 200);

        // 武器标记线（右侧竖线）
        if (item.isWeapon)
        {
            var markObj = new GameObject("WeaponMark");
            markObj.transform.SetParent(go.transform, false);
            var markImg = markObj.AddComponent<Image>();
            markImg.color = ShuiMoPalette.CinnabarRed;
            var markRect = markObj.GetComponent<RectTransform>();
            markRect.anchorMin = new Vector2(1, 0);
            markRect.anchorMax = new Vector2(1, 1);
            markRect.pivot = new Vector2(1, 0.5f);
            markRect.sizeDelta = new Vector2(5, 0);
            markRect.anchoredPosition = Vector2.zero;
        }

        // 稀有度角标
        if (item.rarity != ItemData.Rarity.Common)
        {
            var rareObj = new GameObject("Rarity");
            rareObj.transform.SetParent(go.transform, false);
            var rareText = rareObj.AddComponent<Text>();
            rareText.text = item.rarity == ItemData.Rarity.Epic ? "史诗" : "稀有";
            rareText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rareText.fontSize = 14;
            rareText.fontStyle = FontStyle.Bold;
            rareText.color = item.rarity == ItemData.Rarity.Epic ? ShuiMoPalette.InkPurple : ShuiMoPalette.FlowerBlue;
            rareText.alignment = TextAnchor.MiddleCenter;
            var rareRect = rareObj.GetComponent<RectTransform>();
            rareRect.anchorMin = new Vector2(1, 1);
            rareRect.anchorMax = new Vector2(1, 1);
            rareRect.pivot = new Vector2(1, 1);
            rareRect.anchoredPosition = new Vector2(-5, -5);
            rareRect.sizeDelta = new Vector2(50, 24);
        }

        // 物品名称
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(go.transform, false);
        var nameText = nameObj.AddComponent<Text>();
        nameText.text = item.itemName;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 28;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = item.GetColor();
        nameText.alignment = TextAnchor.MiddleCenter;
        var nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0, -30);
        nameRect.sizeDelta = new Vector2(260, 40);

        // 物品描述
        var descObj = new GameObject("Description");
        descObj.transform.SetParent(go.transform, false);
        var descText = descObj.AddComponent<Text>();
        descText.text = item.description;
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 18;
        descText.color = ShuiMoPalette.InkBlack;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Overflow;
        var descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.offsetMin = new Vector2(20, 80);
        descRect.offsetMax = new Vector2(-20, -20);

        // 一级属性加成明细
        string primaryStr = GetPrimaryBonusText(item);
        if (!string.IsNullOrEmpty(primaryStr))
        {
            var statObj = new GameObject("PrimaryStats");
            statObj.transform.SetParent(go.transform, false);
            var statText = statObj.AddComponent<Text>();
            statText.text = primaryStr;
            statText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statText.fontSize = 14;
            statText.color = ShuiMoPalette.InkBlack;
            statText.alignment = TextAnchor.MiddleCenter;
            var statRect = statObj.GetComponent<RectTransform>();
            statRect.anchorMin = new Vector2(0.5f, 0f);
            statRect.anchorMax = new Vector2(0.5f, 0f);
            statRect.pivot = new Vector2(0.5f, 0f);
            statRect.anchoredPosition = new Vector2(0, 62);
            statRect.sizeDelta = new Vector2(240, 24);
        }

        // 等级信息（已拥有时显示）
        int currentLevel = 0;
        if (item.isWeapon)
            currentLevel = inv.GetWeaponLevel(item.weaponBehaviourId);
        else
            currentLevel = GetItemLevel(inv, item.UniqueId);

        if (currentLevel > 0)
        {
            var lvlObj = new GameObject("Level");
            lvlObj.transform.SetParent(go.transform, false);
            var lvlText = lvlObj.AddComponent<Text>();
            lvlText.text = currentLevel >= item.maxLevel ? "MAX" : $"Lv.{currentLevel} → Lv.{currentLevel + 1}";
            lvlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lvlText.fontSize = 16;
            lvlText.fontStyle = FontStyle.Bold;
            lvlText.color = ShuiMoPalette.Vermillion;
            lvlText.alignment = TextAnchor.MiddleCenter;
            var lvlRect = lvlObj.GetComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0.5f, 0f);
            lvlRect.anchorMax = new Vector2(0.5f, 0f);
            lvlRect.pivot = new Vector2(0.5f, 0f);
            lvlRect.anchoredPosition = new Vector2(0, 45);
            lvlRect.sizeDelta = new Vector2(200, 30);
        }

        return go;
    }

    /// <summary>获取已有道具的等级</summary>
    private int GetItemLevel(Inventory inv, string itemId)
    {
        for (int i = 0; i < inv.Count; i++)
        {
            if (inv.Items[i] != null && inv.Items[i].id == itemId)
                return inv.Items[i].currentLevel;
        }
        return 0;
    }

    /// <summary>生成一级属性加成文本</summary>
    private string GetPrimaryBonusText(ItemData item)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (item.strengthBonus != 0) parts.Add($"力量+{item.strengthBonus}");
        if (item.innerForceBonus != 0) parts.Add($"内力+{item.innerForceBonus}");
        if (item.vitalityBonus != 0) parts.Add($"体力+{item.vitalityBonus}");
        if (item.spiritBonus != 0) parts.Add($"精神+{item.spiritBonus}");
        if (item.comprehensionBonus != 0) parts.Add($"悟性+{item.comprehensionBonus}");
        if (parts.Count == 0) return "";
        return string.Join("  ", parts);
    }

    /// <summary>选项被选择 — 统一走 UpgradeManager.ApplyUpgrade</summary>
    private void OnOptionSelected(ItemData item)
    {
        // 墨水扩散动画（选择反馈）
        StartCoroutine(InkSelectionEffect(item));

        // 统一走 UpgradeManager
        if (_upgradeManager != null)
            _upgradeManager.ApplyUpgrade(item);

        OnUpgradeSelected?.Invoke(item);
        Hide();
    }

    private System.Collections.IEnumerator InkSelectionEffect(ItemData item)
    {
        // 短暂的选择动画：所有其他选项淡出
        foreach (var obj in _optionObjects)
        {
            var img = obj.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, 0.3f);
            }
        }
        yield return new WaitForSecondsRealtime(0.15f);
    }

    private void CreateUI()
    {
        if (panel == null)
        {
            panel = new GameObject("LevelUpPanel");
            panel.SetActive(false);
            panel.transform.SetParent(transform, false);

            var bgImg = panel.AddComponent<Image>();
            bgImg.sprite = PlaceholderSpriteFactory.CreateInkPanelSprite(900, 400);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.96f, 0.94f, 0.89f, 0.95f);

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(100, 100);
            panelRect.offsetMax = new Vector2(-100, -100);

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "升级！选择强化";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 42;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = ShuiMoPalette.CinnabarRed;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -40);
            titleRect.sizeDelta = new Vector2(600, 60);

            var containerObj = new GameObject("OptionContainer");
            containerObj.transform.SetParent(panel.transform, false);
            var containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(30, 100);
            containerRect.offsetMax = new Vector2(-30, 30);

            var layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 30;
            layout.childAlignment = TextAnchor.MiddleCenter;

            optionContainer = containerObj.transform;
        }
    }
}