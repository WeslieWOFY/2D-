using System.Collections;
using UnityEngine;

/// <summary>
/// 派罗斯受击点 —— 继承 Enemy，挂在派罗斯的子物体上。
///
/// 职责：
///   · 持有 HP，接收玩家子弹伤害（复用 Enemy 基类受击逻辑）
///   · 受击变红（含子物体精灵联动）
///   · 死亡：播放爆炸，通知派罗斯本体禁用，并广播 BossDied
/// </summary>
public class PyrosHitPoint : Enemy
{
    [Header("受击闪红")]
    [SerializeField] private SpriteRenderer childSpriteRenderer;   // 子物体精灵（受击联动变红，可留空）

    [Header("死亡通知")]
    [SerializeField] private string destroyEventName = BossEvents.Died;   // 死亡时广播的关卡事件名
    [Tooltip("死亡时是否通过事件总线通知所有子弹禁用自身")]
    [SerializeField] private bool clearBulletsOnDeath = true;
    [Tooltip("清屏事件名（EnemyBullet 已监听该事件并自禁）")]
    [SerializeField] private string clearBulletsEventName = "ClearAllBullets";
    [Tooltip("子弹禁用持续期（秒）：0 只禁用当前在场子弹；>0 期间新生成的子弹也会被禁用")]
    [SerializeField] private float clearBulletBanDuration = 0f;
    [SerializeField] private Pyros parentBoss;                     // 父物体派罗斯（Inspector 直接拖拽引用）

    // ── 子物体颜色缓存 ──
    private Color childOriginalColor;
    private bool childColorSaved;

    // ==================== 生命周期 ====================

    protected override void Awake()
    {
        base.Awake();

        // 兜底：如果 Inspector 没拖引用，自动找父物体
        if (parentBoss == null)
            parentBoss = GetComponentInParent<Pyros>();

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
        childColorSaved = false;

        // 派罗斯出现：广播满血事件，BOSS 血条据此出现（解耦，不直接引用血条）
        BossEvents.NotifyAppear(maxHP);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (childSpriteRenderer != null)
            childSpriteRenderer.color = childOriginalColor;
    }

    // ==================== Enemy 抽象方法 ====================

    public override void OnMove()
    {
        // 位置由父物体派罗斯控制，自身不移动
    }

    public override void OnAttack()
    {
        // 攻击由父物体派罗斯统一驱动，受击点自身不攻击
    }

    /// <summary>受击扣血后广播当前血量，BOSS 血条据此更新（解耦）</summary>
    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
        BossEvents.NotifyHpChanged(currentHP);
    }

    // ==================== 受击闪红（含子物体联动） ====================

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDie) return;
        if (!other.CompareTag("PlayerBullet")) return;

        // 音效 + 扣血（走 Enemy 基类逻辑）
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

        // 广播最终 BOSS 阵亡事件（关卡系统据此结算胜利等）
        LevelEventBus.Trigger(destroyEventName);

        // 通过事件总线通知所有子弹禁用自身（清屏弹幕）
        if (clearBulletsOnDeath)
            LevelEventBus.Trigger(clearBulletsEventName, clearBulletBanDuration);

        // 死亡后直接通知父物体派罗斯禁用自身
        if (parentBoss != null)
            parentBoss.OnHitPointDied();

        gameObject.SetActive(false);
    }
}
