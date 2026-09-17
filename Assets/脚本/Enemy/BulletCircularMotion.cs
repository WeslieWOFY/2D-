using UnityEngine;

/// <summary>
/// 子弹圆周运动组件：挂载到EnemyBullet对象上，让子弹沿圆周轨迹飞行。
/// 支持半径随时间缓慢变大（螺旋扩散），设置半径增长速度为0则为纯圆周运动。
///
/// 使用说明：
/// - 挂载到带有 EnemyBullet 的子弹预制体上
/// - 组件会在 OnEnable 时自动禁用 EnemyBullet 的默认直线移动，改为圆周运动
/// - 若半径会增长到超出屏幕，建议在 EnemyBullet 上将 Disable Out Of Bounds 设为 false
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
public class BulletCircularMotion : MonoBehaviour
{
    [Header("圆周运动")]
    [SerializeField] private float orbitRadius = 2f;           // 圆周半径
    [SerializeField] private float orbitSpeed = 180f;          // 角速度（度/秒）
    [SerializeField] private bool clockwise = true;            // 顺时针旋转

    [Header("半径变化")]
    [SerializeField] private float radiusGrowthRate = 0f;      // 半径增长速度（单位/秒），设为0则纯圆周运动
    [SerializeField] private float maxRadius = 10f;            // 最大半径，超过则禁用子弹（仅 radiusGrowthRate>0 时生效）

    [Header("初始角度")]
    [SerializeField] private float startAngle = 0f;            // 初始角度（度），0=右，90=上，180=左，-90=下

    private EnemyBullet enemyBullet;
    private Vector2 orbitCenter;
    private float currentRadius;
    private float currentAngle;

    private float originalOrbitSpeed;
    private bool originalClockwise;
    private float originalOrbitRadius;

    private void Awake()
    {
        enemyBullet = GetComponent<EnemyBullet>();
        originalOrbitSpeed = orbitSpeed;
        originalClockwise = clockwise;
        originalOrbitRadius = orbitRadius;
    }

    private void OnEnable()
    {
        enemyBullet.SetMoveSpeed(0f);

        orbitCenter = transform.position;
        currentRadius = orbitRadius;
        currentAngle = startAngle * Mathf.Deg2Rad;
        orbitSpeed = originalOrbitSpeed;
        clockwise = originalClockwise;
        orbitRadius = originalOrbitRadius;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 角度递进
        float dir = clockwise ? -1f : 1f;
        currentAngle += orbitSpeed * Mathf.Deg2Rad * dir * dt;

        // 半径递进（螺旋扩散）
        if (radiusGrowthRate != 0f)
        {
            currentRadius += radiusGrowthRate * dt;

            if (maxRadius > 0f && currentRadius >= maxRadius)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        // 应用到位置
        Vector2 offset = new Vector2(
            Mathf.Cos(currentAngle) * currentRadius,
            Mathf.Sin(currentAngle) * currentRadius
        );
        transform.position = orbitCenter + offset;
    }

    /// <summary>
    /// 由发射器调用：覆盖圆心和初始角度。
    /// 半径自动从当前位置与圆心的距离推导，组件自己管自己的半径。
    /// </summary>
    public void Initialize(Vector2 center, float startAngleDeg)
    {
        orbitCenter = center;
        currentAngle = startAngleDeg * Mathf.Deg2Rad;
        // 自动从出生点推导半径
        float dist = Vector2.Distance(transform.position, center);
        if (dist > 0.01f)
            currentRadius = dist;
    }

    /// <summary>发射器调用，设置角速度（度/秒）</summary>
    public void SetOrbitSpeed(float speed)
    {
        orbitSpeed = speed;
    }

    /// <summary>发射器调用，设置旋转方向</summary>
    public void SetClockwise(bool cw)
    {
        clockwise = cw;
    }

    /// <summary>获取当前轨道角度（度）</summary>
    public float GetCurrentAngleDeg()
    {
        return currentAngle * Mathf.Rad2Deg;
    }

    /// <summary>获取轨道角速度（度/秒）</summary>
    public float GetOrbitSpeed()
    {
        return orbitSpeed;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || enemyBullet == null) return;

        // 绘制圆周轨道
        Gizmos.color = Color.cyan;
        const int segments = 64;
        Vector2 center = orbitCenter;
        for (int i = 0; i < segments; i++)
        {
            float a0 = (float)i / segments * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
            Vector2 p0 = center + new Vector2(Mathf.Cos(a0) * currentRadius, Mathf.Sin(a0) * currentRadius);
            Vector2 p1 = center + new Vector2(Mathf.Cos(a1) * currentRadius, Mathf.Sin(a1) * currentRadius);
            Gizmos.DrawLine(p0, p1);
        }

        // 绘制圆心
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(orbitCenter, 0.15f);
    }
#endif
}
