using System.Collections;
using UnityEngine;

/// <summary>
/// 猴子石板受击点 —— 继承 Enemy，挂在猴子石板的子物体上。
///
/// 两种状态：
///   未被扔出（正常）→ SelfRotator 自转 + 周期性发射 12 发环形子弹，首发射向正上方
///   被扔出           → 加速自转，停止射击
///
/// 环形子弹：
///   - 12 发均匀分布，首发射向 90°（垂直向上）
///   - 全都挂有 BulletDirectionChangerV2
///   - 随机部分变向 / 部分不变向
///   - 初始精灵朝向 = 发射方向（素材默认朝上，旋转角 = 方向角 - 90°）
/// </summary>
public class MonkeySlateHitPoint : Enemy
{
    [Header("环形子弹")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int bulletCount = 12;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float fireInterval = 2f;
    [SerializeField] private float fireRadius = 1f;

    [Header("变向（整轮统一）")]
    [SerializeField] [Range(0f, 1f)] private float changeChance = 0.5f;          // 该轮全部变向的概率
    [SerializeField] private float changeTime = 0.05f;                           // 统一变向时间

    [Header("扔出视觉")]
    [SerializeField] private float thrownExtraRotateSpeed = 360f;

    [Header("受击闪红")]
    [SerializeField] private SpriteRenderer childSpriteRenderer;

    [Header("死亡通知")]
    [SerializeField] private string destroyEventName = "MonkeySlateHitPointDestroyed";

    // ── 状态 ──
    private bool isThrownOut;

    // ── 组件缓存 ──
    private SelfRotator selfRotator;

    // ── 协程 ──
    private Coroutine fireCoroutine;

    // ==================== 生命周期 ====================

    protected override void Awake()
    {
        base.Awake();
        selfRotator = GetComponent<SelfRotator>();

        // 自动搜子物体精灵（跳过自身，只找子级）
        if (childSpriteRenderer == null)
        {
            foreach (Transform child in transform)
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    childSpriteRenderer = sr;
                    break;
                }
            }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        isThrownOut = false;
        StartFire();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isThrownOut = false;
        StopFire();

        if (childSpriteRenderer != null)
            childSpriteRenderer.color = childOriginalColor;
    }

    // ==================== Enemy 抽象方法 ====================

    public override void OnMove()
    {
        // 位置由父物体 MonkeySlateBoss 控制，自身不移动
    }

    public override void OnAttack()
    {
        // 攻击由 FireRoutine 协程驱动
    }

    // ==================== 扔出 / 召回 ====================

    /// <summary>被 Boss 扔出：加速旋转 + 停火</summary>
    public void ThrowOut()
    {
        if (isThrownOut) return;
        isThrownOut = true;

        StopFire();

        if (selfRotator != null)
        {
            float normalSpeed = selfRotator.GetOriginalSpeed();
            selfRotator.SetSpeed(normalSpeed + thrownExtraRotateSpeed);
        }
    }

    /// <summary>被 Boss 召回：恢复转速 + 开火</summary>
    public void Recall()
    {
        isThrownOut = false;

        if (selfRotator != null)
            selfRotator.ResetSpeed();

        if (gameObject.activeInHierarchy && !isDie)
            StartFire();
    }

    // ==================== 环形子弹 ====================

    private void StartFire()
    {
        if (isThrownOut || isDie) return;
        if (fireCoroutine != null) StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine());
    }

    private void StopFire()
    {
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
    }

    private IEnumerator FireRoutine()
    {
        while (!isThrownOut && !isDie)
        {
            yield return new WaitForSeconds(fireInterval);
            if (isThrownOut || isDie) yield break;
            FireRing();
        }
    }

    private void FireRing()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[MonkeySlateHitPoint] bulletPrefab 未指定！");
            return;
        }

        float angleStep = 360f / bulletCount;
        float startAngle = 90f;

        // 整轮统一决定是否变向
        bool roundShouldChange = Random.value < changeChance;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = startAngle + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Quaternion rot = Quaternion.Euler(0f, 0f, angle - 90f);

            Vector3 spawnPos = transform.position + (Vector3)(dir * fireRadius);
            GameObject bullet = PoolManager.Release(bulletPrefab, spawnPos, rot);
            if (bullet == null) continue;

            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(bulletDamage);
                eb.SetMoveDirection(dir);
                eb.SetMoveSpeed(bulletSpeed);
            }

            BulletDirectionChangerV2 changer = bullet.GetComponent<BulletDirectionChangerV2>();
            if (changer == null)
                changer = bullet.AddComponent<BulletDirectionChangerV2>();

            // 整轮统一变向时间，是否变向
            changer.Configure(changeTime, !roundShouldChange);
            changer.InitFacing(dir);
        }
    }

    // ==================== 受击闪红（子物体联动） ====================

    private Color childOriginalColor;
    private bool childColorSaved;

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDie) return;
        if (!other.CompareTag("PlayerBullet")) return;

        // 音效 + 扣血
        AudioManager.Instance.PlaySFX(MisSFX);
        Bullet bullet = other.GetComponent<Bullet>();
        int totalDamage = bullet.damage + GameManager.Instance.attackPower;
        TakeDamage(totalDamage);

        // 记录子物体原色（只在首次变红前记录，防止连续被打时记录到红色）
        if (childSpriteRenderer != null && !childColorSaved)
        {
            childOriginalColor = childSpriteRenderer.color;
            childColorSaved = true;
        }

        // 停掉之前的闪红协程
        if (flashRedCoroutine != null)
            StopCoroutine(flashRedCoroutine);

        // 父物体变红
        if (spriteRenderer != null)
            spriteRenderer.color = Color.red;
        // 子物体变红
        if (childSpriteRenderer != null)
            childSpriteRenderer.color = Color.red;

        // 启动统一恢复协程（TakeDamage 可能触发死亡导致物体不活跃）
        if (!isDie && gameObject.activeInHierarchy)
            flashRedCoroutine = StartCoroutine(FlashRedWithChild());
    }

    private IEnumerator FlashRedWithChild()
    {
        yield return new WaitForSeconds(flashDuration);

        // 一起恢复正常
        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        if (childSpriteRenderer != null)
            childSpriteRenderer.color = childOriginalColor;
        childColorSaved = false;
    }

    // ==================== 死亡 ====================

    protected override void OnDeath()
    {
        isDie = true;
        moveSpeed = 0;
        StopFire();

        if (animator != null)
            animator.speed = 0;

        StartCoroutine(BaozhaAndDisable());
    }

    private IEnumerator BaozhaAndDisable()
    {
        AudioManager.Instance.PlaySFX(baozhaSFX);

        foreach (Transform t in boomTransform)
        {
            PoolManager.Release(baozha, t.position);
            yield return waitbaozha;
        }

        // 爆炸播完后通知父物体禁用自身
        LevelEventBus.Trigger(destroyEventName, gameObject);

        gameObject.SetActive(false);
    }

    // ==================== Editor 可视化 ====================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (bulletPrefab == null) return;

        Gizmos.color = Color.red;
        float angleStep = 360f / bulletCount;
        float startAngle = 90f;
        Vector3 center = transform.position;

        for (int i = 0; i < bulletCount; i++)
        {
            float rad = (startAngle + i * angleStep) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad));
            Gizmos.DrawRay(center, dir * 1.5f);
        }

        // 首发高亮
        Gizmos.color = Color.yellow;
        Vector3 firstDir = new Vector3(Mathf.Cos(90f * Mathf.Deg2Rad), Mathf.Sin(90f * Mathf.Deg2Rad));
        Gizmos.DrawRay(center, firstDir * 2f);
    }
#endif
}
