using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatsBar_HUD : MonoBehaviour
{
    // Start is called before the first frame update
    Canvas canvas;

    public static StatsBar_HUD Instance { get; private set; }

    [SerializeField] Image fillImageBack;
    [SerializeField] Image fillImageFront;

    [SerializeField] float fillSpeed=0.1f;
    [SerializeField] bool delayFill=true;
    [SerializeField] float fillDelay=0.1f;

    WaitForSeconds waitForDelayFill;
    Coroutine bufferedFillingCoroutine;
    float currentFillValue;
    float targetFillValue;

    float t;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        canvas=GetComponent<Canvas>();
        canvas.worldCamera=Camera.main;
        waitForDelayFill=new WaitForSeconds(fillDelay);
    }

    void Start()
    {
        // 注册扣血和回血事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.OnPlayerTakeDamage += HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
            GameManager.Instance.Init+=Initialize;
        }
    }
    void OnEnable()
    {
        // 注册扣血和回血事件
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.OnPlayerTakeDamage += HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
            GameManager.Instance.Init+=Initialize;
        }
    }
    void OnDisable()
    {
        // 取消注册事件，避免内存泄漏
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTakeDamage -= HandleHealthChanged;
            GameManager.Instance.Init-=Initialize;
        }
    }

    void HandleHealthChanged(int currentHealth, int changeAmount)
    {
        Debug.Log("执行数");
        UpdateStats(currentHealth, GameManager.Instance.playerMaxHealth);
    }


    public void Initialize(int currentValue,int playerMaxHealth)
    {
        Debug.Log("初始化成功!");
        currentFillValue=currentValue*(1.0f)/playerMaxHealth;
        targetFillValue=playerMaxHealth*(1.0f);
        fillImageBack.fillAmount=currentFillValue;
        fillImageFront.fillAmount=currentFillValue;
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
}
