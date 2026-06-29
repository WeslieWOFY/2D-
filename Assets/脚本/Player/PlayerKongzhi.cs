using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerKongzhi : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private  float moveSpeed = 5f;
    [SerializeField] private  float knockbackDuration = 0.15f; // 弹开时间
    [SerializeField] private  float knockbackForceMultiplier = 1.2f; // 弹开力度倍数（撞敌人）
    [SerializeField] private  float bulletKnockbackMultiplier = 0.5f; // 弹开力度倍数（撞子弹，比撞敌人小）
    private Vector2 moveInput;
    private float knockbackEnd;
    private GameObject lastHitBy; // 上一次碰撞的对象，弹开期间同一对象不重复扣血
    
    [Header("边界设置")]
    [SerializeField] private bool useScreenBounds = true;
    [SerializeField] private float padding = 0.5f; // 边界留白
    [SerializeField] private float leftover = 0.5f; // 边界留白
    [SerializeField] private float rightover = 0.5f; // 边界留白
    [SerializeField] private float upover = 0.5f; // 边界留白
    [SerializeField] private float downover = 0.5f; // 边界留白
    [Header("宠物")]
    [SerializeField] private Transform petContainer;  // 拖入PetContainer对象
    [Header("画布")]

    [SerializeField] private Canvas canvas;

    private Image damageOverlay;
    private Camera mainCamera;
    private float leftBound;
    private float rightBound;
    private float bottomBound;
    private float topBound;
    
    private int lastVerticalDirection = 0;
    private Coroutine flashRed=null;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (useScreenBounds)
        {
            CalculateBounds();
        }
        //找被激活的宠物
        UpdateCurrentPet();
        SetupDamageOverlay();
    }
    private void SetupDamageOverlay()
    {
        // 创建全屏遮罩
        GameObject overlayObj = new GameObject("DamageFlash");
        overlayObj.transform.SetParent(canvas.transform);
        overlayObj.transform.SetAsLastSibling();
        
        damageOverlay = overlayObj.AddComponent<Image>();
        damageOverlay.raycastTarget = false;
        
        // 强制设置全屏（更可靠的方法）
        RectTransform rect = damageOverlay.rectTransform;
        
        // 方法1：设置锚点为全屏拉伸
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;   // 左下角偏移为0
        rect.offsetMax = Vector2.zero;   // 右上角偏移为0
        rect.pivot = new Vector2(0.5f, 0.5f);
        
        // 强制设置大小和位置
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        
        // 初始透明
        damageOverlay.color = new Color(1f, 0.5f, 0.5f, 0f);
    }
    private petbase currentPet;
    private void UpdateCurrentPet()
    {
        if (petContainer == null) return;

        foreach (Transform child in petContainer)
        {
            if (child.gameObject.activeSelf)
            {
                currentPet = child.GetComponent<petbase>();
                return;
            }
        }

        currentPet = null;
    }
    private void Update()
    {
        // 弹开期间不覆盖 velocity，让物理自然分离
        if (Time.time >= knockbackEnd)
        {
            rb.velocity = moveInput * moveSpeed;
        }

        // 限制边界
        if (useScreenBounds)
        {
            ClampPosition();
        }

        // 判断竖直方向并切换动画（仅方向改变时触发）
        int currentVerticalDirection = 0;
        if (moveInput.y > 0)
            currentVerticalDirection = 1;   // 上
        else if (moveInput.y < 0)
            currentVerticalDirection = -1;  // 下

        if (currentVerticalDirection != lastVerticalDirection)
        {
            switch (currentVerticalDirection)
            {
                case 1:
                    SwitchToUpAnimation();
                    break;
                case -1:
                    SwitchToDownAnimation();
                    break;
                case 0:
                    SwitchToIdleAnimation();
                    break;
            }
            lastVerticalDirection = currentVerticalDirection;
        }
    }
    private void SwitchToUpAnimation()
    {
        // TODO: 实现向上动画切换
    }
    private void SwitchToDownAnimation()
    {
        // TODO: 实现向下动画切换
    }
    private void SwitchToIdleAnimation()
    {
        // TODO: 实现默认/待机动画切换
    }
    // 移动输入回调（通过新输入系统调用）
    public void Move(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }
    
    // 计算屏幕边界（相机固定不动）
    private void CalculateBounds()
    {
        if (mainCamera == null) return;
        
        // 获取屏幕四个角的世界坐标（相机不移动，所以边界是固定的）
        Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
        Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
        
        // 应用留白
        leftBound = bottomLeft.x + padding;
        rightBound = topRight.x - padding;
        bottomBound = bottomLeft.y + padding;
        topBound = topRight.y - padding;
        
        // 调试输出，检查边界是否正确
    }
    
    // 限制位置在边界内
    private void ClampPosition()
    {
        Vector2 pos = rb.position;
        pos.x = Mathf.Clamp(pos.x, leftBound-leftover, rightBound-rightover);
        pos.y = Mathf.Clamp(pos.y, bottomBound-downover, topBound-upover);
        rb.position = pos;
    }
    
    // 可视化边界（在Scene视图中显示）
    private void OnDrawGizmosSelected()
    {
        if (!useScreenBounds || mainCamera == null) return;
        
        if (!Application.isPlaying)
        {
            // 编辑模式预览
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
            leftBound = bottomLeft.x + padding;
            rightBound = topRight.x - padding;
            bottomBound = bottomLeft.y + padding;
            topBound = topRight.y - padding;
        }
        
        Gizmos.color = Color.green;
        Vector3 center = new Vector3(
            (leftBound + rightBound) / 2,
            (bottomBound + topBound) / 2,
            0
        );
        Vector3 size = new Vector3(
            rightBound - leftBound,
            topBound - bottomBound,
            0
        );
        Gizmos.DrawWireCube(center, size);
    }

    public void SHOOT(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            currentPet?.NormalAttack();
        }
        else if (ctx.canceled)
            currentPet?.StopNormalAttack();
    }
    public void Xuli(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            //int cost = currentPet != null ? currentPet.manaCost : 0;
            /*if (!GameManager.Instance.HasEnoughMana(cost))
            {
                Debug.Log("蓝量不足，无法蓄力");
                return;
            }
            Debug.Log("蓝量充足，开始蓄力");*/
            currentPet?.StartCharge();
        }
        else if (ctx.canceled)
        {
            currentPet?.CancelCharge();
        }
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        GameObject obj = other.gameObject;

        if (obj.CompareTag("EmenyBullet"))
        {
            Vector2 pushDir = ((Vector2)transform.position - other.GetContact(0).point).normalized;
            rb.velocity = pushDir * moveSpeed * bulletKnockbackMultiplier;
            ExtendKnockback(Time.time + knockbackDuration);
            ChangeRed();
            EnemyBullet bullet = obj.GetComponent<EnemyBullet>();
            if(bullet!=null)
            GameManager.Instance.PlayerTakeDamage(bullet.GetDamage());
            return;
        }

        if (obj.CompareTag("Enemy"))
        {
            Vector2 pushDir = ((Vector2)transform.position - other.GetContact(0).point).normalized;
            rb.velocity = pushDir * moveSpeed * knockbackForceMultiplier;
            ExtendKnockback(Time.time + knockbackDuration);
        }
    }
    public void ChangeRed()
    {
        if(flashRed != null)
        {
            StopCoroutine(FlashRed());
            if (damageOverlay != null)
                damageOverlay.color = new Color(1f, 0.5f, 0.5f, 0f);
            flashRed = null;
        }
        flashRed = StartCoroutine(FlashRed());
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        Laser laser = other.GetComponent<Laser>();
        if (laser != null)
        {
            ExtendKnockback(Time.time + knockbackDuration);
            FlashRedTrigger();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Laser laser = other.GetComponent<Laser>();
        if (laser == null) return;

        FlashRedTrigger();
    }

    /// <summary>
    /// 设置弹开结束时间，只延长不缩短
    /// </summary>
    private void ExtendKnockback(float newEnd)
    {
        if (newEnd > knockbackEnd)
            knockbackEnd = newEnd;
    }

    /// <summary>
    /// 触发屏幕变红（与子弹击中效果一致）
    /// </summary>
    private void FlashRedTrigger()
    {
        if (flashRed != null)
        {
            StopCoroutine(FlashRed());
            if (damageOverlay != null)
                damageOverlay.color = new Color(1f, 0.5f, 0.5f, 0f);
            flashRed = null;
        }
        flashRed = StartCoroutine(FlashRed());
    }

    IEnumerator FlashRed()
    {
        // 立即变淡红色
        damageOverlay.color = new Color(1f, 0.2f, 0.2f, 0.6f);  // 更红更浓        
        // 等待0.1秒
        yield return new WaitForSeconds(0.1f);
        
        // 淡出效果（可选，更平滑）
        float fadeOut = 0.2f;
        float elapsed = 0;
        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.4f, 0, elapsed / fadeOut);
            damageOverlay.color = new Color(1f, 0.2f, 0.2f, alpha);            
            yield return null;
        }
        
        // 确保完全透明
        damageOverlay.color = new Color(1f, 0.2f, 0.2f, 0);
    }
}
