using System.Collections;
using UnityEngine;

/// <summary>
/// 通用炮台发射器（不继承 Enemy）。
/// 两种攻击模式共享 Fire()，概率触发：
///   1. 单发 A 子弹
///   2. 连发扇形 B 子弹（多轮）
/// 挂载到任意 GameObject 上，OnEnable 自动开始攻击。
/// </summary>
public class CannonEmitter : MonoBehaviour
{
    [Header("发射点")]
    [SerializeField] private Transform firePoint;

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefabA;          // A子弹：单发
    [SerializeField] private GameObject bulletPrefabB;          // B子弹：扇形连发
    [SerializeField] private int bulletDamageA = 10;
    [SerializeField] private int bulletDamageB = 10;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private Vector2 bulletDirection = Vector2.left; // 基准发射方向

    [Header("攻击节奏")]
    [SerializeField] private float initialDelay = 1f;           // 首次攻击延迟
    [SerializeField] private float attackInterval = 2f;         // 攻击间隔

    [Header("扇形连发（B子弹）")]
    [SerializeField] private int fanBulletCount = 6;            // 每轮子弹数
    [SerializeField] private int fanRounds = 3;                 // 发射轮数
    [SerializeField] private float fanRoundInterval = 0.15f;    // 轮间隔
    [SerializeField] private float fanSpreadAngle = 45f;        // 扇形总角度（度）
    [SerializeField] [Range(0f, 1f)] private float fanProbability = 0.4f; // 扇形触发概率

    private Coroutine attackCoroutine;

    private void OnEnable()
    {
        StartAttack();
    }

    private void OnDisable()
    {
        StopAttack();
    }

    private void StartAttack()
    {
        StopAttack();
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private void StopAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }

    private IEnumerator AttackRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            Fire();
            yield return new WaitForSeconds(attackInterval);
        }
    }

    /// <summary>
    /// 共享攻击入口：概率选择单发或扇形连发
    /// </summary>
    public void Fire()
    {
        if (firePoint == null) return;

        // 概率触发扇形连发
        if (bulletPrefabB != null && Random.value < fanProbability)
        {
            StartCoroutine(FireFanRoutine());
        }
        else if (bulletPrefabA != null)
        {
            FireSingle(bulletPrefabA, bulletDirection, bulletDamageA);
        }
    }

    /// <summary>
    /// 发射单颗子弹
    /// </summary>
    private void FireSingle(GameObject prefab, Vector2 direction, int damage)
    {
        if (prefab == null || firePoint == null) return;

        GameObject bullet = PoolManager.Release(prefab, firePoint.position, Quaternion.identity);
        if (bullet == null) return;

        var eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(damage);
            eb.SetMoveDirection(direction);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    /// <summary>
    /// 扇形连发协程：多轮，每轮多颗 B 子弹
    /// </summary>
    private IEnumerator FireFanRoutine()
    {
        for (int round = 0; round < fanRounds; round++)
        {
            FireFanRound();
            yield return new WaitForSeconds(fanRoundInterval);
        }
    }

    /// <summary>
    /// 发射一轮扇形子弹：以 bulletDirection 为中心均匀分布
    /// </summary>
    private void FireFanRound()
    {
        if (bulletPrefabB == null || firePoint == null) return;

        float baseAngle = Mathf.Atan2(bulletDirection.y, bulletDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle / 2f;
        float angleStep = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            FireSingle(bulletPrefabB, dir, bulletDamageB);
        }
    }
}
