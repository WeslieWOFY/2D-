using System.Collections;
using UnityEngine;

/// <summary>
/// 参天炮台中部 —— 继承 Enemy，通过事件总线锁定玩家位置后发射跟踪子弹。
///
/// 行为：
///   定时通过 LevelEventBus 请求玩家位置 → 方向 = muzzle → 玩家
///   玩家不可用时（禁用/死亡/不存在）→ 默认朝正前方（Vector2.left）发射
///
/// 对比：
///   CantianCannon  —— 秘密关卡 Boss 主控（不继承 Enemy，实现 ISecretBoss）
///   HomingBulletEmitter —— 纯发射器组件（不继承 Enemy）
///   本脚本继承 Enemy，具备完整血条、受伤、闪红、死亡爆炸等基础功能
/// </summary>
public class CantianCannonMiddle : Enemy
{
    [Header("发射点")]
    [SerializeField] private Transform muzzlePoint;         // 子弹生成位置（留空 = 自身 Transform）

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;       // 子弹预制体
    [SerializeField] private float bulletSpeed = 5f;        // 子弹飞行速度

    [Header("动画")]
    [SerializeField] private float initialDelay = 2f;                // 初始启动延迟（秒），未到期前 OnAtTrigger 直接 return
    [SerializeField] private Vector2Int attackCountRange = new Vector2Int(3, 6); // 攻击计数随机范围

    [Header("死亡通知")]
    [SerializeField] private string destroyEventName = "CantianCannonMiddleDestroyed"; // 爆炸后触发的事件名，父物体监听并禁用

    [Header("默认回退方向")]
    [SerializeField] private Vector2 fallbackDirection = Vector2.left; // 玩家不可用时的发射方向


    private float enableTime;       // OnEnable 时间戳，用于初始延迟判断
    private int attackCount;        // 当前动画事件触发次数
    private int attackThreshold;    // 需要达到的次数阈值（3-6随机）

    // ==================== 生命周期 ====================

    protected override void Awake()
    {
        base.Awake();

        if (muzzlePoint == null)
            muzzlePoint = transform;

    }

    protected override void OnEnable()
    {
        base.OnEnable();

        enableTime = Time.time;

        // 随机生成攻击计数阈值（3~6）
        attackCount = 0;
        attackThreshold = Random.Range(attackCountRange.x, attackCountRange.y + 1);

    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }

    // ==================== Enemy 抽象方法 ====================

    /// <summary>炮台固定位置，不移动</summary>
    public override void OnMove()
    {
        // 参天炮台中部：原地待命，不移动
        // 如需移动（如进出屏幕），子类可覆写此方法
    }

    /// <summary>攻击由动画事件 OnAtTrigger 驱动，此方法为空</summary>
    public override void OnAttack()
    {
        // 攻击由动画循环驱动 → OnAtTrigger 计数 → FireHomingBullet
    }


    // ==================== 动画事件 → 计数 → 攻击 ====================

    /// <summary>
    /// 【动画事件回调】每次 At 动画触发时 +1，达到随机阈值后执行跟踪子弹发射。
    /// 阈值在每次初始化/攻击后重新在 attackCountRange 范围内随机生成。
    /// </summary>
    public void OnAtTrigger()
    {
        // 初始延迟未到期，不响应
        if (Time.time - enableTime < initialDelay)
            return;

        attackCount++;

        if (attackCount >= attackThreshold)
        {
            FireHomingBullet();
            attackCount = 0;
            attackThreshold = Random.Range(attackCountRange.x, attackCountRange.y + 1);
        }
    }

    /// <summary>
    /// 发射一颗跟踪子弹（由 OnAtTrigger 在达到阈值后调用）。
    /// 通过 LevelEventBus 请求玩家位置：
    ///   玩家在线 → 方向 = muzzle → 玩家
    ///   玩家禁用/不存在 → 使用 fallbackDirection（默认正前方 Vector2.left）
    /// </summary>
    private void FireHomingBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[CantianCannonMiddle] bulletPrefab 未指定！");
            return;
        }

        if (muzzlePoint == null) return;

        // ── 1. 事件总线：锁定玩家位置 ──
        var request = new PlayerPositionRequest();
        LevelEventBus.Trigger("RequestPlayerPosition", request);

        Vector2 aimDirection;
        if (request.HasPosition)
        {
            aimDirection = ((Vector2)(request.Position - muzzlePoint.position)).normalized;
        }
        else
        {
            aimDirection = fallbackDirection.normalized;
        }

        // ── 2. 从对象池取出子弹 ──
        Quaternion rot = Quaternion.FromToRotation(Vector2.left, aimDirection);
        GameObject bullet = PoolManager.Release(bulletPrefab, muzzlePoint.position, rot);
        if (bullet == null) return;

        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(damege);
            eb.SetMoveDirection(aimDirection);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    // ==================== 死亡 ====================

    protected override void OnDeath()
    {

        isDie = true;
        moveSpeed = 0;

        if (animator != null)
            animator.speed = 0;

        // 自定义爆炸：爆炸后通知父物体，自身不主动 SetActive(false)
        StartCoroutine(BaozhaAndNotify());
    }

    /// <summary>
    /// 爆炸协程：播放完爆炸特效后，通过事件总线通知父物体禁用自身。
    /// 不调用 base.Baozha()，因为基类爆炸末尾会 SetActive(false) 导致通知发不出去。
    /// </summary>
    private IEnumerator BaozhaAndNotify()
    {
        AudioManager.Instance.PlaySFX(baozhaSFX);

        foreach (Transform t in boomTransform)
        {
            PoolManager.Release(baozha, t.position);
            yield return waitbaozha;
        }

        // 爆炸完成 → 通知父物体来禁用自己
        LevelEventBus.Trigger(destroyEventName, gameObject);
    }

    // ==================== Editor 可视化 ====================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (muzzlePoint == null) return;

        // 发射口位置
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(muzzlePoint.position, 0.15f);

        // 默认发射方向（正前方）
        Gizmos.color = Color.red;
        Gizmos.DrawRay(muzzlePoint.position, (Vector2)fallbackDirection.normalized * 2f);
    }
#endif
}
