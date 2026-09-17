using System.Collections;
using UnityEngine;

/// <summary>
/// 派罗斯手臂 —— 上下摆动组件 + 发射子弹。
/// 挂在手臂子物体上，围绕本地 Z 轴在 minAngle ~ maxAngle 之间往返摆动。
/// 同时在发射点以直线方向周期性发射普通子弹。
///
/// 摆动特点：
///   · 激活即开始摆动，无初始间隔
///   · 启动时从当前本地角度出发，先移向最近边界，角度不突变
///   · minAngle / maxAngle 为本地 Z 轴角度上下界
/// </summary>
public class PyrosArm : MonoBehaviour
{
    [Header("摆动角度")]
    [SerializeField] private float minAngle = -30f;   // 最小角度（本地 Z 轴）
    [SerializeField] private float maxAngle = 30f;    // 最大角度（本地 Z 轴）
    [SerializeField] private float swingSpeed = 60f;  // 摆动速度（度/秒）

    [Header("发射子弹")]
    [SerializeField] private Transform firePoint;             // 发射点（留空则使用自身位置）
    [SerializeField] private GameObject bulletPrefab;        // 子弹预制体（挂 EnemyBullet）
    [SerializeField] private float bulletSpeed = 5f;         // 子弹速度
    [SerializeField] private int bulletDamage = 10;          // 子弹伤害
    [SerializeField] private Vector2 fireDirection = Vector2.left; // 直线发射方向
    [SerializeField] private float fireInterval = 1f;        // 发射间隔（秒）
    [SerializeField] private float fireInitialDelay = 0f;    // 初始发射延迟（秒）

    private float currentAngle;     // 当前摆动角度
    private float targetAngle;      // 当前目标角度
    private int direction;          // 1=向 maxAngle，-1=向 minAngle

    private Coroutine fireCoroutine;

    private void OnEnable()
    {
        // 从当前本地角度开始，不突变
        currentAngle = NormalizeAngle(transform.localEulerAngles.z);

        // 选最近的边界作为起始目标，保证启动时平滑过渡
        float distToMax = Mathf.Abs(NormalizeAngle(currentAngle - maxAngle));
        float distToMin = Mathf.Abs(NormalizeAngle(currentAngle - minAngle));
        targetAngle = distToMax < distToMin ? maxAngle : minAngle;
        direction = targetAngle == maxAngle ? 1 : -1;

        // 启动发射循环
        StartFire();
    }

    private void OnDisable()
    {
        StopFire();
    }

    private void Update()
    {
        // 平滑移向当前目标
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);

        // 到达目标后反向摆向另一边
        if (Mathf.Approximately(currentAngle, targetAngle))
        {
            direction = -direction;
            targetAngle = direction > 0 ? maxAngle : minAngle;
        }

        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    // ==================== 发射 ====================

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

    /// <summary>发射循环：初始延迟后，按 fireInterval 周期性发射直线子弹</summary>
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

    /// <summary>发射单颗直线子弹</summary>
    private void Fire()
    {
        Transform emit = firePoint != null ? firePoint : transform;

        GameObject bullet = PoolManager.Release(bulletPrefab, emit.position, Quaternion.identity);
        if (bullet == null) return;

        var eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(fireDirection);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    /// <summary>把角度归一到 [-180, 180]，用于比较到两个边界的距离</summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
