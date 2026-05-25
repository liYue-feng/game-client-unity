using UnityEngine;
using System.Collections;

/// <summary>
/// 挥砍墨线特效：攻击时画出弧形墨迹线条。
/// 水墨画风格的另一个核心视觉——像毛笔挥过宣纸的笔触。
/// 使用 LineRenderer 实现弧线，0.2s 后淡出消失。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class InkSlashEffect : MonoBehaviour
{
    [Tooltip("弧线段数")]
    public int segments = 8;
    [Tooltip("弧线半径")]
    public float radius = 1.2f;
    [Tooltip("弧线角度范围（度）")]
    public float arcAngle = 90f;
    [Tooltip("淡出时间（秒）")]
    public float fadeDuration = 0.2f;
    [Tooltip("线条宽度")]
    public float lineWidth = 0.08f;

    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth * 0.3f; // 尾部收细
        _lineRenderer.positionCount = segments + 1;
        _lineRenderer.enabled = false;
    }

    /// <summary>
    /// 在指定位置和朝向播放挥砍墨线。
    /// </summary>
    /// <param name="position">角色位置</param>
    /// <param name="facingDir">朝向 1=右 -1=左</param>
    public void Play(Vector3 position, int facingDir)
    {
        StopAllCoroutines();
        StartCoroutine(SlashCoroutine(position, facingDir));
    }

    private IEnumerator SlashCoroutine(Vector3 position, int facingDir)
    {
        _lineRenderer.enabled = true;

        // 绘制弧线
        float startAngle = facingDir == 1 ? -45f : 135f;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = (startAngle + arcAngle * t * facingDir) * Mathf.Deg2Rad;
            Vector3 point = position + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
            _lineRenderer.SetPosition(i, point);
        }

        // 设置墨线颜色（使用水墨调色板）
        Color inkBase = ShuiMoPalette.InkBlack;
        _lineRenderer.startColor = inkBase;
        _lineRenderer.endColor = new Color(inkBase.r, inkBase.g, inkBase.b, 0.1f);

        // 淡出
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeDuration);
            _lineRenderer.startColor = new Color(inkBase.r, inkBase.g, inkBase.b, alpha);
            _lineRenderer.endColor = new Color(inkBase.r, inkBase.g, inkBase.b, alpha * 0.1f);
            yield return null;
        }

        _lineRenderer.enabled = false;
    }
}
