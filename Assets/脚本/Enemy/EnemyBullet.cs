using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class EnemyBullet : MonoBehaviour  // 确保继承 MonoBehaviour
{
    [Header("子弹属性")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector2 moveDirection = Vector2.left;
    [Header("旋转")]
    public float rotateSpeed = 90f;
    public bool isrotate=false;
    [Header("边界设置")]
    [SerializeField] private bool disableOutOfBounds = true;  // 超出边界是否禁用
    [SerializeField] protected float boundsOffset = 1f;         // 超出屏幕边界的偏移量（尽可能小）

    [Header("超时设置")]
    [SerializeField] private bool disableByTimeout = false;   // 超时是否禁用
    [SerializeField] private float timeoutDuration = 5f;       // 超时时间（秒）

    bool isdade;
    private Coroutine timeoutCoroutine;

    // ========== 事件总线：清屏弹幕 ==========
    private static float _banEndTime;  // 禁止启用的截止时间，所有子弹共享
    protected float originalMoveSpeed;
    private Vector2 originalMoveDirection;
    protected float leftBoundary;
    protected float rightBoundary;
    protected float topBoundary;
    protected float bottomBoundary;
    protected void Awake()
    {
        // 缓存原始值，供对象池复用时重置（必须在Awake，早于OnEnable）
        originalMoveSpeed = moveSpeed;
        originalMoveDirection = moveDirection;
    }

    private void Start()
    {
        // 计算屏幕边界（使用主相机）
        CalculateScreenBounds();
    }
    protected virtual void Update()
    {
        if(isrotate)
        {
            transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
        }
        Move();
        CheckBounds();
    }
    protected virtual void OnEnable()
    {
        // 全局禁止期内，对象池取出的子弹直接禁用
        if (Time.time < _banEndTime)
        {
            gameObject.SetActive(false);
            return;
        }

        isdade=false;

        // 重置为原始值（对象池复用时避免残留上次修改）
        moveSpeed = originalMoveSpeed;
        moveDirection = originalMoveDirection;

        if(GetComponent<SpriteRenderer>()!=null)
        GetComponent<SpriteRenderer>().enabled = true;

        // 超时禁用
        if (disableByTimeout && timeoutDuration > 0f)
        {
            if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = StartCoroutine(TimeoutDisable());
        }

        // 签到事件总线：激活期间收到清屏事件则自禁
        LevelEventBus.On("ClearAllBullets", OnClearAllBullets);
    }

    protected virtual void OnDisable()
    {
        // 签退事件总线：已失活的子弹不再响应清屏事件
        LevelEventBus.Off("ClearAllBullets", OnClearAllBullets);

        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }
    protected virtual void CheckBounds()
    {
        if (!disableOutOfBounds) return;
        
        Vector3 pos = transform.position;
        
        // 检查是否超出边界（包含偏移量）
        if (pos.x < leftBoundary || pos.x > rightBoundary || 
            pos.y < bottomBoundary || pos.y > topBoundary)
        {
            DisableBullet();
        }
    }

    protected void DisableBullet()
    {
        gameObject.SetActive(false);
    }
    protected void CalculateScreenBounds()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 获取屏幕四个角的世界坐标
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, 0));
            
            leftBoundary = bottomLeft.x - boundsOffset;
            rightBoundary = topRight.x + boundsOffset;
            bottomBoundary = bottomLeft.y - boundsOffset;
            topBoundary = topRight.y + boundsOffset;
        }
    }
    public void Move()
    {
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
    }
    public void SetMoveDirection(Vector2 direction)
    {
        moveDirection = direction.normalized;
    }
    
    public int GetDamage()
    {
        return damage;
    }

    public void SetDamage(int damage)
    {
        this.damage=damage;
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    public Vector2 GetMoveDirection()
    {
        return moveDirection;
    }
    IEnumerator Setfasle(float cd)
    {
        yield return new WaitForSeconds(cd);
        gameObject.SetActive(false);
    }

    private IEnumerator TimeoutDisable()
    {
        yield return new WaitForSeconds(timeoutDuration);
        gameObject.SetActive(false);
    }

    /// <summary>事件总线回调：收到清屏事件时立即自禁，并设全局禁止期</summary>
    private void OnClearAllBullets(object data = null)
    {
        // data 可传自定义禁用秒数，不传默认 3 秒
        float duration = (data is float f) ? f : 3f;
        _banEndTime = Time.time + duration;
        gameObject.SetActive(false);
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player")&&!isdade)
        {
            isdade=true;
            if(GetComponent<SpriteRenderer>()!=null)
            GetComponent<SpriteRenderer>().enabled = false;
            StartCoroutine(Setfasle(0.15f));
        }
    }
}