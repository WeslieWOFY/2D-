using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable] public abstract class Enemy : MonoBehaviour
{
    [Header("基础属性")]
    public int maxHP = 100;
    protected int currentHP;
    public float moveSpeed = 3f;
    public float baozhajiange = 0.15f;
    public WaitForSeconds waitbaozha;
    public int scoreValue = 100;
    public float flashDuration = 0.1f;  // 变红持续时间
    WaitForSeconds FlashDuration;
    public int damege = 10;  // 伤害
    
    public int colliderdamege=20;
    public float damageCooldown = 0.5f;
    protected float lastDamageTime = -1f;
    [SerializeField] protected AudioClip MisSFX;
    protected bool isDie;
    protected Coroutine flashRedCoroutine;

    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    [SerializeField] protected GameObject baozha;
   [SerializeField]  protected Transform[] boomTransform;

    [SerializeField] protected AudioClip baozhaSFX;
    protected Animator animator;

    // ==================== 受击开关 ====================
    [Header("受击开关")]
    [Tooltip("关闭时玩家子弹打上来不扣血、不播受击音效、不闪红。默认为开；" +
             "超级 BOSS 会按形态切换自行开关这个标志")]
    [SerializeField] protected bool hitEnabled = true;
    protected virtual void Awake()
    {
        currentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator=GetComponent<Animator>();
        FlashDuration=new WaitForSeconds(flashDuration);
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        waitbaozha=new WaitForSeconds(baozhajiange/(boomTransform.Length==0?1:boomTransform.Length));
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
        isDie=false;
        lastDamageTime = -1f;
    }
    protected virtual void OnDisable()
    {
        StopAllCoroutines(); // 清理所有协程
    }

    // 移动逻辑（子类必须实现）
    public abstract void OnMove();

    // 攻击逻辑（子类必须实现）
    public abstract void OnAttack();

    /// <summary>受击开关是否打开；关闭时打上来没有任何反应</summary>
    public bool HitEnabled => hitEnabled;

    /// <summary>打开受击开关</summary>
    public virtual void EnableHit() { hitEnabled = true; }

    /// <summary>关闭受击开关</summary>
    public virtual void DisableHit() { hitEnabled = false; }

    // 受伤
    public virtual void TakeDamage(int damage)
    {
        // 受击开关没打开：不扣血、不触发死亡
        if (!hitEnabled) return;

        if(currentHP-damage>=0)
        currentHP -= damage;
        else
        {
            currentHP = 0;
            OnDeath();
        }
    }

    // 死亡
    protected IEnumerator Baozha()
    {
        AudioManager.Instance.PlaySFX(baozhaSFX);
        foreach(Transform transform in boomTransform)
        {
            PoolManager.Release(baozha,transform.position);
            yield return waitbaozha;
        }
        gameObject.SetActive(false);
    }
    protected virtual void OnDeath()
    {
        //if(isDie) return ;
        isDie=true;
        moveSpeed=0;
        if(animator!=null)
        animator.speed = 0;
        // 可以在这里播放死亡动画、生成道具等
        StartCoroutine(Baozha());
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
        if(isDie) return ;
        // 受击开关没打开：不播受击音效、不闪红、不扣血。
        // 注意只管敌人这边的反应；子弹自身撞上后消失是 Bullet 类的事，不受这个开关影响
        if(!hitEnabled) return ;
        if (other.CompareTag("PlayerBullet"))
        {
            AudioManager.Instance.PlaySFX(MisSFX);   
            Bullet bullet = other.GetComponent<Bullet>();
            if (flashRedCoroutine != null)
            StopCoroutine(flashRedCoroutine);

            flashRedCoroutine = StartCoroutine(FlashRed());

            int totalDamage = bullet.damage + GameManager.Instance.attackPower;
            TakeDamage(totalDamage);
        }
    }
    protected IEnumerator FlashRed()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return FlashDuration;
            spriteRenderer.color = originalColor;
        }
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        GameObject obj = other.gameObject;
        if (obj.CompareTag("Player"))
        {
            if (Time.time - lastDamageTime < damageCooldown) return;
            lastDamageTime = Time.time;
            PlayerKongzhi Player=other.gameObject.GetComponent<PlayerKongzhi>();
            Player.ChangeRed();
            GameManager.Instance.PlayerTakeDamage(colliderdamege);
        }
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        GameObject obj = other.gameObject;
        if (obj.CompareTag("Player"))
        {
            if (Time.time - lastDamageTime < damageCooldown) return;
            lastDamageTime = Time.time;
            GameManager.Instance.PlayerTakeDamage(colliderdamege);
        }
    }
    protected virtual void Retreat()
    {
        
    }
}
