using System.Collections;
using UnityEngine;

/// <summary>
/// 子弹组控制器 —— 挂载到子弹组父物体（圆周运动中心体）上。
///
/// 行为：
/// - 父物体以随机方向匀速移动（方向限制在向左的扇形范围内）
/// - 6个子物体围绕父物体做圆周运动，子物体之间每60°均匀分布
/// - 子物体轨道半径随时间逐渐增大，超过最大半径后整组禁用
/// - 子物体自动跟随父物体运动（每帧更新世界坐标）
///
/// 发射方式：
/// 由 GoldenElephant 在其发射点的扇形区域内随机位置生成，
/// 调用 Initialize() 可覆盖默认移动方向。
/// </summary>
public class BulletGroupController : MonoBehaviour
{
    [Header("父物体移动")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("以 Vector2.left（180°）为中心的方向散布角度（度），越大方向越随机")]
    [SerializeField] private float directionSpreadAngle = 60f;

    [Header("子子弹")]
    [SerializeField] private GameObject childBulletPrefab;       // 必须挂载 EnemyBullet 组件
    [SerializeField] private int childCount = 6;
    private int childDamage = 10;                                // 由发射方（GoldenElephant）通过 Initialize 设置

    [Header("圆周运动")]
    [SerializeField] private float startRadius = 0.5f;           // 初始轨道半径
    [SerializeField] private float orbitSpeed = 120f;            // 角速度（度/秒）
    [Tooltip("顺时针旋转")]
    [SerializeField] private bool clockwise = true;
    [SerializeField] private float radiusGrowthRate = 0.5f;      // 半径增长速度（单位/秒）
    [SerializeField] private float maxRadius = 5f;               // 最大半径，达到后不再增大（不会因此禁用）

    [Header("超时设置")]
    [SerializeField] private float maxLifetime = 15f;            // 最大存活时间（秒），超时后整组禁用
    [SerializeField] private bool disableWhenChildrenGone = true; // 所有子物体消失后是否禁用自身

    // 私有状态
    private Vector2 moveDirection;
    private float currentRadius;
    private float currentBaseAngle;  // 整体旋转基准角度（弧度）
    private GameObject[] childBullets;
    private float elapsedTime;       // 已存活时间

    // 缓存避免GC / 重复计算
    private float angleStep;
    private Camera mainCam;
    private float rightBoundary;
    private float leftBoundary;
    private float topBoundary;
    private float bottomBoundary;
    private float effectiveMargin;   // 出屏容差 = boundsOffset + maxRadius

    private void Awake()
    {
        mainCam = Camera.main;
        childBullets = new GameObject[childCount];
        angleStep = 360f / childCount;

        // 防御：如果父物体预制体上误挂了 EnemyBullet，禁用它，防止它的 CheckBounds 干扰
        EnemyBullet selfEB = GetComponent<EnemyBullet>();
        if (selfEB != null)
        {
            selfEB.enabled = false;
        }

        // 计算屏幕边界：容差必须包含最大轨道半径，确保子物体全部出屏后才禁用父物体
        // 否则父物体刚出屏、子物体还在屏幕内时整组就被禁用了
        if (mainCam != null)
        {
            Vector3 bottomLeft = mainCam.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 topRight = mainCam.ViewportToWorldPoint(new Vector3(1, 1, 0));
            effectiveMargin = 2f + maxRadius;  // 2f基础容差 + 最大轨道半径
            leftBoundary = bottomLeft.x - effectiveMargin;
            rightBoundary = topRight.x + effectiveMargin;
            bottomBoundary = bottomLeft.y - effectiveMargin;
            topBoundary = topRight.y + effectiveMargin;
        }
    }

