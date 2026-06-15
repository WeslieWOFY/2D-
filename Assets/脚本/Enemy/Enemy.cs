using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public abstract class Enemy : MonoBehaviour
{
    [Header("基础属性")]
    public int maxHP = 100;
    public int currentHP;
    public float moveSpeed = 3f;

    public int scoreValue = 100;
    public float flashDuration = 0.1f;  // 变红持续时间
    
    public int damege = 10;  // 伤害
    
    public int colliderdamege=20;

    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    protected virtual void Awake()
    {
        currentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    protected virtual void Update()
    {
        OnMove();
        OnAttack();
    }

    protected virtual void OnEnable()    
    {
        currentHP = maxHP;
        spriteRenderer.color=originalColor;
    }
    protected virtual void OnDisable()
    {
        StopAllCoroutines(); // 清理所有协程
    }

    // 移动逻辑（子类必须实现）
    public abstract void OnMove();

    // 攻击逻辑（子类必须实现）
    public abstract void OnAttack();

    // 受伤
    public virtual void TakeDamage(int damage)
    {
        if(currentHP-damage>=0)
        currentHP -= damage;
        else
        {
            currentHP = 0;
            OnDeath();
        }
    }

    // 死亡
    protected virtual void OnDeath()
    {
        // 可以在这里播放死亡动画、生成道具等
        gameObject.SetActive(false);
    }

    // 进入屏幕（从屏幕外飞入）
    public virtual void OnEnterScreen() { }

    // 离开屏幕
    protected virtual void OnExitScreen()
    {
        gameObject.SetActive(false);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        
    }
}
