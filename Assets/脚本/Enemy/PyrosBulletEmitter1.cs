using System.Collections;
using UnityEngine;

/// <summary>
/// 派罗斯子弹发射器1 —— 扇形子弹发射组件（挂在派罗斯子物体上）。
///
/// 发射模式（两批发射）：
///   批1：在发射点 A 一次性发射 7 发扇形分布的子弹（同批同时射出）
///   批2：然后发射 6 发扇形范围的子弹（同批同时射出），与批1分两批，两批之间有间隔
/// </summary>
public class PyrosBulletEmitter1 : MonoBehaviour
{
    [Header("发射点A")]
    [SerializeField] private Transform firePointA;               // 发射点（留空则使用自身位置）

    [Header("轮次1：7发扇形")]
    [SerializeField] private GameObject round1BulletPrefab;      // 轮次1子弹预制体（挂 EnemyBullet）
    [SerializeField] private float round1Speed = 5f;             // 轮次1子弹速度
    [SerializeField] private int round1Damage = 10;              // 轮次1子弹伤害
    [SerializeField] private int round1Count = 7;                // 轮次1子弹数
    [SerializeField] private float round1SpreadAngle = 60f;      // 轮次1扇形总角度（度）
    [Tooltip("轮次1子弹Z轴旋转修正角度（度）：子弹最终Z轴旋转 = 发射方向角度 + 修正角度。素材默认朝右为0，朝上为-90，朝下为+90，按素材实际方向调")]
    [SerializeField] private float round1RotationOffset = 0f;    // 轮次1子弹Z轴旋转修正角度

    [Header("轮次2：6发扇形（穿插在轮次1间隔中）")]
    [SerializeField] private GameObject round2BulletPrefab;      // 轮次2子弹预制体（挂 EnemyBullet）
    [SerializeField] private float round2Speed = 5f;             // 轮次2子弹速度
    [SerializeField] private int round2Damage = 10;              // 轮次2子弹伤害
    [SerializeField] private int round2Count = 6;                // 轮次2子弹数
    [SerializeField] private float round2SpreadAngle = 60f;      // 轮次2扇形总角度（度）

    [Header("扇形中心基准方向")]
    [SerializeField] private Vector2 baseDirection = Vector2.left; // 扇形中心基准方向

    [Header("节奏")]
    [SerializeField] private float initialDelay = 0.5f;          // 初始间隔：激活后首次发射前的等待时间（秒）
    [SerializeField] private float batchInterval = 0.15f;        // 批次间隔：批1(7发)与批2(6发)之间的间隔（秒）
    [SerializeField] private float attackInterval = 3f;          // 整轮攻击结束后的间隔（秒）

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

    /// <summary>
    /// 攻击主循环：初始间隔后分批发射（批1=7发、批2=6发），整轮结束后等待 attackInterval 再来一轮
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        // 先等一帧，确保 PoolManager.Awake 已执行（否则 OnEnable 阶段静态字典未初始化会崩）
        yield return null;

        // 初始间隔：激活后先等一段时间再首次发射
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            yield return StartCoroutine(FireBatchesRoutine());
            yield return new WaitForSeconds(attackInterval);
        }
    }

    /// <summary>
    /// 批次发射：批1 一次性发射 7 发扇形，间隔 batchInterval 后，批2 一次性发射 6 发扇形。
    /// </summary>
    private IEnumerator FireBatchesRoutine()
    {
        if (firePointA == null || round1BulletPrefab == null || round2BulletPrefab == null)
        {
            Debug.LogWarning("[PyrosBulletEmitter1] firePointA 或轮次子弹预制体未指定！");
            yield break;
        }

        // 批1：一次性发射 7 发扇形
        float[] anglesA = ComputeFanAngles(round1Count, round1SpreadAngle);
        for (int i = 0; i < round1Count; i++)
        {
            FireSingle(round1BulletPrefab, round1Speed, round1Damage, anglesA[i], round1RotationOffset);
        }

        // 批次间隔
        yield return new WaitForSeconds(batchInterval);

        // 批2：一次性发射 6 发扇形
        float[] anglesB = ComputeFanAngles(round2Count, round2SpreadAngle);
        for (int i = 0; i < round2Count; i++)
        {
            FireSingle(round2BulletPrefab, round2Speed, round2Damage, anglesB[i], 0f);
        }
    }

    /// <summary>
    /// 发射单颗子弹，方向为扇形序列中的指定角度。
    /// 子弹Z轴旋转 = 发射方向角度 + 修正角度（轮次1用 round1RotationOffset，轮次2不修正传0）。
    /// </summary>
    private void FireSingle(GameObject prefab, float speed, int damage, float angle, float rotationOffset)
    {
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        // 子弹Z轴旋转 = 发射方向角度 + 修正角度
        Quaternion rot = Quaternion.Euler(0f, 0f, angle + rotationOffset);

        GameObject bullet = PoolManager.Release(prefab, firePointA.position, rot);
        if (bullet == null) return;

        var eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(damage);
            eb.SetMoveDirection(dir);
            eb.SetMoveSpeed(speed);
        }
    }

    /// <summary>
    /// 计算扇形角度序列：以 baseDirection 为中心，在 [-spreadAngle/2, +spreadAngle/2] 内均匀分布 count 个角度
    /// </summary>
    private float[] ComputeFanAngles(int count, float spreadAngle)
    {
        float[] angles = new float[count];
        if (count <= 1)
        {
            angles[0] = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            return angles;
        }

        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - spreadAngle / 2f;
        float step = spreadAngle / (count - 1);

        for (int i = 0; i < count; i++)
            angles[i] = startAngle + step * i;

        return angles;
    }
}