    private void OnEnable()
    {
        // === 清理上一轮的残留子物体 ===
        for (int i = 0; i < childBullets.Length; i++)
        {
            if (childBullets[i] != null)
            {
                childBullets[i].SetActive(false);
                childBullets[i] = null;
            }
        }

        // 随机移动方向：以 Vector2.left（180°）为中心，在散布范围内随机
        float halfSpread = directionSpreadAngle * 0.5f;
        float randomAngle = Random.Range(-halfSpread, halfSpread);
        float finalAngle = (180f + randomAngle) * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));

        // 重置轨道参数
        currentRadius = startRadius;
        currentBaseAngle = 0f;
        elapsedTime = 0f;

        // 注意：子子弹不在此处生成，由 Initialize() 在伤害设置完毕后统一生成
    }

    /// <summary>
    /// 在初始轨道位置生成所有子子弹
    /// </summary>
    private void SpawnChildren()
    {
        for (int i = 0; i < childCount; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentRadius;
            Vector2 spawnPos = (Vector2)transform.position + offset;

            GameObject child = PoolManager.Release(childBulletPrefab, spawnPos, Quaternion.identity);
            if (child != null)
            {
                EnemyBullet eb = child.GetComponent<EnemyBullet>();
                if (eb != null)
                {
                    eb.SetMoveSpeed(0f);       // 禁用自主直线移动，由本控制器统一站位
                    eb.SetDamage(childDamage);
                }
                childBullets[i] = child;
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // === 1. 父物体移动（Enemy 风格：世界空间平移） ===
        transform.Translate(moveDirection * moveSpeed * dt, Space.World);

        // === 2. 更新轨道角度 ===
        float dir = clockwise ? -1f : 1f;
        currentBaseAngle += orbitSpeed * Mathf.Deg2Rad * dir * dt;

        // === 3. 半径增长（到达上限后不再增大） ===
        currentRadius = Mathf.Min(currentRadius + radiusGrowthRate * dt, maxRadius);

        // === 4. 更新子物体世界位置（跟随父物体 + 圆周偏移） ===
        Vector2 parentPos = transform.position;
        int aliveCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            GameObject child = childBullets[i];
            if (child == null || !child.activeSelf)
            {
                // 子物体可能已被玩家碰撞或其他原因禁用
                childBullets[i] = null;
                continue;
            }

            aliveCount++;

            float childAngle = currentBaseAngle + angleStep * i * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(childAngle), Mathf.Sin(childAngle)) * currentRadius;
            child.transform.position = parentPos + offset;
        }

        // === 5. 所有子物体都消失 → 禁用自身 ===
        if (disableWhenChildrenGone && aliveCount == 0)
        {
            DisableAll();
            return;
        }

        // === 6. 超时自禁 ===
        elapsedTime += dt;
        if (maxLifetime > 0f && elapsedTime >= maxLifetime)
        {
            DisableAll();
            return;
        }

        // === 7. 父物体出屏检查 ===
        Vector3 pos = transform.position;
        if (pos.x > rightBoundary || pos.x < leftBoundary ||
            pos.y > topBoundary || pos.y < bottomBoundary)
        {
            DisableAll();
        }
    }

    /// <summary>
    /// 禁用所有子子弹并禁用自身
    /// </summary>
    private void DisableAll()
    {
        for (int i = 0; i < childBullets.Length; i++)
        {
            if (childBullets[i] != null)
            {
                childBullets[i].SetActive(false);
                childBullets[i] = null;
            }
        }
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // 确保所有子子弹被回收
        for (int i = 0; i < childBullets.Length; i++)
        {
            if (childBullets[i] != null)
            {
                childBullets[i].SetActive(false);
                childBullets[i] = null;
            }
        }
    }

    // ==================== 公开接口 ====================

    /// <summary>
    /// 由 GoldenElephant 调用，覆盖移动方向 + 伤害 + 移动速度，然后生成子子弹。
    /// </summary>
    public void Initialize(Vector2 direction, int damage, float speed)
    {
        moveDirection = direction.normalized;
        childDamage = damage;
        moveSpeed = speed;
        SpawnChildren();
    }

    /// <summary>
    /// 由 GoldenElephant 调用，同时覆盖移动方向、伤害、移动速度和轨道参数，然后生成子子弹。
    /// </summary>
    public void Initialize(Vector2 direction, int damage, float speed, float radius, float orbitSpd, float growthRate)
    {
        moveDirection = direction.normalized;
        childDamage = damage;
        moveSpeed = speed;
        startRadius = radius;
        currentRadius = radius;
        orbitSpeed = orbitSpd;
        radiusGrowthRate = growthRate;
        SpawnChildren();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // 绘制父物体移动方向
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, moveDirection * 2f);

        // 绘制圆周轨道
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float a0 = (float)i / segments * Mathf.PI * 2f;
            float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
            Vector2 p0 = (Vector2)transform.position + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * currentRadius;
            Vector2 p1 = (Vector2)transform.position + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * currentRadius;
            Gizmos.DrawLine(p0, p1);
        }

        // 绘制圆心
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
#endif
}
