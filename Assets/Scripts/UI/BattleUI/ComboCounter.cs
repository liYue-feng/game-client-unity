using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 连击计数器：水墨风格，命中+1，3秒不命中归零。
/// 连击数越大文字越大，颜色随连击变深。
/// </summary>
public class ComboCounter : MonoBehaviour
{
    [Tooltip("连击文字")]
    public Text comboText;
    [Tooltip("连击超时时间（秒）")]
    public float comboTimeout = 3f;

    private int _comboCount;
    private float _comboTimer;

    private void Start()
    {
        CombatEvents.OnHitLanded += OnHit;
    }

    private void OnDestroy()
    {
        CombatEvents.OnHitLanded -= OnHit;
    }

    private void OnHit(Vector3 pos, int dmg)
    {
        _comboCount++;
        _comboTimer = comboTimeout;
        UpdateDisplay();
    }

    private void Update()
    {
        if (_comboCount <= 0) return;

        _comboTimer -= Time.deltaTime;
        if (_comboTimer <= 0)
        {
            _comboCount = 0;
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (comboText == null) return;

        if (_comboCount <= 0)
        {
            comboText.text = "";
            return;
        }

        comboText.text = $"{_comboCount} 连";

        // 连击数越大字体越大（基础28 + 连击*2，上限64）
        comboText.fontSize = Mathf.Min(64, 28 + _comboCount * 2);

        // 颜色：低连击墨黑，高连击朱砂红
        float t = Mathf.Clamp01((float)_comboCount / 20f);
        comboText.color = ShuiMoPalette.Interpolate(ShuiMoPalette.InkBlack, ShuiMoPalette.CinnabarRed, t);
    }
}
