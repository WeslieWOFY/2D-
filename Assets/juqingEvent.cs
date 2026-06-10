using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class juqingEvent : MonoBehaviour
{
    public string[] s;
    public string[] b;
    public Text[] Abb;
    // Start is called before the first frame update
    int x;
    private Coroutine currentACoroutine;
    private Coroutine stopACoroutine;

    private Coroutine currentBCoroutine;
    [Header("动画参数")]
    public float animDuration = 0.25f;   // 动画时长
    public float startScale = 1.5f;      // 入场起始大小（大）
    public float endScale = 1.5f;        // 出场结束大小（大）
    
    // 两个独立的协程变量
    
    // ========== A的对话控制 ==========
    public void SetAtext(int x)
    {
        // 先停止A正在进行的特效
        if (currentACoroutine != null)
            StopCoroutine(currentACoroutine);
        // 设置文字内容
        Abb[0].text = s[x];
        
        // 确保文字控件可见
        Abb[0].gameObject.SetActive(true);
        
        // 播放入场特效
        currentACoroutine = StartCoroutine(EnterCoroutine(Abb[0]));
    }
    
    public void SetAnull()
    {
        // 先停止A正在进行的特效
        if (currentACoroutine != null)
            StopCoroutine(currentACoroutine);
        
        // 播放清空特效（出场）
        currentACoroutine = StartCoroutine(ExitCoroutine(Abb[0], () => {
            Abb[0].text = "";
        }));
    }
    
    // ========== B的对话控制 ==========
    public void SetBtext(int x)
    {
        if (currentBCoroutine != null)
            StopCoroutine(currentBCoroutine);
        
        Abb[1].text = b[x];
        Abb[1].gameObject.SetActive(true);
        currentBCoroutine = StartCoroutine(EnterCoroutine(Abb[1]));
    }
    
    public void SetBnull()
    {
        if (currentBCoroutine != null)
            StopCoroutine(currentBCoroutine);
        
        currentBCoroutine = StartCoroutine(ExitCoroutine(Abb[1], () => {
            Abb[1].text = "";
        }));
    }
    
    // ========== 入场协程（从大到小）==========
    IEnumerator EnterCoroutine(Text targetText)
    {
        float elapsed = 0f;
        
        // 起始状态：放大 + 完全不透明
        targetText.transform.localScale = Vector3.one * startScale;
        Color color = targetText.color;
        color.a = 1f;
        targetText.color = color;
        
        Vector3 targetScale = Vector3.one;
        
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animDuration;
            float easeT = 1 - Mathf.Pow(1 - t, 2);  // 先快后慢
            
            targetText.transform.localScale = Vector3.Lerp(Vector3.one * startScale, targetScale, easeT);
            
            yield return null;
        }
        
        // 确保最终状态
        targetText.transform.localScale = targetScale;
        currentACoroutine = null;  // 如果是A调用的，这里会被覆盖，没关系
    }
    
    // ========== 出场协程（从小到大 + 淡出）==========
    IEnumerator ExitCoroutine(Text targetText, System.Action onComplete)
    {
        float elapsed = 0f;
        
        Vector3 startScaleCurrent = targetText.transform.localScale;
        Vector3 targetScale = Vector3.one * endScale;
        
        Color color = targetText.color;
        float startAlpha = color.a;
        float targetAlpha = 0f;
        
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animDuration;
            float easeT = t * t;  // 先慢后快
            
            targetText.transform.localScale = Vector3.Lerp(startScaleCurrent, targetScale, easeT);
            color.a = Mathf.Lerp(startAlpha, targetAlpha, easeT);
            targetText.color = color;
            
            yield return null;
        }
        
        // 动画结束后的清理
        targetText.gameObject.SetActive(false);
        onComplete?.Invoke();
        
        // 清空协程引用
        if (targetText == Abb[0])
            currentACoroutine = null;
        else if (targetText == Abb[1])
            currentBCoroutine = null;
    }
}
