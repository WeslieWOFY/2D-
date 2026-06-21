using System.Collections;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEngine;

public class jixietunshu : Enemy
{
    [Header("机械豚鼠特有属性")]
    [SerializeField] private  GameObject bulletPrefab; // 子弹预制体
    [SerializeField] private float shootInterval = 3f; // 射击间隔（秒）
    
    //[SerializeField] private float initialRandomMoveDuration = 1.5f; // 初始随机移动持续时间
    [SerializeField] private float initialRandomSpeed = 2f; // 初始随机移动速度

    
    [SerializeField] private  Vector2 moveDirection; // 移动方向
    [SerializeField] private Transform muzz;
    //public bool isshoot;

    //private bool isMovingLeft = false; // 是否已经开始向左移动
    private Coroutine shootCoroutine; // 射击协程引用
    
    private Coroutine flashRed; // 射击协程引用

    private Camera mainCamera; // 主摄像机引用
    private Animator animator;
    private enum EnemyState
    {
        InitialRandomMove, // 初始随机移动阶段
        MovingLeft         // 向左移动阶段
    }
    
    private EnemyState currentState = EnemyState.InitialRandomMove;
    
    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main;
        //isshoot=false;
        shootCoroutine=null;
        animator = GetComponent<Animator>();
    }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        isDie=false;
        // 重置状态
        currentState = EnemyState.InitialRandomMove;
        //isMovingLeft = false;
        
        // 开始初始随机移动
        StartInitialRandomMove();
        
        if (shootCoroutine != null)
        {
        StopCoroutine(shootCoroutine);
        }
    
    // 启动新的协程
        shootCoroutine = StartCoroutine(ShootRoutine());
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        
        // 停止射击协程
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
    }
    
    private void StartInitialRandomMove()
    {
        // 获取屏幕边界（世界坐标）
        Vector3 screenMin = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 screenMax = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
        
        // 生成屏幕内的随机目标位置
        Vector2 targetPosition = new Vector2(
            Random.Range(screenMin.x + 5f, screenMax.x - 2f),  // 避免贴边，留点边距
            Random.Range(screenMin.y + 2f, screenMax.y - 2f)
        );

        // 开始移动到随机位置
        StartCoroutine(MoveToRandomPositionAndThenLeft(targetPosition));
    }
    private IEnumerator MoveToRandomPositionAndThenLeft(Vector2 targetPosition)
    {
        // 计算方向
        Vector2 startPosition = transform.position;
        moveDirection = (targetPosition - startPosition).normalized;
        
        // 持续移动到目标位置
        while (Vector2.Distance(transform.position, targetPosition) > 0.2f)
        {
            // 每帧移动，由 OnMove 负责移动逻辑
            yield return null;
        }
        
        // 到达目标位置后，切换到向左移动
        currentState = EnemyState.MovingLeft;
        moveDirection = Vector2.left;
    }
    /*private IEnumerator SwitchToLeftMoveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentState = EnemyState.MovingLeft;
        //isMovingLeft = true;
        moveDirection = Vector2.left;
    }*/
    
    private IEnumerator ShootRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(shootInterval);
            
            // 只有在启用状态且未死亡时才射击
            if (gameObject.activeSelf && currentHP > 0)
            {
                animator.SetTrigger("shoot");
            }
        }
    }
    /*private IEnumerator DelayedShoot(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject bullet=PoolManager.Release(bulletPrefab, muzz.position);
        bullet.GetComponent<EnemyBullet>().SetDamage(damege);
    }*/
    void Bullet()
    {
        if (bulletPrefab != null)
        {
            // 获取子弹组件并设置方向（向左射击）
            GameObject bullet=PoolManager.Release(bulletPrefab, muzz.position);
            EnemyBullet enemyBullet = bullet.GetComponent<EnemyBullet>();
            enemyBullet.SetDamage(damege);
            // 可选：添加音效、特效等
            // AudioManager.PlaySound(shootSound);
        }
    }
    
    public override void OnMove()
    {
        Vector2 movement;
        if(currentState == EnemyState.InitialRandomMove)
        {
            movement=moveDirection * initialRandomSpeed * Time.deltaTime;
        }
        else
        {
            movement=moveDirection * moveSpeed * Time.deltaTime; 
        }
        transform.Translate(movement);
        if(currentState == EnemyState.MovingLeft)
        {
            CheckIfOutOfScreen();
        }
    }
    
    private void CheckIfOutOfScreen()
    {
        if (mainCamera == null) return;
        
        Vector3 viewportPos = mainCamera.WorldToViewportPoint(transform.position);
        
        // 检查是否完全离开屏幕（左侧边缘）
        // 可以调整阈值，让敌人完全离开屏幕后再禁用
        if (viewportPos.x < -0.2f || viewportPos.x > 1.2f || 
            viewportPos.y < -0.2f || viewportPos.y > 1.2f)
        {
            OnExitScreen();
        }
    }
    
    public override void OnAttack()
    {
        // 机械豚鼠的攻击逻辑在独立的射击协程中处理
        // 这个方法可以留空，或者用于处理碰撞伤害
    }
    
    protected override void OnDeath()
    {
        isDie=true;
        // 停止射击
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if(flashRed!=null)
        {
            StopCoroutine(flashRed);
            flashRed=null;
        }
        // 可以添加死亡特效
        // 播放死亡动画或粒子效果
        // 添加分数（可以通过游戏管理器）
        // ScoreManager.Instance.AddScore(scoreValue);
        
        // 调用基类的死亡逻辑
        base.OnDeath();
    }
    
    protected override void OnExitScreen()
    {
        // 停止射击协程
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
        if(flashRed!=null)
        {
            StopCoroutine(flashRed);
            flashRed=null;
        }
        // 调用基类的离开屏幕逻辑
        base.OnExitScreen();
    }
    
    /*IEnumerator FlashRed()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = originalColor;
        }
    }*/
}
