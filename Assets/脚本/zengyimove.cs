using Unity.VisualScripting;
using UnityEngine;
public enum TriggerType
{
    Heal,        // 治疗
    MPHeal,  
    SpeedUp,     // 加速
    Power,
}
/// <summary>
/// 增益效果图标脚本：
/// 1. 上下轻度循环震荡（幅度 ±1.1）
/// 2. 持续向左移动，离开屏幕左边界后禁用自身
/// </summary>
public class zengyimove : MonoBehaviour
{
    [Header("震荡")]
    public float amplitude = 1.1f;   // 上下震荡幅度
    public float frequency = 1f;     // 震荡频率（Hz）

    [Header("移动")]
    public float moveSpeed = 2f;     // 向左移动速度
    [Header("碰撞方式")]
    public TriggerType triggerType = TriggerType.Heal;
    private Vector3 centerPos;       // 当前中心位置（X会持续减小）
    private Camera mainCam;
    [SerializeField] AudioClip SFX;

    void Start()
    {
        centerPos = transform.position;
        mainCam = Camera.main;
    }
    void OnEnable()
    {
        centerPos = transform.position;
    }
    void Update()
    {
        // 1. 向左移动
        centerPos += moveSpeed * Time.deltaTime * Vector3.left;

        // 2. 上下震荡（基于当前中心位置 ± amplitude）
        float offsetY = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude;
        transform.position = centerPos + new Vector3(0f, offsetY, 0f);

        // 3. 离开屏幕左边界后禁用自身
        if (mainCam != null)
        {
            // 屏幕左边界的世界坐标
            float screenLeftX = mainCam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;
            if (transform.position.x < screenLeftX-1f)
            {
                gameObject.SetActive(false);
            }
        }
    }
    void OnTriggerEnter2D(Collider2D  other)
    {
        if (other.CompareTag("Player"))
        {
            AudioManager.Instance.PlaySFX(SFX,GameManager.volume);
            switch(triggerType)
            {
                case TriggerType.Heal :
                GameManager.Instance.HealPlayer((int)(GameManager.Instance.playerMaxHealth*0.2));
                break;
                case TriggerType.MPHeal :
                GameManager.Instance.RestoreMana((int)(GameManager.Instance.playerMaxHealth*0.2));
                break;
                case TriggerType.SpeedUp:

                break;
                case TriggerType.Power:
                break;
            }
            gameObject.SetActive(false);
        }
    }
}
