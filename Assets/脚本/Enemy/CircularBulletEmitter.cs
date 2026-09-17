using System.Collections;
using UnityEngine;

/// <summary>
/// 圆周子弹发射器：周期性地发射一组共享圆心、均匀分布角度的圆周运动子弹。
/// 挂载到敌人物体上，自动按间隔发射。
///
/// 子弹实际运动轨迹由 BulletCircularMotion 组件控制，
/// 发射器只负责：确定子弹生成位置 + 调用 Initialize 统一圆心和角度。
///
/// 效果：N 颗子弹围绕同一个圆心等角度分布，各自沿圆周旋转，半径可随时间扩大。
/// </summary>
public class CircularBulletEmitter : MonoBehaviour
{
    [Header("子弹预制体")]
    [SerializeField] private GameObject bulletPrefab;           // 必须挂载 BulletCircularMotion 组件
    [SerializeField] private Transform muzzlePoint;             // 发射口（留空 = 自身位置）
    [SerializeField] private int damage = 10;                   // 子弹伤害

    [Header("发射数量")]
    [SerializeField] private int bulletCount = 6;               // 每组子弹数量
    [SerializeField] private float startAngleOffset = 0f;       // 起始角度偏移（度），用于旋转整组子弹

    [Header("发射节奏")]
    [SerializeField] private float initialDelay = 0f;           // 首次发射延迟（秒）
    [SerializeField] private float fireInterval = 3f;           // 发射间隔（秒）

    private Coroutine fireCoroutine;
    private WaitForSeconds waitInterval;
    private Transform emitPoint;

    private void Awake()
    {
        emitPoint = muzzlePoint != null ? muzzlePoint : transform;
        waitInterval = new WaitForSeconds(fireInterval);
    }

    private void OnEnable()
    {
        if (fireCoroutine != null)
            StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine());
    }

    private void OnDisable()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }
    }

    private IEnumerator FireRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            FireGroup();
            yield return waitInterval;
        }
    }

    /// <summary>
    /// 发射一组圆周运动子弹。由协程自动调用，也可外部主动调用。
    /// </summary>
    public void FireGroup()
    {
        if (bulletPrefab == null) return;

        Vector2 center = emitPoint.position;
        float angleStep = 360f / bulletCount;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = startAngleOffset + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;

            // 子弹生成在圆周上
            Vector2 spawnOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 spawnPos = center + spawnOffset;  // 先用默认方向算位置，BulletCircularMotion 会接管

            // 子弹朝向：精灵默认朝左(Vector2.left)，这里让视觉对准切线方向
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle + 90f);

            GameObject bullet = PoolManager.Release(bulletPrefab, spawnPos, rotation);
            if (bullet == null) continue;

            // 设置伤害
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(damage);
            }

            // 关键：覆盖圆周运动圆心和起始角度
            BulletCircularMotion circularMotion = bullet.GetComponent<BulletCircularMotion>();
            if (circularMotion != null)
            {
                circularMotion.Initialize(center, angle);
            }
        }
    }
}
