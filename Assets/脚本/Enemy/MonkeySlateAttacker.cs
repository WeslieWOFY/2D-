using System.Collections;
using UnityEngine;

/// <summary>
/// 猴子石板攻击体 —— 不继承 Enemy，独立 MonoBehaviour。
///
/// 两个攻击位置，两种攻击方式随机切换：
///   齐射模式（Simultaneous）：两个点位同时各发射 N 发圆形子弹
///   交替模式（Alternate）  ：两个点位交替发射 M 发圆形子弹，共 K 个来回
///     → 点位A 发射 → 短暂延迟 → 点位B 发射 → 短暂延迟 → 点位A 发射 → …
///
/// 每次攻击方式随机选择，两种模式各有独立的攻击间隔。
/// 有初始发射 CD，启用后先等待初始冷却再进入攻击循环。
/// </summary>
public class MonkeySlateAttacker : MonoBehaviour
{
    // ==================== 攻击位置 ====================

    [Header("攻击位置")]
    [SerializeField] private Transform attackPoint1;
    [SerializeField] private Transform attackPoint2;

    // ==================== 子弹通用 ====================

    [Header("子弹通用")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float fireRadius = 0.5f;            // 子弹生成距发射点的半径
    [SerializeField] private float startAngle = 90f;             // 首发角度（90° = 正上方）

    // ==================== 齐射模式 ====================

    [Header("齐射模式（两个点位同时发射）")]
    [SerializeField] private int simultaneousBulletCount = 20;    // 每个点位发射数量
    [SerializeField] private float simultaneousInterval = 3f;     // 齐射后间隔

    // ==================== 交替模式 ====================

    [Header("交替模式（两个点位交替发射）")]
    [SerializeField] private int alternateBulletCount = 12;       // 每个点位每次发射数量
    [SerializeField] private int alternateRounds = 2;             // 来回数（1来回 = A→B，2来回 = A→B→A→B）
    [SerializeField] private float alternateDelay = 0.3f;         // 一个点位发射后另一个点位的延迟
    [SerializeField] private float alternateInterval = 3f;        // 交替整轮结束后间隔

    // ==================== 发射节奏 ====================

    [Header("发射节奏")]
    [SerializeField] private float initialCooldown = 2f;          // 初始发射 CD
    [SerializeField] private float minAttackInterval = 2f;        // 攻击间隔最小值（随机用）
    [SerializeField] private float maxAttackInterval = 4f;        // 攻击间隔最大值（随机用）
    [SerializeField] private bool useFixedInterval = true;        // true = 使用模式固定间隔，false = 使用随机间隔

    // ==================== 角度旋转 ====================

    [Header("角度旋转（每轮递增偏移）")]
    [SerializeField] private bool enableAngleRotation = true;     // 是否启用每轮角度递增
    [SerializeField] private float angleRotationStep = 15f;       // 每轮递增角度

    // ==================== 两个点位角度偏移 ====================

    [Header("点位独立角度偏移")]
    [SerializeField] private float point1AngleOffset = 0f;        // 点位 1 额外角度偏移
    [SerializeField] private float point2AngleOffset = 0f;        // 点位 2 额外角度偏移

    // ==================== 激活控制 ====================

    [Header("激活控制")]
    [SerializeField] private bool autoStartOnEnable = true;       // 启用时自动开始攻击循环
    [SerializeField] private bool stopOnDisable = true;           // 禁用时停止攻击

    // ==================== 运行时状态 ====================

    private Coroutine attackCoroutine;
    private float currentAngleOffset;                             // 当前累计角度偏移

    // ==================== 生命周期 ====================

    private void OnEnable()
    {
        currentAngleOffset = 0f;

        if (autoStartOnEnable)
            StartAttack();
    }

    private void OnDisable()
    {
        if (stopOnDisable)
            StopAttack();
    }

    // ==================== 公开 API ====================

    /// <summary>开始攻击循环</summary>
    public void StartAttack()
    {
        StopAttack();
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    /// <summary>停止攻击循环</summary>
    public void StopAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }

    /// <summary>外部手动触发一次齐射</summary>
    public void FireSimultaneous()
    {
        FireRing(attackPoint1, simultaneousBulletCount, point1AngleOffset);
        FireRing(attackPoint2, simultaneousBulletCount, point2AngleOffset);

        if (enableAngleRotation)
            currentAngleOffset += angleRotationStep;
    }

    /// <summary>外部手动触发一次交替发射（完整来回）</summary>
    public void FireAlternate()
    {
        StartCoroutine(AlternateAttack());
    }

    // ==================== 攻击主循环 ====================

    private IEnumerator AttackRoutine()
    {
        // 初始冷却
        if (initialCooldown > 0f)
            yield return new WaitForSeconds(initialCooldown);

        while (true)
        {
            // 随机选择攻击方式
            bool useSimultaneous = Random.value < 0.5f;

            if (useSimultaneous)
            {
                yield return SimultaneousAttack();
            }
            else
            {
                yield return AlternateAttack();
            }
        }
    }

    // ==================== 齐射模式 ====================

    private IEnumerator SimultaneousAttack()
    {
        // 两个点位同时发射
        FireRing(attackPoint1, simultaneousBulletCount, point1AngleOffset);
        FireRing(attackPoint2, simultaneousBulletCount, point2AngleOffset);

        // 角度递增
        if (enableAngleRotation)
            currentAngleOffset += angleRotationStep;

        // 间隔
        float interval = useFixedInterval
            ? simultaneousInterval
            : Random.Range(minAttackInterval, maxAttackInterval);

        yield return new WaitForSeconds(interval);
    }

    // ==================== 交替模式 ====================

    private IEnumerator AlternateAttack()
    {
        for (int round = 0; round < alternateRounds; round++)
        {
            // 点位 A 发射
            FireRing(attackPoint1, alternateBulletCount, point1AngleOffset);

            yield return new WaitForSeconds(alternateDelay);

            // 点位 B 发射
            FireRing(attackPoint2, alternateBulletCount, point2AngleOffset);

            // 如果不是最后一个来回，再等一个延迟
            if (round < alternateRounds - 1)
                yield return new WaitForSeconds(alternateDelay);
        }

        // 角度递增
        if (enableAngleRotation)
            currentAngleOffset += angleRotationStep;

        // 间隔
        float interval = useFixedInterval
            ? alternateInterval
            : Random.Range(minAttackInterval, maxAttackInterval);

        yield return new WaitForSeconds(interval);
    }

    // ==================== 环形子弹发射 ====================

    /// <summary>
    /// 从指定点位发射一圈圆形子弹。
    /// </summary>
    /// <param name="firePoint">发射位置</param>
    /// <param name="count">发射数量</param>
    /// <param name="extraAngleOffset">该点位额外角度偏移</param>
    private void FireRing(Transform firePoint, int count, float extraAngleOffset)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[MonkeySlateAttacker] bulletPrefab 未指定！");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning("[MonkeySlateAttacker] 发射点位未指定！");
            return;
        }

