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
    [SerializeField] private float boundsOffset = 1f;         // 超出屏幕边界的偏移量（尽可能小）
    bool isdade;
    private float leftBoundary;
    private float rightBoundary;
    private float topBoundary;
    private float bottomBoundary;
    private void Start()
    {
        // 计算屏幕边界（使用主相机）
        CalculateScreenBounds();
    }
    private void Update()
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
        isdade=false;
        if(GetComponent<SpriteRenderer>()!=null)
        GetComponent<SpriteRenderer>().enabled = true;
    }
    private void CheckBounds()
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

    private void DisableBullet()
    {
        gameObject.SetActive(false);
    }
    private void CalculateScreenBounds()
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
        transform.position += (Vector3)moveDirection * moveSpeed * Time.deltaTime;
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
        this.moveSpeed = speed;
    }

    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    IEnumerator Setfasle(float cd)
    {
        yield return new WaitForSeconds(cd);
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