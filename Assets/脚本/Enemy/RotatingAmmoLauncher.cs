using System.Collections;
using UnityEngine;

/// <summary>
/// 旋转弹药发射器（秘密关卡BOSS）：
/// 不继承 Enemy，实现 ISecretBoss。
/// 阶段：入场（固定向左移动）→ 中场不动（攻击）→ 离场（向右出屏）。
/// 无随机漂移。入场结束后，某个子物体会向上移动一小段距离。
///
/// 攻击分为两组，各自独立节奏：
///   · 组1 = 攻击方式1：一波两线抛物线（A、B 两个焦点无间隔同帧各发一条抛物线）
///   · 组2 = 攻击方式2和3随机交替：
///       - 方式2：C 点朝左上 10-20° 随机角度，随机发射两发子弹之一
///       - 方式3：D 点连续 20 连发扇形，每轮 14 发、总 140°，子弹 Z 轴旋转对齐发射方向
///   · 组3 = 激光：按节奏直接开关 Laser 子物体（无特效）
/// </summary>
public class RotatingAmmoLauncher : MonoBehaviour, ISecretBoss
{
    [Header("秘密关卡-入场")]
    [SerializeField] private float moveSpeed = 3f;              // 入场移动速度（向左）
    [SerializeField] private float enterDuration = 3f;          // 入场阶段持续时间（秒）

    [Header("子物体上移")]
    [SerializeField] private Transform movingChild;             // 入场结束后向上移动的子物体
    [SerializeField] private float childMoveUpDistance = 0.5f;  // 上移距离
    [SerializeField] private float childMoveUpDuration = 0.5f;  // 上移耗时（秒）

    [Header("攻击方式1：两轮抛物线（A/B 同时发射，发射点即抛物线焦点）")]
    [SerializeField] private Transform firePointA;              // 抛物线焦点/发射点 A
    [SerializeField] private Transform firePointB;              // 抛物线焦点/发射点 B
    [SerializeField] private GameObject parabolicBulletPrefab;  // 抛物线子弹（需含 EnemyBullet + BulletParabolic）
    [Tooltip("沿抛物线分布的子弹数，强制为奇数（顶点处占一颗），默认 11")]
    [SerializeField] private int parabolicBulletCount = 11;
    [Tooltip("抛物线开口方向：焦点=发射点。A/B 共用，统一开口向右")]
    [SerializeField] private ParabolaOpen parabolaOrientation = ParabolaOpen.Right;
    [Tooltip("焦距：焦点到顶点的距离。越小抛物线越扁、开口向右越明显；越大顶点离焦点越远但形状趋近竖直")]
    [SerializeField] private float parabolaFocalLength = 0.6f;
    [Tooltip("相邻子弹在开口方向上的间距，决定曲线长短；轨迹长度随子弹数量动态变化。A/B 分开摆放时建议调小（0.4~0.6），避免两条抛物线互相重叠")]
    [SerializeField] private float parabolaBulletSpacing = 0.5f;
    [SerializeField] private float parabolicSpeed = 8f;         // 平抛初速度
    [SerializeField] private int parabolicDamage = 10;          // 抛物线子弹伤害
    [Tooltip("发射角：0=水平向左（纯平抛），>0 斜抛上仰，<0 向下")]
    [SerializeField] private float parabolicLaunchAngle = 0f;
    [Tooltip("A 发射点的重力加速度（正值向下，负值向上）")]
    [SerializeField] private float parabolicGravityA = 9.8f;
    [Tooltip("B 发射点的重力加速度（正值向下，负值向上）")]
    [SerializeField] private float parabolicGravityB = -9.8f;
    [Header("攻击方式1 节奏（组1）")]
    [SerializeField] private float attack1InitialDelay = 1.5f;  // 组1 初始攻击间隔
    [SerializeField] private float attack1Interval = 3f;        // 组1 每次攻击之间的间隔

    [Header("攻击方式2：C点 左上10-20° 随机两发之一")]
    [SerializeField] private Transform firePointC;              // 发射点 C
    [SerializeField] private GameObject bulletPrefabC1;         // C 点两发子弹之一
    [SerializeField] private GameObject bulletPrefabC2;         // C 点两发子弹之二
    [SerializeField] private float bulletCSpeed = 5f;           // C 子弹速度
    [SerializeField] private int bulletCDamage = 10;            // C 子弹伤害
    [SerializeField] private float cMinAngle = 10f;             // 左上角度下限（相对水平左，0=左，90=上）
    [SerializeField] private float cMaxAngle = 20f;             // 左上角度上限

