using System.Collections;
using UnityEngine;

/// <summary>
/// 旋转弹药发射器 · 主要受击部位：继承 Enemy。
/// 触碰到玩家子弹时，把基类的受伤逻辑（音效 + 扣血 + 变红）包装成一个方法，
/// 并在该方法里让两个额外的物体也一起变红。
/// </summary>
public class RotatingAmmoLauncherHitPoint : Enemy
{
    [Header("联动变红物体")]
    [SerializeField] private SpriteRenderer extraRendererA;   // 一起变红的物体 1
    [SerializeField] private SpriteRenderer extraRendererB;   // 一起变红的物体 2

    // 两个额外物体各自的原始颜色（Awake 记录一次，用于变红后恢复）
    private Color extraOriginalColorA;
    private Color extraOriginalColorB;

    // 父物体引用（RotatingAmmoLauncher），死亡时通知其禁用自身
    private GameObject parentBody;

    // ==================== 生命周期 ====================

    protected override void Awake()
    {
        base.Awake();

        // 直接获取父物体引用
        parentBody = transform.parent != null ? transform.parent.gameObject : null;

        // 记录两个额外物体的原色，供变红后恢复
        if (extraRendererA != null)
            extraOriginalColorA = extraRendererA.color;
        if (extraRendererB != null)
            extraOriginalColorB = extraRendererB.color;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // 对象池复用后可能残留红色，启用时恢复原色
        if (extraRendererA != null)
            extraRendererA.color = extraOriginalColorA;
        if (extraRendererB != null)
            extraRendererB.color = extraOriginalColorB;
    }

    // ==================== Enemy 抽象方法 ====================

    // 本部位由父物体 RotatingAmmoLauncher 控制，自身不移动、不主动攻击
    public override void OnMove() { }
    public override void OnAttack() { }

    // ==================== 受击（包装基类受伤逻辑） ====================

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDie) return;
        if (!other.CompareTag("PlayerBullet")) return;

        // 把基类的受伤逻辑包装成一个方法，方法内新增两个一起变红的物体
        HandlePlayerBulletHit(other);
    }

    /// <summary>
    /// 包装后的受伤方法：
    /// 播放受击音效 → 计算并扣除伤害 → 自身 + 两个额外物体一起变红 → 统一恢复。
    /// public，供受击转移脚本等外部对象直接调用。
    /// </summary>
    public virtual void HandlePlayerBulletHit(Collider2D other)
    {
        if (isDie) return;

        // ── 基类受伤逻辑 ──
        AudioManager.Instance.PlaySFX(MisSFX);

        Bullet bullet = other.GetComponent<Bullet>();
        if (flashRedCoroutine != null)
            StopCoroutine(flashRedCoroutine);

        int totalDamage = bullet.damage + GameManager.Instance.attackPower;
        TakeDamage(totalDamage);

        // ── 新增：两个额外物体一起变红 ──
        if (spriteRenderer != null)
            spriteRenderer.color = Color.red;
        if (extraRendererA != null)
            extraRendererA.color = Color.red;
        if (extraRendererB != null)
            extraRendererB.color = Color.red;

        // TakeDamage 可能触发死亡导致物体不活跃，此时不再启动恢复协程
        if (!isDie && gameObject.activeInHierarchy)
            flashRedCoroutine = StartCoroutine(FlashRedWithExtras());
    }

    /// <summary>变红结束后，自身与两个额外物体一起恢复原色</summary>
    private IEnumerator FlashRedWithExtras()
    {
        yield return new WaitForSeconds(flashDuration);

        if (spriteRenderer != null)
            spriteRenderer.color = originalColor;
        if (extraRendererA != null)
            extraRendererA.color = extraOriginalColorA;
        if (extraRendererB != null)
            extraRendererB.color = extraOriginalColorB;
    }

    // ==================== 死亡 ====================

    protected override void OnDeath()
    {
        isDie = true;
        moveSpeed = 0;

        if (flashRedCoroutine != null)
            StopCoroutine(flashRedCoroutine);

        if (animator != null)
            animator.speed = 0;

        StartCoroutine(DeathRoutine());
    }

    /// <summary>播放爆炸特效后，通知父物体禁用自身</summary>
    private IEnumerator DeathRoutine()
    {
        AudioManager.Instance.PlaySFX(baozhaSFX);

        foreach (Transform t in boomTransform)
        {
            PoolManager.Release(baozha, t.position);
            yield return waitbaozha;
        }

        // 直接禁用父物体
        if (parentBody != null)
            parentBody.SetActive(false);
    }
}
