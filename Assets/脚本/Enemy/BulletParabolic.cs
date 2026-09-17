using UnityEngine;

/// <summary>
/// 抛物线子弹组件：挂载到 EnemyBullet 对象上，使其按初速度方向做抛物线运动。
/// ● 初速度 y 分量向上（vy0 > 0）→ 斜抛运动，中途有极值点（最高点）
/// ● 初速度 y 分量向下（vy0 ≤ 0）→ 无极值点，直接向下抛落
/// 重力方向始终向下（-y），x 方向保持匀速（始终向左）。
///
/// 原理：每帧更新内部速度向量（施加重力），再同步给 EnemyBullet 的方向+速率，
/// 由 EnemyBullet.Move() 完成唯一位移。本组件不做 Translate，避免双重移动。
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
[DefaultExecutionOrder(-50)]  // 在 EnemyBullet.Update 之前运行，先同步好方向和速率
public class BulletParabolic : MonoBehaviour
{
    [Header("抛物线设置")]
    [SerializeField] private float gravity = 9.8f;           // 重力加速度（正值向下，负值向上）
    [SerializeField] private float initialSpeed = 5f;        // 初速度大小
    [SerializeField] private float launchAngle = 45f;        // 发射仰角（度）：0=水平向左，90=竖直向上，-90=竖直向下

    [Header("调试")]
    [SerializeField] private bool drawTrajectory = false;     // Scene 视图绘制预测轨迹
    [SerializeField] private float trajectoryTime = 3f;       // 轨迹绘制时长
    [SerializeField] private int trajectorySteps = 60;        // 轨迹绘制步数

    private EnemyBullet enemyBullet;
    private Vector2 velocity;          // 当前速度
    private bool hasApex;              // 是否有极值点（vy0 > 0）
    private bool reachedApex;          // 是否已经过极值点
    private bool paused;               // 暂停标志：暂停后不施加重力、不同步速度
    private float originalGravity;     // 默认重力（Awake 缓存，OnEnable 重置用）

    /// <summary>当前速度（只读）</summary>
    public Vector2 Velocity => velocity;

    /// <summary>初速度 y 分量是否向上（存在极值点）</summary>
    public bool HasApex => hasApex;

    /// <summary>是否已过极值点</summary>
    public bool ReachedApex => reachedApex;

    private void Awake()
    {
        enemyBullet = GetComponent<EnemyBullet>();
        originalGravity = gravity;   // 缓存默认重力，供对象池复用时重置
    }

    private void OnEnable()
    {
        paused = false;

        // 对象池复用：重置回默认重力（9.8），避免上一次被 B 焦点改成 -9.8 后残留
        gravity = originalGravity;

        // 根据发射仰角构造初速度：x 始终向左，y 由仰角决定
        float angle = Mathf.Clamp(launchAngle, -90f, 90f);
        float rad = angle * Mathf.Deg2Rad;
        velocity = new Vector2(-Mathf.Cos(rad) * initialSpeed, Mathf.Sin(rad) * initialSpeed);

        hasApex = velocity.y > 0f;
        reachedApex = false;

        // 首帧立即同步给 EnemyBullet，防止 EnemyBullet 用旧方向移一帧
        SyncToEnemyBullet();
    }

    private void Update()
    {
        if (enemyBullet == null || paused) return;

        float dt = Time.deltaTime;

        // ── 施加重力（仅影响 y 分量，x 始终匀速） ──
        velocity.y -= gravity * dt;

        // ── 检测极值点（vy 从正变负的瞬间） ──
        if (hasApex && !reachedApex && velocity.y <= 0f)
        {
            reachedApex = true;
        }

        // ── 同步给 EnemyBullet：方向+速率，由 EnemyBullet.Move() 完成唯一位移 ──
        SyncToEnemyBullet();
    }

    /// <summary>
    /// 将当前抛物线速度同步给 EnemyBullet 的方向和速率。
    /// EnemyBullet.Move() 会用这些值做 transform.Translate，这是唯一的位移来源。
    /// </summary>
    private void SyncToEnemyBullet()
    {
        if (enemyBullet == null) return;
        float mag = velocity.magnitude;
        if (mag > 0.0001f)
        {
            enemyBullet.SetMoveDirection(velocity / mag);
            enemyBullet.SetMoveSpeed(mag);
        }
    }

    // ── 外部接口 ──

    /// <summary>暂停抛物线运动：不再施加重力、不再同步速度到 EnemyBullet</summary>
    public void Pause()
    {
        paused = true;
        velocity = Vector2.zero;
    }

    /// <summary>恢复抛物线运动（需重新设置初速度）</summary>
    public void Resume()
    {
        paused = false;
    }

    /// <summary>
    /// 以仰角和速率设置初速度（推荐接口）。
    /// angleDeg: 发射仰角（度），0=水平向左，90=竖直向上，-90=竖直向下。
    /// x 分量始终为负（向左），y 分量由仰角决定。
    /// </summary>
    public void SetInitialVelocity(float angleDeg, float speed)
    {
        launchAngle = Mathf.Clamp(angleDeg, -90f, 90f);
        initialSpeed = speed;
        float rad = launchAngle * Mathf.Deg2Rad;
        velocity = new Vector2(-Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);
        hasApex = velocity.y > 0f;
        reachedApex = false;
        SyncToEnemyBullet();
    }

    /// <summary>
    /// 直接设置初速度向量（高级接口，调用者须保证 vx ≤ 0）。
    /// </summary>
    public void SetInitialVelocity(Vector2 v0)
    {
        velocity = v0;
        initialSpeed = v0.magnitude;
        launchAngle = Mathf.Clamp(
            Mathf.Atan2(v0.y, -v0.x) * Mathf.Rad2Deg, -90f, 90f);
        hasApex = v0.y > 0f;
        reachedApex = false;
        SyncToEnemyBullet();
    }

    /// <summary>
    /// 设置重力加速度（正值向下，负值向上）。用于不同发射点不同重力，如 B 焦点 -9.8。
    /// 对象池复用时会由 OnEnable 重置回默认值，不会残留。
    /// </summary>
    public void SetGravity(float g)
    {
        gravity = g;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawTrajectory) return;

        Vector2 pos = transform.position;
        float rad = launchAngle * Mathf.Deg2Rad;
        Vector2 v = new Vector2(-Mathf.Cos(rad) * initialSpeed, Mathf.Sin(rad) * initialSpeed);

        Gizmos.color = Color.green;
        float stepDt = trajectoryTime / trajectorySteps;
        Vector2 prev = pos;
        for (int i = 1; i <= trajectorySteps; i++)
        {
            float t = i * stepDt;
            float x = pos.x + v.x * t;
            float y = pos.y + v.y * t - 0.5f * gravity * t * t;
            Vector2 cur = new Vector2(x, y);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        if (v.y > 0f)
        {
            float tapex = v.y / gravity;
            float apexX = pos.x + v.x * tapex;
            float apexY = pos.y + v.y * tapex - 0.5f * gravity * tapex * tapex;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(new Vector3(apexX, apexY, 0f), 0.15f);
        }
    }
#endif
}