    [Header("攻击方式3：D点 20连发扇形（每轮14发/140°）")]
    [SerializeField] private Transform firePointD;              // 发射点 D
    [SerializeField] private GameObject fanBulletPrefab;        // 扇形子弹
    [SerializeField] private int fanVolleyCount = 20;           // 连续 20 连发
    [SerializeField] private int fanBulletCount = 14;           // 每轮扇形 14 发
    [SerializeField] private float fanSpreadAngle = 140f;       // 扇形总范围（度）
    [Tooltip("扇形中心角度（相对 +X 轴，180 = 正左）")]
    [SerializeField] private float fanCenterAngle = 180f;
    [SerializeField] private float fanVolleyInterval = 0.05f;   // 每轮扇形之间的间隔（秒）
    [SerializeField] private float fanBulletSpeed = 7f;         // 扇形子弹速度
    [SerializeField] private int fanBulletDamage = 10;          // 扇形子弹伤害
    [Tooltip("每次连发扇形初始角度往上抬的量（度）：正值每轮上抬、负值下压，扇形总角度不变")]
    [SerializeField] private float fanVolleyAngleStep = 0f;
    [Tooltip("扇形子弹Z轴旋转偏移（度）：素材默认朝上时为 -90，默认朝右为 0，默认朝下为 +90，按素材实际方向调")]
    [SerializeField] private float fanBulletRotationOffset = -90f;

    [Header("攻击方式2和3 节奏（组2，随机交替）")]
    [SerializeField] private float attack2InitialDelay = 2f;    // 组2 初始攻击间隔
    [SerializeField] private float attack2Interval = 4f;        // 组2 每次攻击之间的间隔
    [Tooltip("每次从攻击2/3中选攻击2的概率（0~1）：1=只打攻击2，0=只打攻击3，0.5=随机，便于调试")]
    [SerializeField] [Range(0f, 1f)] private float attack2Probability = 0.5f;

    [Header("激光（组3，直接激活 Laser 子物体，无特效）")]
    [SerializeField] private GameObject laser;                  // 激光子物体（含 Laser 组件）
    [SerializeField] private float laserInitialDelay = 2.5f;    // 组3 初始攻击间隔
    [SerializeField] private float laserInterval = 5f;          // 每次激活之间的间隔（从激光关闭时算起）
    [SerializeField] private float laserActiveDuration = 1.5f;  // 每次激活持续时间（秒）

    [Header("秘密关卡-离场")]
    [SerializeField] private float retreatSpeed = 4f;           // 离场移动速度（向右）

    /// <summary>抛物线开口方向（发射点为焦点）</summary>
    public enum ParabolaOpen { Left, Right, Up, Down }

    private enum LauncherState
    {
        Entering,   // 入场：固定向左移动
        Stationary, // 中场：原地不动，执行攻击指令
        Retreat,    // 离场：向右移动直到出屏
    }
    private LauncherState currentState;

    private Camera mainCam;
    private Coroutine enterCoroutine;
    private Coroutine childMoveUpCoroutine;
    private Coroutine attack1Coroutine;
    private Coroutine attack2Coroutine;
    private Coroutine laserCoroutine;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        currentState = LauncherState.Entering;

