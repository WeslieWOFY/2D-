using System.Collections;
using UnityEngine;

/// <summary>
/// 三龟巨魔炮台：挂载到 ThreeTurtleTroll 子对象上。
///
/// 发射条件：Boss 位置 X 进入右侧发射区间 [fireZoneMinX, +∞) 时才可发射。
///
/// 节奏逻辑：
///   1. 启用后等待 initialDelay 秒（初始间隔），之后 CD 开始正常流转
///   2. CD 转好 + Boss 已在发射区间 → 立即发射
///   3. CD 转好 + Boss 不在发射区间 → 等待，Boss 一进入区间就发射
///   4. Boss 刚从左侧进入发射区间（此帧进入，上一帧不在区间内）：
///      - 若 CD 已好 → 延迟 0.3s~0.8s 再发射（模拟瞄准/蓄力感）
///      - 若 CD 未好 → 不额外延迟，CD 自然转好后正常发射
///
/// 发射方向：由 launchAngle 字段直接决定，不受父级动画旋转影响。
///   launchAngle: 0°=水平向左，90°=竖直向上，-90°=竖直向下，x 始终向左。
///
/// 发射位置：始终在地面高度（Boss 跳跃时不抬高发射点）。
/// </summary>
public class TrollCannon : MonoBehaviour
{
    [Header("Boss 引用")]
    [SerializeField] private Transform bossTransform;       // 三龟巨魔的 Transform（拖入或留空自动查找父级）

    [Header("发射区间（世界坐标 X）")]
    [SerializeField] private float fireZoneMinX = 0f;       // 发射区间左边界（X ≥ 此值时可发射）

    [Header("发射节奏")]
    [SerializeField] private float initialDelay = 2f;       // 初始间隔时间（首次发射前的等待）
    [SerializeField] private float fireInterval = 1.5f;     // 发射 CD（秒）

    [Header("刚进入区间额外延迟")]
    [SerializeField] private float justArrivedDelayMin = 0.3f;  // 最小延迟
    [SerializeField] private float justArrivedDelayMax = 0.8f;  // 最大延迟

    [Header("发射点")]
    [SerializeField] private Transform firePoint;            // 发射口位置（留空 = 自身）
    [SerializeField] private bool lockToGroundY = true;      // 锁定地面高度发射（Boss 跳跃时不抬高发射点）

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;        // 子弹预制体
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletSpeed = 5f;
    [Tooltip("发射仰角（度）：0=水平向左，90=竖直向上，-90=竖直向下。x 始终向左，不受父级动画旋转影响。")]
    [SerializeField] private float launchAngle = 0f;        // 直接设仰角，不从 transform 推导
    [Tooltip("分裂子弹伤害系数：若子弹挂了 BulletSplitter，发射时设置其 damageCoefficient")]
    [SerializeField] private float splitDamageCoefficient = 1f;

    // ── 内部状态 ──
    private bool initialWaitDone;          // 初始间隔是否已过
    private float cdRemaining;             // CD 剩余时间
    private bool wasInFireZone;            // 上一帧 Boss 是否在发射区间内
    private bool isWaitingJustArrived;     // 是否正在等待"刚到达"额外延迟
    private Coroutine justArrivedCoroutine;
    private float groundY;                 // 发射点地面高度（Boss 不跳时的 Y）

    /// <summary>运行时修改发射仰角</summary>
    public float LaunchAngle
    {
        get => launchAngle;
        set => launchAngle = Mathf.Clamp(value, -90f, 90f);
    }

    private void Awake()
    {
        if (bossTransform == null)
            bossTransform = transform.parent;

        if (firePoint == null)
            firePoint = transform;
    }

    private void OnEnable()
    {
        initialWaitDone = false;
        cdRemaining = 0f;
        wasInFireZone = false;
        isWaitingJustArrived = false;
        justArrivedCoroutine = null;

        // 记录发射点的地面高度（此时 Boss 应在地面）
        groundY = firePoint.position.y;

        StartCoroutine(InitialWaitRoutine());
    }

    private void OnDisable()
    {
        if (justArrivedCoroutine != null)
        {
            StopCoroutine(justArrivedCoroutine);
            justArrivedCoroutine = null;
        }
    }

