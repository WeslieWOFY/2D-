using UnityEngine;

/// <summary>
/// 子弹变向组件：挂载在有EnemyBullet的对象上，一定时间后改变y轴移动方向
/// 假设子弹初始向左运动
/// </summary>
public class BulletDirectionChanger : MonoBehaviour
{
    [Header("变向设置")]
    [SerializeField] private float changeTime = 1f;             // 多久后变向
    [SerializeField] private float angleOffset = 15f;           // 角度偏移（正值向上，负值向下）

    private EnemyBullet enemyBullet;
    private float timer;
    private bool hasChanged = false;

    private void Awake()
    {
        enemyBullet = GetComponent<EnemyBullet>();
    }

    private void OnEnable()
    {
        timer = 0f;
        hasChanged = false;
    }

    private void Update()
    {
        if (hasChanged || enemyBullet == null) return;

        timer += Time.deltaTime;

        if (timer >= changeTime)
        {
            ChangeDirection();
            hasChanged = true;
        }
    }

    /// <summary>
    /// 改变子弹方向
    /// 假设子弹初始向左（180°），根据角度偏移计算新方向
    /// </summary>
    private void ChangeDirection()
    {
        if (enemyBullet == null) return;

        // 基准角度：向左 = 180°
        float baseAngle = 180f;

        // 计算新角度（正值向上偏移，负值向下偏移）
        float newAngle = baseAngle - angleOffset;

        // 转换为方向向量
        float rad = newAngle * Mathf.Deg2Rad;
        Vector2 newDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        enemyBullet.SetMoveDirection(newDirection);
    }
}