        if (enterCoroutine != null) StopCoroutine(enterCoroutine);
        enterCoroutine = StartCoroutine(EnterRoutine());
    }

    private void OnDisable()
    {
        CleanupCoroutines();
    }

    private void Update()
    {
        switch (currentState)
        {
            case LauncherState.Entering:
                // 入场：固定向左移动
                transform.Translate(moveSpeed * Time.deltaTime * Vector2.left);
                break;

            case LauncherState.Stationary:
                // 中场不动：攻击由两组协程驱动
                break;

            case LauncherState.Retreat:
                // 离场：向右移动，出屏后禁用
                transform.Translate(retreatSpeed * Time.deltaTime * Vector2.right);
                if (mainCam != null && mainCam.WorldToViewportPoint(transform.position).x > 1.2f)
                {
                    gameObject.SetActive(false);
                }
                break;
        }
    }

    /// <summary>
    /// 入场协程：等待 enterDuration 秒后进入中场，让子物体向上移动并启动攻击
    /// </summary>
    private IEnumerator EnterRoutine()
    {
        yield return new WaitForSeconds(enterDuration);

        currentState = LauncherState.Stationary;

        // 入场结束，某个子物体向上移动一小段距离
        if (childMoveUpCoroutine != null) StopCoroutine(childMoveUpCoroutine);
        childMoveUpCoroutine = StartCoroutine(MoveChildUpRoutine());

        // 进入中场，开始两组攻击
        StartAttacks();
    }

    /// <summary>
    /// 子物体上移：在 childMoveUpDuration 秒内向上平滑移动 childMoveUpDistance
    /// </summary>
    private IEnumerator MoveChildUpRoutine()
    {
        if (movingChild == null) yield break;

        Vector3 startPos = movingChild.localPosition;
        Vector3 targetPos = startPos + Vector3.up * childMoveUpDistance;

        float elapsed = 0f;
        while (elapsed < childMoveUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / childMoveUpDuration);
            movingChild.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }
        movingChild.localPosition = targetPos;
    }

    // ==================== 攻击调度 ====================

    /// <summary>启动三组攻击协程（组1独立节奏，组2随机交替方式2和3，组3激光开关）</summary>
    private void StartAttacks()
    {
        StopAttacks();
        attack1Coroutine = StartCoroutine(Attack1Routine());
        attack2Coroutine = StartCoroutine(Attack2Routine());
        laserCoroutine = StartCoroutine(LaserRoutine());
    }

    private void StopAttacks()
    {
        if (attack1Coroutine != null) { StopCoroutine(attack1Coroutine); attack1Coroutine = null; }
        if (attack2Coroutine != null) { StopCoroutine(attack2Coroutine); attack2Coroutine = null; }
        if (laserCoroutine != null) { StopCoroutine(laserCoroutine); laserCoroutine = null; }

        // 激光随攻击一起关闭，避免撤退/失活后残留
        if (laser != null) laser.SetActive(false);
    }

    /// <summary>
    /// 组1：按 attack1Interval 循环发射「一波两线」抛物线（A、B 无间隔同帧）
    /// </summary>
    private IEnumerator Attack1Routine()
    {
        yield return new WaitForSeconds(attack1InitialDelay);

        // 状态守卫：一旦离场/失活即退出循环，保证撤退后不再开火
        while (currentState == LauncherState.Stationary)
        {
            FireParabolicSalvo();
            yield return new WaitForSeconds(attack1Interval);
        }
    }

    /// <summary>
    /// 一波两线：A、B 两个焦点各自发射一条抛物线，同帧、无间隔。
    /// </summary>
    private void FireParabolicSalvo()
    {
        FireParabolicBurst(firePointA);                      // 焦点 A 一条抛物线
        FireParabolicBurst(firePointB);                      // 焦点 B 一条抛物线（与 A 同帧）
    }

    /// <summary>
    /// 以 focus 为焦点发射一组抛物线子弹：
    /// 子弹生成位置沿一条抛物线分布（发射点就是这条抛物线的焦点），
    /// 每颗子弹再以 BulletParabolic 做平抛/斜抛运动。
    /// </summary>
    private void FireParabolicBurst(Transform focus)
    {
        if (focus == null || parabolicBulletPrefab == null) return;

        int count = ForceOddBulletCount(); // 强制奇数，顶点处占一颗
        for (int i = 0; i < count; i++)
        {
            // 沿抛物线均布：t 从 -1 到 +1，中间一颗正好落在顶点
            float t = count > 1 ? (float)i / (count - 1) * 2f - 1f : 0f;

            Vector3 localPos = GetParabolaLocalPoint(t);
            // 用世界坐标轴排列（focus.position + 偏移），忽略发射点自身旋转/缩放，
            // 保证 A、B 两条抛物线在世界空间统一开口向右
            Vector3 spawnPos = focus.position + localPos;

            GameObject bullet = PoolManager.Release(parabolicBulletPrefab, spawnPos, Quaternion.identity);
            if (bullet == null) continue;

            BulletParabolic bp = bullet.GetComponent<BulletParabolic>();
            if (bp != null)
            {
                bp.SetInitialVelocity(parabolicLaunchAngle, parabolicSpeed);
                // A/B 焦点各自独立的重力（池复用后由 OnEnable 先重置回默认 9.8）
                bp.SetGravity(focus == firePointB ? parabolicGravityB : parabolicGravityA);
            }

            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null) eb.SetDamage(parabolicDamage);
        }
    }

    /// <summary>子弹数强制为奇数（偶数自动 +1），保证顶点处恰好有一颗子弹</summary>
    private int ForceOddBulletCount()
    {
        int c = parabolicBulletCount;
        if (c < 1) c = 1;
        else if (c % 2 == 0) c++; // 偶数 → +1 变奇数
        return c;
    }

    /// <summary>
    /// 计算抛物线上的本地坐标点：焦点在本地原点 (0,0)。
    /// 顶点离焦点距离 = 焦距 p（可调）；开口半宽 H = (子弹数-1)/2 × 子弹间距，
    /// 所以轨迹长短随子弹数量动态变化，且顶点那一颗正好落在曲线上。
    ///   开口右：x = y²/(4p) - p   开口左：x = p - y²/(4p)
    ///   开口上：y = x²/(4p) - p   开口下：y = p - x²/(4p)
    /// </summary>
    private Vector3 GetParabolaLocalPoint(float t)
    {
        int count = ForceOddBulletCount();
        float p = Mathf.Max(parabolaFocalLength, 0.01f);          // 焦距（焦点→顶点距离）
        float H = ((count - 1) * 0.5f) * parabolaBulletSpacing;   // 开口半宽（随子弹数变化）
        float d = t * H;                                          // 沿开口方向的采样偏移

        switch (parabolaOrientation)
        {
            // 开口朝右（+X）：顶点在 (-p, 0)，臂向 +X 伸展
            case ParabolaOpen.Right:
                return new Vector3(d * d / (4f * p) - p, d, 0f);
            // 开口朝左（-X）：顶点在 (+p, 0)，臂向 -X 伸展
            case ParabolaOpen.Left:
                return new Vector3(p - d * d / (4f * p), d, 0f);
            // 开口朝上（+Y）：顶点在 (0, -p)，臂向 +Y 伸展
            case ParabolaOpen.Up:
                return new Vector3(d, d * d / (4f * p) - p, 0f);
            // 开口朝下（-Y）：顶点在 (0, +p)，臂向 -Y 伸展
            case ParabolaOpen.Down:
            default:
                return new Vector3(d, p - d * d / (4f * p), 0f);
        }
    }

    /// <summary>组2：按 attack2Interval 循环，随机交替方式2与方式3</summary>
    private IEnumerator Attack2Routine()
    {
        yield return new WaitForSeconds(attack2InitialDelay);

        // 状态守卫：一旦离场/失活即退出循环，保证撤退后不再开火
        while (currentState == LauncherState.Stationary)
        {
            if (Random.value < attack2Probability)
            {
                FireAttack2();                                  // 方式2：瞬时单发
            }
            else
            {
                yield return StartCoroutine(FireAttack3Routine()); // 方式3：20连发扇形
            }
            yield return new WaitForSeconds(attack2Interval);
        }
    }

    /// <summary>方式2：C 点朝左上 10-20° 随机角度，随机发射两发子弹之一</summary>
    private void FireAttack2()
    {
        if (firePointC == null) return;

        GameObject prefab = Random.value < 0.5f ? bulletPrefabC1 : bulletPrefabC2;
        if (prefab == null) prefab = bulletPrefabC1 != null ? bulletPrefabC1 : bulletPrefabC2;
        if (prefab == null) return;

        float angle = Random.Range(cMinAngle, cMaxAngle);
        Vector2 dir = new Vector2(-Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        GameObject bullet = PoolManager.Release(prefab, firePointC.position, Quaternion.identity);
        if (bullet == null) return;

        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletCDamage);
            eb.SetMoveDirection(dir);
            eb.SetMoveSpeed(bulletCSpeed);
        }
    }

    /// <summary>方式3：D 点连续 fanVolleyCount 连发扇形，每轮初始角度上抬 fanVolleyAngleStep</summary>
    private IEnumerator FireAttack3Routine()
    {
        for (int volley = 0; volley < fanVolleyCount; volley++)
        {
            FireFanVolley(volley * fanVolleyAngleStep); // 每轮往上抬一点
            if (volley < fanVolleyCount - 1)
                yield return new WaitForSeconds(fanVolleyInterval);
        }
    }

    /// <summary>
    /// 一轮扇形：fanBulletCount 发均匀分布在 fanSpreadAngle 度内（中心 fanCenterAngle），
    /// angleOffset 让整轮扇形整体上移（减角度 = 朝屏幕上方抬），扇形总角度不变。
    /// 同时把子弹 Z 轴旋转到与发射方向一致。
    /// </summary>
    private void FireFanVolley(float angleOffset)
    {
        if (firePointD == null || fanBulletPrefab == null) return;

        float startAngle = fanCenterAngle - fanSpreadAngle / 2f - angleOffset;
        float angleStep = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            // 子弹Z轴旋转与发射方向一致（旋转偏移可调，见 fanBulletRotationOffset）
            Quaternion rot = Quaternion.Euler(0f, 0f, angle + fanBulletRotationOffset);

            GameObject bullet = PoolManager.Release(fanBulletPrefab, firePointD.position, rot);
            if (bullet == null) continue;

            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(fanBulletDamage);
                eb.SetMoveDirection(dir);
                eb.SetMoveSpeed(fanBulletSpeed);
            }
        }
    }

    /// <summary>
    /// 组3：激光按节奏开关。激活 laserActiveDuration 秒后关闭，
    /// 再等 laserInterval 秒进入下一轮。无特效，直接 SetActive Laser 子物体。
    /// </summary>
    private IEnumerator LaserRoutine()
    {
        if (laser == null) yield break;
        laser.SetActive(false);

        yield return new WaitForSeconds(laserInitialDelay);

        // 状态守卫：一旦离场/失活即退出循环，保证撤退后激光不再激活
        while (currentState == LauncherState.Stationary)
        {
            laser.SetActive(true);
            yield return new WaitForSeconds(laserActiveDuration);
            laser.SetActive(false);
            yield return new WaitForSeconds(laserInterval);
        }
    }

    /// <summary>
    /// ISecretBoss：由外部调用，触发离场
    /// </summary>
    public void TriggerRetreat()
    {
        if (currentState == LauncherState.Retreat) return;
        currentState = LauncherState.Retreat;
        StopAttacks();
    }

    /// <summary>
    /// 统一清理所有协程引用
    /// </summary>
    private void CleanupCoroutines()
    {
        StopAttacks();
        if (enterCoroutine != null)
        {
            StopCoroutine(enterCoroutine);
            enterCoroutine = null;
        }
        if (childMoveUpCoroutine != null)
        {
            StopCoroutine(childMoveUpCoroutine);
            childMoveUpCoroutine = null;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // A、B 焦点：青色焦点 + 青色抛物线分布曲线
        DrawParabolaPreview(firePointA);
        DrawParabolaPreview(firePointB);

        // C、D 发射点：品红
        Gizmos.color = Color.magenta;
        if (firePointC != null) Gizmos.DrawWireSphere(firePointC.position, 0.15f);
        if (firePointD != null) Gizmos.DrawWireSphere(firePointD.position, 0.15f);

        // D 点扇形预览：黄色
        DrawFanPreview(firePointD);
    }

    /// <summary>绘制以 focus 为焦点的抛物线分布曲线（含焦点标记）</summary>
    private void DrawParabolaPreview(Transform focus)
    {
        if (focus == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(focus.position, 0.15f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        const int steps = 20;
        Vector3 prev = focus.position + GetParabolaLocalPoint(-1f);
        for (int i = 1; i <= steps; i++)
        {
            float t = -1f + 2f * i / steps;
            Vector3 cur = focus.position + GetParabolaLocalPoint(t);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }

    /// <summary>绘制 D 点扇形预览</summary>
    private void DrawFanPreview(Transform firePoint)
    {
        if (firePoint == null) return;

        Gizmos.color = Color.yellow;
        float half = fanSpreadAngle / 2f;
        for (int i = 0; i <= fanBulletCount; i++)
        {
            float t = fanBulletCount > 0 ? (float)i / fanBulletCount : 0f;
            float angle = (fanCenterAngle - half) + fanSpreadAngle * t;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Gizmos.DrawRay(firePoint.position, dir * 2f);
        }
    }
#endif
}
