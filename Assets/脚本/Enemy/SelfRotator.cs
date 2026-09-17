using UnityEngine;

/// <summary>
/// 自旋组件：不断自我旋转。
/// 支持外部通过 SetSpeed / ResetSpeed 临时调速并恢复原速。
/// </summary>
public class SelfRotator : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 90f;

    private float originalSpeed;

    private void Awake()
    {
        originalSpeed = rotateSpeed;
    }

    private void OnEnable()
    {
        rotateSpeed = originalSpeed;
    }

    private void Update()
    {
        transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);
    }

    public void SetSpeed(float speed) => rotateSpeed = speed;

    public void ResetSpeed() => rotateSpeed = originalSpeed;

    public float GetSpeed() => rotateSpeed;
    public float GetOriginalSpeed() => originalSpeed;
}
