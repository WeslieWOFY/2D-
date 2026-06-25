using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingSheep : Enemy
{
    [Header("飞绵羊特有属性")]
    [SerializeField] private GameObject bulletPrefab;   // 子弹预制体
    [SerializeField] private float shootInterval = 2.5f; // 射击间隔（秒）
    [SerializeField] private float verticalMoveInterval = 2f; // 垂直移动间隔（秒）
    [SerializeField] private float verticalMoveDistance = 2f;  // 垂直移动距离（上下移动的幅度）
    
    [SerializeField] private Transform muzzle;          // 子弹发射口位置
    [SerializeField] private float bulletDelay = 0.5f;  // 子弹生成延迟（配合动画）

    private Coroutine shootCoroutine;
    private Coroutine verticalMoveRoutine;

    private Animator animator;

    // 垂直移动的目标偏移量（相对初始位置）
    private float verticalTargetOffset;
    private float initialY;
    private bool isMovingVertical = false;

    private enum SheepState
    {
        MovingLeft,           // 持续向左移动
        VerticalAdjusting     // 垂直调整中
    }
    private SheepState currentState = SheepState.MovingLeft;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 记录初始Y坐标
        initialY = transform.position.y;
        verticalTargetOffset = 0f;

        // 开始向左移动状态
        currentState = SheepState.MovingLeft;

        // 启动射击协程
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(ShootRoutine());

        // 启动垂直移动协程
        if(verticalMoveRoutine!=null) StopCoroutine(verticalMoveRoutine);
        verticalMoveRoutine=StartCoroutine(VerticalMoveRoutine());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        StopAllCoroutines();
    }

    // 垂直移动控制协程
    private IEnumerator VerticalMoveRoutine()
    {
        while (true)
        {
            if(isDie)
            break;
            yield return new WaitForSeconds(verticalMoveInterval);

            if (gameObject.activeSelf && currentHP > 0 && !isDie)
            {
                // 获取屏幕边界（世界坐标）
                Camera cam = Camera.main;
                Vector3 screenMin = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
                Vector3 screenMax = cam.ViewportToWorldPoint(new Vector3(1, 1, 0));
                
                // 获取当前屏幕位置
                Vector3 currentScreenPos = cam.WorldToViewportPoint(transform.position);
                
                float targetY;
                
                // 如果在屏幕下半区（y <= 0.5），随机移动到上半区
                if (currentScreenPos.y <= 0.5f)
                {
                    // 随机生成上半区的Y坐标（0.5 到 1 之间，留出边距）
                    float randomViewportY = Random.Range(0.55f, 0.95f);
                    targetY = cam.ViewportToWorldPoint(new Vector3(0, randomViewportY, 0)).y;
                }
                // 如果在屏幕上半区（y > 0.5），随机移动到下半区
                else
                {
                    // 随机生成下半区的Y坐标（0 到 0.5 之间，留出边距）
                    float randomViewportY = Random.Range(0.05f, 0.45f);
                    targetY = cam.ViewportToWorldPoint(new Vector3(0, randomViewportY, 0)).y;
                }
                
                // 开始垂直移动
                currentState = SheepState.VerticalAdjusting;
                yield return StartCoroutine(MoveVerticalTo(targetY));

                // 垂直移动完成，回到向左移动状态
                currentState = SheepState.MovingLeft;
            }
        }
    }

    // 垂直移动到指定的Y坐标
    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    // 或者使用更平滑的三次方缓动
    private float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // 修改 MoveVerticalTo 方法
    private IEnumerator MoveVerticalTo(float targetY)
    {
        Vector2 startPos = transform.position;
        Vector2 targetPos = new Vector2(transform.position.x, targetY);

        float duration = 0.6f; // 可以稍微增加持续时间
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // 使用缓动函数替代线性插值
            float easedT = EaseInOutQuad(t); // 或 EaseInOutCubic(t)
            float newY = Mathf.Lerp(startPos.y, targetY, easedT);
            
            transform.position = new Vector2(transform.position.x, newY);
            yield return null;
        }

        transform.position = new Vector2(transform.position.x, targetY);
    }

        // 射击协程
        private IEnumerator ShootRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(shootInterval);

                if (gameObject.activeSelf && currentHP > 0 && !isDie)
                {
                    Shoot();
                }
            }
    }

    private void Shoot()
    {
        /*if (animator != null)
            animator.SetTrigger("shoot");*/

        StartCoroutine(DelayedShoot(bulletDelay));
    }

    private IEnumerator DelayedShoot(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bulletPrefab != null && muzzle != null)
        {
            GameObject bullet = PoolManager.Release(bulletPrefab, muzzle.position);
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(damege);
            }
        }
    }

    public override void OnMove()
    {
        // 始终向左移动（即使在垂直移动过程中也保持向左）
        Vector2 movement = Vector2.left * moveSpeed * Time.deltaTime;
        transform.Translate(movement);

        // 检查是否超出屏幕（超出则禁用）
        CheckIfOutOfScreen();
    }

    private void CheckIfOutOfScreen()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        if (viewportPos.x < -0.2f || viewportPos.x > 1.2f ||
            viewportPos.y < -0.2f || viewportPos.y > 1.2f)
        {
            OnExitScreen();
        }
    }

    public override void OnAttack()
    {
        // 射击由协程处理，此方法可以为空
    }

    protected override void OnDeath()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if (flashRedCoroutine != null)
        {
            StopCoroutine(flashRedCoroutine);
            flashRedCoroutine = null;
        }
        if(verticalMoveRoutine!=null)
        {
            StopCoroutine(verticalMoveRoutine);
            verticalMoveRoutine=null;
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
        if (flashRedCoroutine != null)
        {
            StopCoroutine(flashRedCoroutine);
            flashRedCoroutine = null;
        }
        if(verticalMoveRoutine!=null)
        {
            StopCoroutine(verticalMoveRoutine);
            verticalMoveRoutine=null;
        }
        base.OnExitScreen();
    }
}
