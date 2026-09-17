using System.Collections;
using UnityEngine;

/// <summary>
/// 派罗斯武器 —— 发射子弹组件。
/// 唯一功能：在发射点发射普通子弹，发射方向与武器自身旋转角度有关
/// （朝武器本地坐标轴方向发射，武器旋转到哪子弹就飞向哪）。
/// </summary>
public class PyrosWeapon : MonoBehaviour
{
    /// <summary>武器朝向轴：决定本地哪个方向是发射方向</summary>
    public enum AimAxis { Up, Right }

    [Header("发射点")]
    [SerializeField] private Transform firePoint;             // 发射点（留空则使用自身位置）

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;        // 子弹预制体（挂 EnemyBullet）
    [SerializeField] private float bulletSpeed = 5f;         // 子弹速度
    [SerializeField] private int bulletDamage = 10;          // 子弹伤害

    [Header("发射方向")]
    [SerializeField] private AimAxis aimAxis = AimAxis.Up;   // 武器朝向轴：Up=本地Y轴朝上，Right=本地X轴朝右
    [SerializeField] private float aimOffsetAngle = 0f;      // 发射方向角度修正（度，叠加在自身旋转上）

    [Header("节奏")]
    [SerializeField] private float fireInterval = 1f;        // 发射间隔（秒）
    [SerializeField] private float fireInitialDelay = 0f;    // 初始发射延迟（秒）

    private Coroutine fireCoroutine;

    private void OnEnable()
    {
        StartFire();
    }

    private void OnDisable()
    {
        StopFire();
    }

    private void StartFire()
    {
        if (fireCoroutine != null) StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine());
    }

    private void StopFire()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }
    }

    /// <summary>发射循环：初始延迟后，按 fireInterval 周期性发射</summary>
    private IEnumerator FireRoutine()
    {
        if (bulletPrefab == null) yield break;

        // 先等一帧，确保 PoolManager.Awake 已执行（否则 OnEnable 阶段静态字典未初始化会崩）
        yield return null;

        if (fireInitialDelay > 0f)
            yield return new WaitForSeconds(fireInitialDelay);

        while (true)
        {
            Fire();
            yield return new WaitForSeconds(fireInterval);
        }
    }

    /// <summary>发射单颗子弹，方向 = 武器自身旋转方向（本地轴向 + 角度修正）</summary>
    private void Fire()
    {
        Transform emit = firePoint != null ? firePoint : transform;

        // 发射方向 = 本地轴向的世界方向（Up=transform.up，Right=transform.right），再叠加角度修正
        Vector3 axis = aimAxis == AimAxis.Up ? transform.up : transform.right;
        Vector2 dir = Quaternion.Euler(0f, 0f, aimOffsetAngle) * axis;

        GameObject bullet = PoolManager.Release(bulletPrefab, emit.position, Quaternion.identity);
        if (bullet == null) return;

        var eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(dir.normalized);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }
}
