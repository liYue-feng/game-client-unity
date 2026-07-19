using System.Collections;
using System.Collections.Generic;
using Game.Gameplay;
using UnityEngine;

public class Archer : EnemyBase
{
    [Header("Archer")]
    public GameObject projectilePrefab;
    public float shootCooldown = 2f;
    public float preferredDistance = 5f;

    private float _shootCooldownTimer;
    private readonly HashSet<Projectile> _ownedProjectiles = new HashSet<Projectile>();

    protected override void Awake()
    {
        hp = 20;
        maxHp = 20;
        moveSpeed = 1.5f;
        damage = 8;
        attackRange = 6f;
        chaseRange = 10f;
        telegraphDuration = 0.6f;
        attackDuration = 0.2f;
        isCurrentAttackParryable = true;
        base.Awake();
    }

    protected override void Update()
    {
        _shootCooldownTimer -= Time.deltaTime;
        base.Update();
    }

    protected override void ResetSubclassState()
    {
        _shootCooldownTimer = 0f;
    }

    protected override void UpdateChase()
    {
        if (_player == null)
        {
            return;
        }

        FacePlayer();
        var distance = _distanceToPlayer;
        if (distance < preferredDistance - 1f)
        {
            var direction = _player.position.x > transform.position.x ? -1f : 1f;
            _rb.velocity = new Vector2(direction * moveSpeed, _rb.velocity.y);
        }
        else if (distance <= attackRange && _shootCooldownTimer <= 0f)
        {
            TryStartPreparedAttack();
        }
        else if (distance > attackRange)
        {
            var direction = _player.position.x > transform.position.x ? 1f : -1f;
            _rb.velocity = new Vector2(direction * moveSpeed, _rb.velocity.y);
        }
        else
        {
            _rb.velocity = Vector2.zero;
        }
    }

    protected override EnemyAttackPlan PrepareAttackPlan()
    {
        var aimDirection = _player != null
            ? ((Vector2)(_player.position - transform.position)).normalized
            : new Vector2(_facingDirection, 0f);
        if (aimDirection == Vector2.zero)
        {
            aimDirection = new Vector2(_facingDirection, 0f);
        }

        var facing = aimDirection.x < 0f ? -1 : 1;
        var localOffset = (Vector2)transform.InverseTransformVector(aimDirection * 1.5f);
        return EnemyAttackPlan.Box(
            "archer_shot",
            telegraphDuration,
            attackDuration,
            0.15f,
            true,
            localOffset,
            new Vector2(3f, 0.6f),
            facing,
            aimDirection,
            1,
            0f,
            damage,
            3f);
    }

    protected override IEnumerator ExecuteAttackPlan(EnemyAttackPlan plan)
    {
        ShootArrow(plan);
        _shootCooldownTimer = shootCooldown;
        yield break;
    }

    private void ShootArrow(EnemyAttackPlan plan)
    {
        var arrow = projectilePrefab != null
            ? Instantiate(projectilePrefab)
            : CreateRuntimeArrow();
        arrow.name = "Arrow";
        arrow.transform.position = transform.position + (Vector3)(plan.AimDirection * 0.3f);
        arrow.layer = LayerMask.NameToLayer("Default");
        arrow.tag = "EnemyProjectile";

        var projectile = arrow.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = arrow.AddComponent<Projectile>();
        }

        projectile.Launch(
            plan.AimDirection,
            gameObject,
            plan.Damage,
            plan.IsParryable,
            plan.Knockback);
        TrackProjectile(projectile);
    }

    private static GameObject CreateRuntimeArrow()
    {
        var arrow = new GameObject("Arrow");
        var renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = PlaceholderSpriteFactory.CreateCircle(3, ShuiMoPalette.Vermillion);
        renderer.sortingOrder = 5;
        var collider = arrow.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.3f, 0.1f);
        return arrow;
    }

    protected override void OnOwnedAttackCancelled()
    {
        DestroyOwnedProjectiles();
    }

    private void TrackProjectile(Projectile projectile)
    {
        PruneOwnedProjectiles();
        if (projectile == null || !_ownedProjectiles.Add(projectile))
        {
            return;
        }

        projectile.Destroyed += HandleProjectileDestroyed;
    }

    private void HandleProjectileDestroyed(Projectile projectile)
    {
        _ownedProjectiles.Remove(projectile);
    }

    private void PruneOwnedProjectiles()
    {
        _ownedProjectiles.RemoveWhere(projectile => projectile == null);
    }

    private void DestroyOwnedProjectiles()
    {
        PruneOwnedProjectiles();
        foreach (var projectile in _ownedProjectiles)
        {
            if (projectile == null)
            {
                continue;
            }

            projectile.Destroyed -= HandleProjectileDestroyed;
            Destroy(projectile.gameObject);
        }

        _ownedProjectiles.Clear();
    }
}