    private void Update()
    {
        if (bossTransform == null) return;

        if (!initialWaitDone) return;

        if (cdRemaining > 0f)
            cdRemaining -= Time.deltaTime;

        bool inFireZone = bossTransform.position.x >= fireZoneMinX;
        bool justArrived = inFireZone && !wasInFireZone;
        wasInFireZone = inFireZone;

        if (isWaitingJustArrived) return;

        if (cdRemaining <= 0f && inFireZone)
        {
            if (justArrived)
            {
                float delay = Random.Range(justArrivedDelayMin, justArrivedDelayMax);
                isWaitingJustArrived = true;
                justArrivedCoroutine = StartCoroutine(JustArrivedFireRoutine(delay));
            }
            else
            {
                Fire();
            }
        }
    }

    private IEnumerator InitialWaitRoutine()
    {
        yield return new WaitForSeconds(initialDelay);
        initialWaitDone = true;
    }

    private IEnumerator JustArrivedFireRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        isWaitingJustArrived = false;
        justArrivedCoroutine = null;

        if (bossTransform != null && bossTransform.position.x >= fireZoneMinX)
        {
            Fire();
        }
    }

    /// <summary>
    /// 发射一颗子弹。
    /// ● 方向：由 launchAngle 决定，x 始终向左，不受父级动画旋转影响
    /// ● 位置：firePoint 的 X + 地面 Y（Boss 跳跃时不抬高）
    /// </summary>
    public void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;

        cdRemaining = fireInterval;

        // ── 发射仰角（钳制到 [-90, 90]，保证 x 向左） ──
        float angle = Mathf.Clamp(launchAngle, -90f, 90f);
        float rad = angle * Mathf.Deg2Rad;
        Vector2 fireDir = new Vector2(-Mathf.Cos(rad), Mathf.Sin(rad));

        // ── 发射位置：X 用 firePoint 当前值，Y 锁定地面高度 ──
        Vector3 spawnPos = firePoint.position;
        if (lockToGroundY)
            spawnPos.y = groundY;

        // ── 子弹朝向旋转：精灵默认朝左(Vector2.left)，旋转对准实际发射方向 ──
        Quaternion fireRot = Quaternion.FromToRotation(Vector2.left, fireDir);

        // ── 取出子弹 ──
        GameObject bullet = PoolManager.Release(bulletPrefab, spawnPos, fireRot);
        if (bullet == null) return;

        // ── 设置 EnemyBullet（直线运动或被 BulletParabolic 覆盖） ──
        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(fireDir);
            eb.SetMoveSpeed(bulletSpeed);
        }

        // ── 设置 BulletParabolic（若存在） ──
        BulletParabolic parabolic = bullet.GetComponent<BulletParabolic>();
        if (parabolic != null)
        {
            parabolic.SetInitialVelocity(angle, bulletSpeed);
        }

        // ── 设置 BulletSplitter 伤害系数（若存在） ──
        BulletSplitter splitter = bullet.GetComponent<BulletSplitter>();
        if (splitter != null)
        {
            splitter.SetDamageCoefficient(splitDamageCoefficient);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 发射区间边界线
        float y = Application.isPlaying ? groundY : transform.position.y;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(
            new Vector3(fireZoneMinX, y - 3f, 0f),
            new Vector3(fireZoneMinX, y + 3f, 0f));

        // 可发射区间标记
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        for (int i = 0; i < 5; i++)
        {
            float x = fireZoneMinX + 0.5f + i * 0.5f;
            Gizmos.DrawLine(new Vector3(x, y - 0.2f, 0f), new Vector3(x, y + 0.2f, 0f));
        }

        // 发射点 + 方向箭头
        if (firePoint != null)
        {
            Vector3 fp = firePoint.position;
            if (Application.isPlaying && lockToGroundY)
                fp.y = groundY;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(fp, 0.15f);

            float a = Mathf.Clamp(launchAngle, -90f, 90f);
            float r = a * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(-Mathf.Cos(r), Mathf.Sin(r));
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(fp, dir * 1.2f);
        }
    }
#endif
}
