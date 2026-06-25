using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Zhuqingting : Enemy
{
    [Header("竹蜻蜓特有属性")]
    [SerializeField] private GameObject bulletPrefab;       // 子弹预制体
    [SerializeField] private float shootInterval = 3f;      // 射击间隔（秒）
    [SerializeField] private float bulletAngle = 30f;       // 子弹间隔角度
    [SerializeField] private Transform muzzle;              // 发射口位置
    [SerializeField] private float verticalDurationMin = 2f;  // 竖直运动最短时间
    [SerializeField] private float verticalDurationMax = 3f;  // 竖直运动最长时间
    [SerializeField] private float verticalSpeedMultiplier = 1f; // 竖直运动速度倍率

    private Coroutine shootCoroutine;
    private Coroutine switchDirectionCoroutine;
    private Vector2 currentMoveDirection;   // 当前移动方向
    private bool startFromVertical;         // 是否从上下两端刷出（竖直运动阶段）


    protected override void OnEnable()
    {
        base.OnEnable();

        // 根据初始位置判断运动方式
        DetermineInitialDirection();

        // 如果初始是竖直运动，启动延迟切换到水平的协程
        if (startFromVertical)
        {
            if (switchDirectionCoroutine != null)
                StopCoroutine(switchDirectionCoroutine);
            switchDirectionCoroutine = StartCoroutine(SwitchToHorizontalRoutine());
        }

        if (shootCoroutine != null)
            StopCoroutine(shootCoroutine);

        shootCoroutine = StartCoroutine(ShootRoutine());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if (switchDirectionCoroutine != null)
        {
            StopCoroutine(switchDirectionCoroutine);
            switchDirectionCoroutine = null;
        }
    }

    /// <summary>
    /// 根据初始位置判断运动方向：
    /// - 从右侧刷入 → 直接水平向左运动（不竖直）
    /// - 从上方刷入 → 先竖直向下，若干秒后变水平
    /// - 从下方刷入 → 先竖直向上，若干秒后变水平
    /// - 已在屏幕内 → 默认水平向左
    /// </summary>
    private void DetermineInitialDirection()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            startFromVertical = false;
            currentMoveDirection = Vector2.left;
            return;
        }

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);

        // 从右侧刷入（屏幕右侧外部）→ 直接水平运动，不做竖直
        if (viewportPos.x > 1f)
        {
            startFromVertical = false;
            currentMoveDirection = Vector2.left;
        }
        // 从上方刷入 → 先竖直向下
        else if (viewportPos.y > 1f)
        {
            startFromVertical = true;
            currentMoveDirection = Vector2.down;
        }
        // 从下方刷入 → 先竖直向上
        else if (viewportPos.y < 0f)
        {
            startFromVertical = true;
            currentMoveDirection = Vector2.up;
        }
        // 已在屏幕内，默认水平向左
        else
        {
            startFromVertical = false;
            currentMoveDirection = Vector2.left;
        }
    }

    /// <summary>
    /// 随机2-3秒后从竖直运动切换为水平运动
    /// </summary>
    private IEnumerator SwitchToHorizontalRoutine()
    {
        float delay = Random.Range(verticalDurationMin, verticalDurationMax);
        yield return new WaitForSeconds(delay);
        currentMoveDirection = Vector2.left;
        startFromVertical = false;
    }

    private IEnumerator ShootRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(shootInterval);

            if (gameObject.activeSelf && currentHP > 0 && !isDie)
            {
                Shoot360();
            }
        }
    }

    /// <summary>
    /// 以自身为中心朝360°方向发射子弹，每隔bulletAngle度发射一颗
    /// </summary>
    private void Shoot360()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("竹蜻蜓：子弹预制体未设置！");
            return;
        }

        Vector3 shootPos = muzzle != null ? muzzle.position : transform.position;

        for (float angle = 0f; angle < 360f; angle += bulletAngle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject bullet = PoolManager.Release(bulletPrefab, shootPos);
            if (bullet != null)
            {
                EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
                if (eb != null)
                {
                    eb.SetMoveDirection(direction);
                    eb.SetDamage(damege);
                }
            }
        }
    }

    public override void OnMove()
    {
        float speed = moveSpeed;
        // 竖直运动阶段应用速度倍率
        if (startFromVertical)
        {
            speed *= verticalSpeedMultiplier;
        }
        Vector2 movement = speed * Time.deltaTime * currentMoveDirection;
        transform.Translate(movement);
        CheckIfOutOfScreen();
    }

    private void CheckIfOutOfScreen()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
            // 只检测左侧离开屏幕时才禁用（因为初始就是从上下右三侧屏幕外刷入的）
        if (viewportPos.x < -0.2f)
        {
            OnExitScreen();
        }
    }

    public override void OnAttack()
    {
        // 射击由协程处理
    }

    protected override void OnDeath()
    {
        isDie = true;
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if (switchDirectionCoroutine != null)
        {
            StopCoroutine(switchDirectionCoroutine);
            switchDirectionCoroutine = null;
        }
        base.OnDeath();
    }

    protected override void OnExitScreen()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if (switchDirectionCoroutine != null)
        {
            StopCoroutine(switchDirectionCoroutine);
            switchDirectionCoroutine = null;
        }
        base.OnExitScreen();
    }
}