        float angleStep = 360f / count;
        float baseAngle = startAngle + currentAngleOffset + extraAngleOffset;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 子弹朝向：素材默认朝上，旋转角 = 方向角 - 90°
            Quaternion rot = Quaternion.Euler(0f, 0f, angle - 90f);

            // 生成位置 = 发射点 + 方向 × 半径
            Vector3 spawnPos = firePoint.position + (Vector3)(dir * fireRadius);

            GameObject bullet = PoolManager.Release(bulletPrefab, spawnPos, rot);
            if (bullet == null) continue;

            // 设置 EnemyBullet
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(bulletDamage);
                eb.SetMoveDirection(dir);
                eb.SetMoveSpeed(bulletSpeed);
            }
        }
    }

    // ==================== 重置角度偏移 ====================

    /// <summary>外部调用重置累计角度偏移</summary>
    public void ResetAngleOffset()
    {
        currentAngleOffset = 0f;
    }

    // ==================== Editor 可视化 ====================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 绘制两个攻击位置
        Gizmos.color = Color.red;
        if (attackPoint1 != null)
        {
            Gizmos.DrawWireSphere(attackPoint1.position, 0.2f);
            DrawRingGizmos(attackPoint1.position, simultaneousBulletCount, point1AngleOffset);
        }

        Gizmos.color = Color.blue;
        if (attackPoint2 != null)
        {
            Gizmos.DrawWireSphere(attackPoint2.position, 0.2f);
            DrawRingGizmos(attackPoint2.position, simultaneousBulletCount, point2AngleOffset);
        }
    }

    private void DrawRingGizmos(Vector3 center, int count, float extraOffset)
    {
        if (count <= 0) return;

        float angleStep = 360f / count;
        float baseAngle = startAngle + extraOffset;

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.4f);
        for (int i = 0; i < count; i++)
        {
            float rad = (baseAngle + i * angleStep) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad));
            Gizmos.DrawRay(center, dir * 1.2f);
        }
    }
#endif
}
