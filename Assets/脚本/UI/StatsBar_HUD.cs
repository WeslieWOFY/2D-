using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatsBar_HUD : MonoBehaviour
{
    Canvas canvas;
    public static StatsBar_HUD Instance { get; private set; }

    [Header("血条")]
    [SerializeField] Image fillImageBack;
    [SerializeField] Image fillImageFront;

    [Header("蓝条")]
    [SerializeField] Image manaFillBack;
    [SerializeField] Image manaFillFront;

    [Header("动画")]
    [SerializeField] float fillSpeed=0.1f;
    [SerializeField] bool delayFill=true;
    [SerializeField] float fillDelay=0.1f;

    WaitForSeconds waitForDelayFill;
    Coroutine bufferedFillingCoroutine;
    float currentFillValue;
    float targetFillValue;

    Coroutine manaFillingCoroutine;
    float currentManaFillValue;
    float targetManaFillValue;

    float t;
    // 跨场景可销毁：不再 DontDestroyOnLoad，血条随场景加载/卸载生灭。
    // 每个关卡场景自带一份 HUD，重玩/切场景时新场景的 HUD 重新接管
    // （Awake 里重新绑定新场景的摄像机，避免旧实例引用已销毁摄像机导致 UI 消失）。
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        canvas=GetComponent<Canvas>();
        canvas.worldCamera=Camera.main;
        waitForDelayFill=new WaitForSeconds(fillDelay);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.OnPlayerTakeDamage += HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
            GameManager.Instance.Init+=Initialize;
            GameManager.Instance.OnPlayerManaChanged -= HandleManaChanged;
            GameManager.Instance.OnPlayerManaChanged += HandleManaChanged;

            // 重玩等场景重载时 GameManager 已常驻、Init 事件不会再触发，
            // 这里直接按管理器当前值初始化血/蓝条（管理器是唯一数据源）
            Initialize(GameManager.Instance.PlayerCurrentHealth, GameManager.Instance.playerMaxHealth);
        }
    }
    void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.OnPlayerTakeDamage += HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
            GameManager.Instance.Init+=Initialize;
            GameManager.Instance.OnPlayerManaChanged -= HandleManaChanged;
            GameManager.Instance.OnPlayerManaChanged += HandleManaChanged;
        }
    }
    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
            GameManager.Instance.OnPlayerManaChanged -= HandleManaChanged;
        }
    }

    void HandleHealthChanged(int currentHealth, int changeAmount)
    {
        UpdateStats(currentHealth, changeAmount);
    }

    void HandleManaChanged(int currentMana, int maxMana)
    {
        UpdateMana(currentMana, maxMana);
    }

    public void Initialize(int currentValue,int playerMaxHealth)
    {
        currentFillValue=currentValue*(1.0f)/playerMaxHealth;
        targetFillValue=currentFillValue;
        fillImageBack.fillAmount=currentFillValue;
        fillImageFront.fillAmount=currentFillValue;
        InitializeMana(GameManager.Instance.PlayerCurrentMana, GameManager.Instance.playerMaxMana);
    }

    void InitializeMana(int currentValue, int maxMana)
    {
        currentManaFillValue = currentValue * 1.0f / maxMana;
        targetManaFillValue = currentManaFillValue;
        if (manaFillBack != null) manaFillBack.fillAmount = currentManaFillValue;
        if (manaFillFront != null) manaFillFront.fillAmount = currentManaFillValue;
    }

    public void UpdateStats(int currentValue,int playerMaxHealth)
    {
        targetFillValue=currentValue*(1.0f)/playerMaxHealth;
        if(bufferedFillingCoroutine!=null)
        {
            StopCoroutine(bufferedFillingCoroutine);
        }
        if(currentFillValue>targetFillValue)
        {
            fillImageFront.fillAmount=targetFillValue;
            bufferedFillingCoroutine=StartCoroutine(BufferedFillingCoroutine(fillImageBack));
        }

        if(currentFillValue<targetFillValue)
        {
            fillImageBack.fillAmount=targetFillValue;
            bufferedFillingCoroutine=StartCoroutine(BufferedFillingCoroutine(fillImageFront));
        }
    }

    public void UpdateMana(int currentValue, int maxMana)
    {
        targetManaFillValue = currentValue * 1.0f / maxMana;
        if (manaFillingCoroutine != null)
        {
            StopCoroutine(manaFillingCoroutine);
        }
        if (currentManaFillValue > targetManaFillValue)
        {
            if (manaFillFront != null) manaFillFront.fillAmount = targetManaFillValue;
            manaFillingCoroutine = StartCoroutine(BufferedManaFillingCoroutine(manaFillBack));
        }
        if (currentManaFillValue < targetManaFillValue)
        {
            if (manaFillBack != null) manaFillBack.fillAmount = targetManaFillValue;
            manaFillingCoroutine = StartCoroutine(BufferedManaFillingCoroutine(manaFillFront));
        }
    }

    IEnumerator BufferedFillingCoroutine(Image image)
    {
        if(delayFill)
        {
            yield return waitForDelayFill;
        }
        t=0f;
        while(t<1f)
        {
            t+=Time.deltaTime*fillSpeed;
            currentFillValue=Mathf.Lerp(currentFillValue,targetFillValue,t);
            image.fillAmount=currentFillValue;
            yield return null;
        }
    }

    IEnumerator BufferedManaFillingCoroutine(Image image)
    {
        if (delayFill) yield return waitForDelayFill;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fillSpeed;
            currentManaFillValue = Mathf.Lerp(currentManaFillValue, targetManaFillValue, t);
            if (image != null) image.fillAmount = currentManaFillValue;
            yield return null;
        }
    }
}
