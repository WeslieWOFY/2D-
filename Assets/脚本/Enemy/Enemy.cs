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
    
    public int damege = 10;  // 伤害
    
    public int colliderdamege=20;
    [SerializeField] protected AudioClip MisSFX;
    protected bool isDie;
    protected Coroutine flashRedCoroutine;

    protected SpriteRenderer spriteRenderer;
    protected Color originalColor;
    [SerializeField] protected GameObject baozha;
   [SerializeField]  protected Transform[] boomTransform;

    [SerializeField] protected AudioClip baozhaSFX;
    [SerializeField] protected int temp=3;
    protected virtual void Awake()
    {
        currentHP = maxHP;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        waitbaozha=new WaitForSeconds(baozhajiange);
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
    protected IEnumerator Baozha()
    {
        int t=0;
        int cnt=0;
        foreach(Transform transform in boomTransform)
        {
            if(t%temp==0)
            {
                cnt++;
                AudioManager.Instance.PlaySFX(baozhaSFX,GameManager.volume);
                Debug.Log("响了"+cnt+"声");
            }
            PoolManager.Release(baozha,transform.position);
            yield return waitbaozha;
            t++;
        }
        gameObject.SetActive(false);
    }
    protected virtual void OnDeath()
    {
        //if(isDie) return ;
        moveSpeed=0;
        GetComponent<Animator>().speed = 0;
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
        if (other.CompareTag("PlayerBullet"))
        {
            AudioManager.Instance.PlaySFX(MisSFX,GameManager.volume);   
            Bullet bullet = other.GetComponent<Bullet>();
            if (flashRedCoroutine != null)
            StopCoroutine(flashRedCoroutine);

            flashRedCoroutine = StartCoroutine(FlashRed());

            int totalDamage = bullet.damage + GameManager.Instance.attackPower;
            TakeDamage(totalDamage);
            if(bullet.getfalse())
            {
                other.gameObject.SetActive(false);
            }
        }
    }
    protected IEnumerator FlashRed()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = originalColor;
        }
    }
}
