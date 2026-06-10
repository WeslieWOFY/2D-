using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float outOfScreenMargin = 1f;
    public int damage = 10;
    [SerializeField] private bool issetfalse=true;

    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 直线移动：沿自身右边方向
        transform.Translate(Vector3.right * speed * Time.deltaTime);

        // 出屏回收
        if (IsOutOfScreen())
            gameObject.SetActive(false);
    }

    private bool IsOutOfScreen()
    {
        Vector3 viewPos = mainCamera.WorldToViewportPoint(transform.position);
        return viewPos.x < -outOfScreenMargin
            || viewPos.x > 1 + outOfScreenMargin
            || viewPos.y < -outOfScreenMargin
            || viewPos.y > 1 + outOfScreenMargin;
    }
    public bool getfalse() =>issetfalse;
}
