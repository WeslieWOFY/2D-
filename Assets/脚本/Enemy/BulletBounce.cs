using UnityEngine;

/// <summary>
/// 子弹屏幕反弹组件：挂载到EnemyBullet对象上，碰到屏幕边缘时按入射角=反射角反弹。
/// 不是EnemyBullet的子类，作为额外组件使用。
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
[DefaultExecutionOrder(-50)]  // 在 EnemyBullet.Update 之前运行，预测并反射方向
public class BulletBounce : MonoBehaviour
{
    [Header("反弹设置")]
    [SerializeField] private int maxBounces = 3;           // 最大反弹次数（0 = 不限制）
    [SerializeField] private float boundsOffset = 0.2f;    // 边界偏移（提前量，避免视觉穿帮）

    [Header("反弹特效")]
    [SerializeField] private GameObject bounceEffect;       // 反弹时播放的特效（可选）

    [Header("反弹后速度变化")]
    [SerializeField] private float speedMultiplier = 1f;    // 每次反弹后速度倍率（1 = 不变，<1 = 减速，>1 = 加速）

    private EnemyBullet enemyBullet;
    private int bounceCount;
    private float leftBound;
    private float rightBound;
    private float topBound;
    private float bottomBound;

    private void Awake()
    {
        enemyBullet = GetComponent<EnemyBullet>();
        CalculateBounds();
    }

    private void OnEnable()
    {
        bounceCount = 0;
    }

    private void Update()
    {
        if (enemyBullet == null) return;

        // 预测下一帧的位置
        Vector2 dir = enemyBullet.GetMoveDirection();
        float speed = enemyBullet.GetMoveSpeed();
        Vector3 nextPos = transform.position + (Vector3)(dir * speed * Time.deltaTime);

        bool bounced = false;

        // 检查 X 边界
        if (nextPos.x <= leftBound)
        {
            dir.x = Mathf.Abs(dir.x);   // 强制向右
            bounced = true;
        }
        else if (nextPos.x >= rightBound)
        {
            dir.x = -Mathf.Abs(dir.x);  // 强制向左
            bounced = true;
        }

        // 检查 Y 边界
        if (nextPos.y <= bottomBound)
        {
            dir.y = Mathf.Abs(dir.y);   // 强制向上
            bounced = true;
        }
        else if (nextPos.y >= topBound)
        {
            dir.y = -Mathf.Abs(dir.y);  // 强制向下
            bounced = true;
        }

        if (bounced)
        {
            HandleBounce(dir);
        }
    }

    private void HandleBounce(Vector2 newDir)
    {
        // 检查是否超过最大反弹次数
        if (maxBounces > 0 && bounceCount >= maxBounces)
        {
            gameObject.SetActive(false);
            return;
        }

        bounceCount++;
        enemyBullet.SetMoveDirection(newDir.normalized);

        // 应用速度倍率
        if (speedMultiplier != 1f)
        {
            enemyBullet.SetMoveSpeed(enemyBullet.GetMoveSpeed() * speedMultiplier);
        }

        // 播放反弹特效
        if (bounceEffect != null)
        {
            PoolManager.Release(bounceEffect, transform.position);
        }
    }

    private void CalculateBounds()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));

            leftBound = bottomLeft.x + boundsOffset;
            rightBound = topRight.x - boundsOffset;
            bottomBound = bottomLeft.y + boundsOffset;
            topBound = topRight.y - boundsOffset;
        }
    }
}
