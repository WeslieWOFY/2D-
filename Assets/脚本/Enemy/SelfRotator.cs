using UnityEngine;

/// <summary>
/// 自旋组件：不断自我旋转
/// </summary>
public class SelfRotator : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 90f;  // 旋转速度（度/秒）

    private void Update()
    {
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }
}
