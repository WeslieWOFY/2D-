using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class qiqiu : Enemy
{
    [Header("气球形态")]
    [SerializeField] private Sprite[] formSprites = new Sprite[4]; // 4种形态精灵

    [Header("碰撞冷却")]
    [SerializeField] private float hitCooldown = 0.2f; // 每0.2秒只能受击一次

    [Header("形态切换动画")]
    [SerializeField] private float formSwitchScale = 1.15f;  // 切换时最大放大倍数
    [SerializeField] private float formSwitchDuration = 0.12f; // 切换动画时长
    [SerializeField] private int shengming=4;
    private int currentForm = 0;      // 当前形态 (0~3)
    private int hpLostInForm = 0;     // 当前形态已扣血量
    private float lastHitTime = -1f;  // 上次受击时间
    private Coroutine scaleCoroutine; // 缩放动画协程

    protected override void Awake()
    {
        base.Awake();
        // 根据形态数量设置最大生命值（4形态）
        if (formSprites.Length > 0)
        {
            maxHP = formSprites.Length * shengming;
            currentHP = maxHP;
        }
        UpdateSprite();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        currentForm = 0;
        hpLostInForm = 0;
        lastHitTime = -1f;
        UpdateSprite();
    }

    // ========== 碰撞处理 ==========
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (isDie) return;
        if (other.CompareTag("PlayerBullet"))
        {
            Bullet bullet = other.GetComponent<Bullet>();
            // 冷却检查：0.2秒内只能受击一次
            if (Time.time - lastHitTime < hitCooldown) 
            {
                if (bullet != null && bullet.getfalse())
                {
                    other.gameObject.SetActive(false);
                }
                return;
            }
            lastHitTime = Time.time;
            // 闪红
            if (flashRedCoroutine != null) StopCoroutine(flashRedCoroutine);
            flashRedCoroutine = StartCoroutine(FlashRed());
            // 受击音效
            AudioManager.Instance.PlaySFX(MisSFX, GameManager.volume);
            // 回收子弹
            if (bullet != null && bullet.getfalse())
            {
                other.gameObject.SetActive(false);
            }
            // 强制只扣一滴血
            TakeOneDamage();
        }
    }

    // ========== 扣血 & 形态切换 ==========
    private void TakeOneDamage()
    {
        currentHP--;
        hpLostInForm++;

        if (hpLostInForm >= shengming)
        {
            hpLostInForm = 0;

            if (currentForm < formSprites.Length - 1)
            {
                // 切换到下一形态
                currentForm++;
                UpdateSprite();
                // 膨胀回弹效果
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(FormSwitchEffect());
            }
            else
            {
                // 第四形态扣满3血 → 爆炸
                Explode();
            }
        }
    }

    // 更新精灵图（不借助动画机）
    private void UpdateSprite()
    {
        if (formSprites != null
            && currentForm < formSprites.Length
            && formSprites[currentForm] != null
            && spriteRenderer != null)
        {
            spriteRenderer.sprite = formSprites[currentForm];
        }
    }

    // 形态切换时的膨胀回弹效果（碰撞体随 transform 等比缩放）
    private IEnumerator FormSwitchEffect()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * formSwitchScale;
        float halfDuration = formSwitchDuration * 0.5f;
        float elapsed = 0f;

        // 第一阶段：放大
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        // 第二阶段：缩回原大小
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    // ========== 爆炸 ==========
    private void Explode()
    {
        isDie = true;
        moveSpeed = 0f;
        StartCoroutine(Baozha()); // 使用基类 Baozha() 协程
    }

    // ========== 移动 ==========
    public override void OnMove()
    {
        transform.Translate(Vector2.left * moveSpeed * Time.deltaTime);

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        if (vp.x < -0.2f || vp.x > 1.2f || vp.y < -0.2f || vp.y > 1.2f)
        {
            OnExitScreen();
        }
    }

    // ========== 攻击（气球不主动攻击） ==========
    public override void OnAttack()
    {
        // 气球类敌人不发射子弹，仅通过碰撞或存在本身构成威胁
    }
}
