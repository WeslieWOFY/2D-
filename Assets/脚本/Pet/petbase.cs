using System.Collections;
using System.Collections.Generic;
//using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using UnityEngine.UI;
public abstract class petbase : MonoBehaviour
{
    [Header("等级")]
    [SerializeField] protected int level = 1;

    [Header("攻击")]
    [SerializeField] protected float attackInterval = 0.2f;      // 攻击间隔
    [SerializeField] protected float chargeDuration = 2f;        // 蓄力时长
    //[SerializeField] protected float chargeCooldown = 5f;        // 蓄力冷却

    [Header("子弹")]
    [SerializeField] protected GameObject[] bulletPrefabs;       // 子弹预制体数组
    [SerializeField] protected GameObject xulibulletPrefabs;       // 子弹预制体数组

    [SerializeField] protected Transform[] spawnPoints;          // 发射点数组

    [SerializeField] protected Transform xuli;          // 发射点数组
    [SerializeField] protected GameObject VFX;          // 特效预制体

    [Header("音效")]
    [SerializeField] protected AudioClip projectSFX;
    [SerializeField] protected AudioClip xuliattacksFX;

    [Header("蓝量消耗")]
    [SerializeField] public int manaCost = 30;
    [Header("状态")]
    protected bool canCharge = true;
    protected bool isCharging = false;
    [Header("蓄力UI")]
    protected float currentChargeTime = 0f; 
    [SerializeField] protected Canvas canvas;
    [SerializeField] protected Image image;
    bool k=false;
    protected Coroutine chargeCoroutine;
    protected Coroutine attackCoroutine;

    protected Coroutine xulijinduCoroutine;

    
    protected Coroutine chargeProgressCoroutine;
    GameObject sFX;


        // ============ 抽象函数：子类实现具体攻击 ============

    /// <summary> 普通攻击的具体逻辑 </summary>
    protected abstract IEnumerator NormalAttackRoutine();

    /// <summary> 蓄力攻击的具体逻辑 </summary>
    //protected abstract IEnumerator ChargeAttackRoutine();

    public void OnEnable()
    {
        chargeCoroutine=null;
        attackCoroutine=null;
        xulijinduCoroutine=null;
        image.fillAmount=0f;
    }
    // ============ 公共接口 ============

    /// <summary> 普通攻击 </summary>
    public void NormalAttack()
    {
        k=true;
        if (isCharging) return;
        if(attackCoroutine!=null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine=null;
        }
        if(attackCoroutine==null)
        {
            attackCoroutine = StartCoroutine(NormalAttackRoutine());
        }
    }

    public void StopNormalAttack()
    {
        if(attackCoroutine!=null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        k=false;
        Debug.Log("取消了攻击");
    }

    /// <summary> 开始蓄力 </summary>
    public void StartCharge()
    {        // 开始播放
        canvas.gameObject.SetActive(true);
        AudioManager.Instance.PlayxuliSFX(GameManager.volume);
        if(attackCoroutine!=null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine=null;
        }
        isCharging=true;
        sFX=PoolManager.Release(VFX, xuli.position);
        sFX.transform.SetParent(transform);
        if (chargeProgressCoroutine != null)
        {
            StopCoroutine(chargeProgressCoroutine);
        }
        chargeProgressCoroutine = StartCoroutine(ChargeProgress());
    }
        /// <summary> 蓄力进度协程 </summary>
    private IEnumerator ChargeProgress()
    {
        while (isCharging)
        {
            currentChargeTime += Time.deltaTime;
            image.fillAmount+=Time.deltaTime/chargeDuration;
            if(image.fillAmount>=1f)
            image.fillAmount=1f;
            Debug.Log($"Image fillAmount设为0.5，当前值: {image.fillAmount}");
            yield return null;
        }
    }
    /// <summary> 取消蓄力 </summary>
    public void CancelCharge()
    {
        AudioManager.Instance.StopxuliSFX();
        sFX.transform.SetParent(null);
        sFX.gameObject.SetActive(false);
        isCharging = false;
        if (currentChargeTime >= chargeDuration)
        {
            if (GameManager.Instance.TryUseMana(manaCost))
            {
                AudioManager.Instance.PlaySFX(xuliattacksFX,GameManager.volume);
                PoolManager.Release(xulibulletPrefabs, xuli.position);
            }
        }
        if(k)
        {
            attackCoroutine=StartCoroutine(NormalAttackRoutine());
        }
        currentChargeTime=0f;
        image.fillAmount=0f;
        canvas.gameObject.SetActive(false);
    }
}
