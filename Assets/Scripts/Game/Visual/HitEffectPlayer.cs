using UnityEngine;
using System.Collections;

/// <summary>
/// 受击闪白效果：角色被命中时短暂白色闪烁。
/// 通过临时修改 SpriteRenderer.color 实现，不依赖 shader。
/// 为什么不用 Material：占位阶段保持简单，正式阶段可换成 shader 方案。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitEffectPlayer : MonoBehaviour
{
    [Tooltip("闪白持续时间（秒）")]
    public float flashDuration = 0.05f;

    private SpriteRenderer _sprite;
    private Color _originalColor;
    private Coroutine _flashCoroutine;

    private void Awake()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _originalColor = _sprite.color;
    }

    /// <summary>播放受击闪白效果</summary>
    public void PlayHitEffect()
    {
        if (_flashCoroutine == null)
        {
            _originalColor = _sprite.color;
        }
        else
        {
            StopCoroutine(_flashCoroutine);
        }

        _flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    public void Clear()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        if (_sprite != null)
        {
            _sprite.color = _originalColor;
        }
    }

    private IEnumerator FlashCoroutine()
    {
        _sprite.color = Color.white;
        yield return new WaitForSeconds(flashDuration);
        _sprite.color = _originalColor;
        _flashCoroutine = null;
    }

    private void OnDisable()
    {
        Clear();
    }
}
