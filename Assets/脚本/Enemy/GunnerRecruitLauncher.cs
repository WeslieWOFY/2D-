using System.Collections;
using UnityEngine;

/// <summary>
/// 枪弹新兵发射台（纯 MonoBehaviour，不继承任何类）。
///
/// 若干个发射点同时发射子弹，所有发射点共用一个对准点。
/// 子弹方向 = 对准点 → 发射位置。按 fireInterval 周期性自动发射。
/// </summary>
public class GunnerRecruitLauncher : MonoBehaviour
{
    [Header("发射点")]
    [SerializeField] private Transform[] firePoints;       // 若干个发射点

    [Header("对准点")]
    [SerializeField] private Transform aimPoint;            // 所有发射点共用的对准点

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;       // 子弹预制体
    [SerializeField] private float bulletSpeed = 5f;        // 子弹速度
    [SerializeField] private int bulletDamage = 10;         // 子弹伤害

    [Header("发射节奏")]
    [SerializeField] private float startDelay = 1f;         // 首次发射前的等待时间
    [SerializeField] private float fireInterval = 2f;       // 每轮发射间隔

    private Coroutine shootCoroutine;

    private void OnEnable()
    {
        if (shootCoroutine != null)
            StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(FireLoop());
    }

    private void OnDisable()
    {
        StopAttack();
    }

    /// <summary>
    /// 发射协程：等待 startDelay 后，每隔 fireInterval 发射一轮
    /// </summary>
    private IEnumerator FireLoop()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            FireAll();

            if (fireInterval > 0f)
                yield return new WaitForSeconds(fireInterval);
            else
                yield return null;
        }
    }

    /// <summary>
    /// 所有发射点同时发射：每个发射点生成一颗子弹，方向 = 对准点→发射点
    /// </summary>
    private void FireAll()
    {
        if (bulletPrefab == null || firePoints == null || aimPoint == null) return;

        for (int i = 0; i < firePoints.Length; i++)
        {
            if (firePoints[i] != null)
                FireOneBullet(firePoints[i]);
        }
    }

    /// <summary>
    /// 从单个发射点发射一颗子弹
    /// </summary>
    private void FireOneBullet(Transform firePoint)
    {
        // 子弹方向 = 对准点 → 发射位置
        Vector2 dir = firePoint.position - aimPoint.position;
        if (dir.sqrMagnitude > 0.0001f)
            dir = dir.normalized;
        else
            dir = Vector2.left;

        // 精灵默认朝左，+180° 使其朝向运动方向
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg + 180f);

        GameObject bullet = PoolManager.Release(bulletPrefab, firePoint.position, rot);
        if (bullet == null) return;

        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(dir);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    /// <summary>
    /// 停止攻击协程（由父物体撤退时调用）
    /// </summary>
    public void StopAttack()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
    }
}
