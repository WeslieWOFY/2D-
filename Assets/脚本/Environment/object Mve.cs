using UnityEngine;

public class MoveLeft : MonoBehaviour
{
    [Header("移动速度")]
    public float speed = 2f;
    [Header("旋转")]
    public float rotateSpeed = 90f;
    public bool isrotate=false;

    [Header("边界")]
    public float leftBoundOffset = 3f;
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private float objectWidth;
    private float leftEdge;
    void Start()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            objectWidth = spriteRenderer.bounds.size.x;
        }
        else
        {
            objectWidth = transform.localScale.x;
        }    
    }
    void OnEnable()
    {
        if (mainCamera != null)
        {
            leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x;
        }
    }
    
    void Update()
    {
        // 向左移动
        transform.Translate(Vector3.left * speed * Time.deltaTime,Space.World);
        if (mainCamera != null)
        {
            leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, 0)).x;
        }
        float objectRightEdge = transform.position.x + (objectWidth / 2);
        if (objectRightEdge < leftEdge - leftBoundOffset)
        {
            gameObject.SetActive(false);
        }
        if(isrotate)
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }
}