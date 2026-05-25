using UnityEngine;
using System.Collections;

/// <summary>
/// 浮动伤害数字：从目标位置弹出，上漂+渐隐。
/// 水墨风格：墨色数字，毛笔字感，朱砂红暴击。
/// </summary>
public class DamageNumber : MonoBehaviour
{
    private TextMesh _textMesh;
    private float _lifetime = 0.8f;
    private float _elapsed;
    private float _riseSpeed = 1.5f;
    private float _driftX;

    private static readonly Color NormalColor = new Color(0.1f, 0.1f, 0.1f);    // 墨黑
    private static readonly Color CritColor = new Color(0.75f, 0.15f, 0.15f);   // 朱砂红
    private static readonly Color HealColor = new Color(0.2f, 0.55f, 0.3f);     // 花青
    private static readonly Color ParryColor = new Color(0.85f, 0.7f, 0.1f);    // 藤黄

    public void Init(string text, Vector2 worldPos, DamageType type)
    {
        if (_textMesh == null)
        {
            var go = new GameObject("TextMesh");
            go.transform.SetParent(transform, false);
            _textMesh = go.AddComponent<TextMesh>();
            _textMesh.fontSize = 36;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _textMesh.characterSize = 0.1f;
        }

        _textMesh.text = text;
        _textMesh.color = GetColor(type);
        transform.position = worldPos + new Vector2(Random.Range(-0.3f, 0.3f), 0.5f);
        _elapsed = 0f;
        _driftX = Random.Range(-0.4f, 0.4f);

        // 暴击数字更大
        if (type == DamageType.Crit || type == DamageType.Parry)
        {
            _textMesh.fontSize = 48;
            _lifetime = 1.0f;
            _riseSpeed = 2.0f;
        }
        else
        {
            _textMesh.fontSize = 36;
            _lifetime = 0.8f;
            _riseSpeed = 1.5f;
        }

        gameObject.SetActive(true);
        StartCoroutine(FloatRoutine());
    }

    private IEnumerator FloatRoutine()
    {
        while (_elapsed < _lifetime)
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _lifetime;

            // 上升 + 水平漂移
            Vector3 pos = transform.position;
            pos.y += _riseSpeed * Time.deltaTime;
            pos.x += _driftX * Time.deltaTime;
            transform.position = pos;

            // 渐隐
            Color c = _textMesh.color;
            c.a = 1f - t * t; // 二次曲线渐隐
            _textMesh.color = c;

            yield return null;
        }

        DamageNumberPool.Instance.Return(this);
    }

    private Color GetColor(DamageType type) => type switch
    {
        DamageType.Normal => NormalColor,
        DamageType.Crit => CritColor,
        DamageType.Heal => HealColor,
        DamageType.Parry => ParryColor,
        _ => NormalColor
    };
}

public enum DamageType
{
    Normal,
    Crit,
    Heal,
    Parry
}