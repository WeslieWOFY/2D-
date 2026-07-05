using UnityEngine;
using System.Collections;

/// <summary>
/// 跟踪弹发射器：通过事件总线请求玩家位置，向玩家方向发射子弹。
/// 玩家禁用/不存在时，自动回退为水平方向（朝左）。
/// 启用 (OnEnable) 后自动开始按间隔发射，禁用 (OnDisable) 则停止。
/// </summary>
public class HomingBulletEmitter : MonoBehaviour
{
    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;       // 子弹预制体
    [SerializeField] private Transform muzzlePoint;         // 发射口位置（留空=自身Transform）
    [SerializeField] private int damage = 10;               // 伤害
    [SerializeField] private float bulletSpeed = 5f;        // 子弹飞行速度

    [Header("发射节奏")]
    [SerializeField] private float firstFireDelay = 0f;     // 首次发射延迟（秒）
    [SerializeField] private float fireInterval = 3f;       // 发射间隔（秒）

    [Header("回退方向")]
    [SerializeField] private Vector2 fallbackDirection = Vector2.left; // 玩家不可用时的默认发射方向

    private Coroutine fireCoroutine;
    private WaitForSeconds waitInterval;

    private void Awake()
    {
        if (muzzlePoint == null)
            muzzlePoint = transform;

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

    /// <summary>
    /// 发射协程：按间隔循环发射
    /// </summary>
    private IEnumerator FireRoutine()
    {
        if (firstFireDelay > 0f)
            yield return new WaitForSeconds(firstFireDelay);

        while (true)
        {
            Fire();
            yield return waitInterval;
        }
    }

    /// <summary>
    /// 发射一颗跟踪弹。
    /// 通过事件总线请求玩家位置：玩家在线 → 瞄准玩家；玩家禁用/不存在 → 使用 fallbackDirection。
    /// </summary>
    public void Fire()
    {
        if (bulletPrefab == null) return;

        // ---- 事件总线请求玩家位置 ----
        var request = new PlayerPositionRequest();
        LevelEventBus.Trigger("RequestPlayerPosition", request);

        Vector2 aimDirection;
        if (request.HasPosition)
        {
            // 玩家在线：方向 = muzzle → 玩家
            aimDirection = ((Vector2)(request.Position - muzzlePoint.position)).normalized;
        }
        else
        {
            // 玩家禁用或不存在：水平默认方向
            aimDirection = fallbackDirection.normalized;
        }

        // 旋转：精灵默认朝左 (Vector2.left)，让子弹视觉对准瞄准方向
        Quaternion rot = Quaternion.FromToRotation(Vector2.left, aimDirection);

        // 从对象池取出子弹
        GameObject bullet = PoolManager.Release(bulletPrefab, muzzlePoint.position, rot);
        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(damage);
            eb.SetMoveDirection(aimDirection);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }
}
