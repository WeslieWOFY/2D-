using System.Collections;
using UnityEngine;

/// <summary>
/// 炮塔组件：从发射点A向远离参考点B的方向发射子弹。
/// 子弹方向 = (A.position - B.position).normalized（即从B到A的方向）。
/// 可独立使用，也可挂载到 Enemy 子类上配合工作。
/// </summary>
public class Turret : MonoBehaviour
{
    [Header("发射点")]
    [SerializeField] private Transform firePoint;        // A：子弹生成位置

    [Header("参考点")]
    [SerializeField] private Transform referencePoint;   // B：方向参考点，子弹朝远离B的方向发射

    [Header("子弹设置")]
    [SerializeField] private GameObject[] bulletPrefabs;  // 子弹预制体（多个则每次随机选一个）
    [SerializeField] private int bulletDamage = 10;       // 子弹伤害

    [Header("射击设置")]
    [SerializeField] private float fireInterval = 1.5f;  // 射击间隔（秒）
    [SerializeField] private float fireDelay = 0f;       // 首次射击延迟（秒，0 = 立即射击）

    [Header("启用控制")]
    [SerializeField] private bool autoFire = true;       // 激活后是否自动开始射击

    private Coroutine fireCoroutine;
    private bool isFiring;

    private void Start()
    {
        if (firePoint == null)
            firePoint = transform;
    }

    private void OnEnable()
    {
        if (autoFire)
            StartFiring();
    }

    private void OnDisable()
    {
        StopFiring();
    }

    /// <summary>
    /// 开始射击（可由外部调用）
    /// </summary>
    public void StartFiring()
    {
        if (isFiring) return;
        isFiring = true;

        if (fireCoroutine != null) StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine());
    }

    /// <summary>
    /// 停止射击（可由外部调用）
    /// </summary>
    public void StopFiring()
    {
        isFiring = false;
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }
    }

    /// <summary>
    /// 立即发射一发子弹（可由外部调用，不受间隔限制）
    /// </summary>
    public void FireOnce()
    {
        Fire();
    }

    private IEnumerator FireRoutine()
    {
        // 首次延迟
        if (fireDelay > 0f)
            yield return new WaitForSeconds(fireDelay);

        while (true)
        {
            if (gameObject.activeSelf)
                Fire();

            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void Fire()
    {
        if (bulletPrefabs == null || bulletPrefabs.Length == 0) return;
        if (firePoint == null || referencePoint == null) return;

        // 随机选一个子弹预制体
        GameObject prefab = bulletPrefabs[Random.Range(0, bulletPrefabs.Length)];

        // 方向：从 B 到 A（子弹远离参考点）
        Vector2 dir = (firePoint.position - referencePoint.position).normalized;

        // 旋转：精灵默认朝左，用 SignedAngle 计算旋转角度
        float angle = Vector2.SignedAngle(Vector2.left, dir);
        Quaternion rot = Quaternion.Euler(0, 0, angle);

        GameObject bullet = PoolManager.Release(prefab, firePoint.position, rot);
        if (bullet != null)
        {
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(bulletDamage);
                eb.SetMoveDirection(dir);
            }
        }
    }

    /// <summary>
    /// 在编辑器中绘制发射方向和参考线
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (firePoint == null || referencePoint == null) return;

        // 发射方向（从B到A的延长线）
        Gizmos.color = Color.red;
        Gizmos.DrawLine(referencePoint.position, firePoint.position);
        Vector3 dir = (firePoint.position - referencePoint.position).normalized;
        Gizmos.DrawRay(firePoint.position, dir * 2f);

        // 发射点
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(firePoint.position, 0.15f);
        // 参考点
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(referencePoint.position, 0.1f);
    }
}
