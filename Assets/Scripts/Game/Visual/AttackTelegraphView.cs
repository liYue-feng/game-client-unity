using Game.Gameplay;
using UnityEngine;

/// <summary>
/// Renders a frozen enemy attack plan without participating in physics.
/// </summary>
public sealed class AttackTelegraphView : MonoBehaviour
{
    private static readonly Color ParryableColor = new Color(0.85f, 0.7f, 0.1f, 0.25f);
    private static readonly Color UnparryableColor = new Color(0.75f, 0.15f, 0.15f, 0.25f);

    private LineRenderer _line;
    private Material _material;
    private EnemyAttackPlan _plan;

    public bool IsVisible => _line != null && _line.enabled;
    public EnemyTelegraphShape CurrentShape => _plan.Shape;
    public Vector2 RenderedLocalMin { get; private set; }
    public Vector2 RenderedLocalMax { get; private set; }

    private void Awake()
    {
        EnsureLine();
        Hide();
    }

    public void Show(EnemyAttackPlan plan)
    {
        EnsureLine();
        _plan = plan;
        _line.enabled = true;
        _line.startColor = _line.endColor = plan.IsParryable
            ? ParryableColor
            : UnparryableColor;

        if (plan.Shape == EnemyTelegraphShape.Box)
        {
            BuildBox(plan);
        }
        else
        {
            BuildCircle(plan, 32);
        }
    }

    public void SetProgress(float progress)
    {
        if (_line == null)
        {
            return;
        }

        var color = _plan.IsParryable ? ParryableColor : UnparryableColor;
        color.a = Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01(progress));
        _line.startColor = _line.endColor = color;
    }

    public void Hide()
    {
        if (_line != null)
        {
            _line.enabled = false;
        }
    }

    private void EnsureLine()
    {
        if (_line != null)
        {
            return;
        }

        var lineObject = new GameObject("[AttackTelegraph]");
        lineObject.transform.SetParent(transform, false);
        _line = lineObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = false;
        _line.loop = false;
        _line.widthMultiplier = 0.045f;
        _line.numCapVertices = 2;
        _line.numCornerVertices = 2;
        _line.sortingOrder = 20;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored");
        if (shader != null)
        {
            _material = new Material(shader)
            {
                name = "AttackTelegraphView.Material"
            };
            _line.sharedMaterial = _material;
        }
    }

    private void BuildBox(EnemyAttackPlan plan)
    {
        var half = plan.Size * 0.5f;
        var forward = plan.AimDirection.sqrMagnitude > 0f
            ? plan.AimDirection.normalized
            : Vector2.right;
        var perpendicular = new Vector2(-forward.y, forward.x);
        var worldCenter = (Vector2)transform.TransformPoint(plan.LocalOffset);
        var worldCorners = new[]
        {
            worldCenter - forward * half.x - perpendicular * half.y,
            worldCenter - forward * half.x + perpendicular * half.y,
            worldCenter + forward * half.x + perpendicular * half.y,
            worldCenter + forward * half.x - perpendicular * half.y
        };
        var localCorners = new Vector2[worldCorners.Length];
        for (var index = 0; index < worldCorners.Length; index++)
        {
            localCorners[index] = transform.InverseTransformPoint(worldCorners[index]);
        }

        _line.positionCount = 5;
        for (var index = 0; index < localCorners.Length; index++)
        {
            _line.SetPosition(index, new Vector3(localCorners[index].x, localCorners[index].y, 0f));
        }
        _line.SetPosition(4, new Vector3(localCorners[0].x, localCorners[0].y, 0f));
        UpdateRenderedBounds(localCorners);
    }

    private void BuildCircle(EnemyAttackPlan plan, int segments)
    {
        var worldCenter = (Vector2)transform.TransformPoint(plan.LocalOffset);
        var localPoints = new Vector2[segments + 1];
        _line.positionCount = segments + 1;
        for (var index = 0; index <= segments; index++)
        {
            var angle = Mathf.PI * 2f * index / segments;
            var worldPoint = worldCenter + new Vector2(
                Mathf.Cos(angle) * plan.Radius,
                Mathf.Sin(angle) * plan.Radius);
            localPoints[index] = transform.InverseTransformPoint(worldPoint);
            _line.SetPosition(index, new Vector3(localPoints[index].x, localPoints[index].y, 0f));
        }
        UpdateRenderedBounds(localPoints);
    }

    private void UpdateRenderedBounds(Vector2[] localPoints)
    {
        RenderedLocalMin = localPoints[0];
        RenderedLocalMax = localPoints[0];
        for (var index = 1; index < localPoints.Length; index++)
        {
            RenderedLocalMin = Vector2.Min(RenderedLocalMin, localPoints[index]);
            RenderedLocalMax = Vector2.Max(RenderedLocalMax, localPoints[index]);
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        Hide();
        if (_material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_material);
        }
        else
        {
            DestroyImmediate(_material);
        }

        _material = null;
    }
}
