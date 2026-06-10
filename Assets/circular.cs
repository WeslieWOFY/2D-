using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class circular : MonoBehaviour
{
    [Header("运动模式控制（同时只能激活一个）")]
    public bool mode1_YUp_AntiClockwise = false;   // 模式1：沿Y轴上方逆时针旋转一圈
    public bool mode2_XLeft_AntiClockwise = false;  // 模式2：沿X轴左边逆时针旋转一圈
    public bool mode3_YDown_Clockwise = false;      // 模式3：沿Y轴下方顺时针旋转一圈

    [Header("运动参数")]
    public float radius = 2f;
    public float duration = 2f;
    
    private Vector2 localStartPos2D;
    private Coroutine currentMotion;

    void OnEnable()
    {
        localStartPos2D = transform.localPosition;
        
        if (currentMotion != null)
            StopCoroutine(currentMotion);
        
        if (mode1_YUp_AntiClockwise)
            currentMotion = StartCoroutine(MotionYUpAntiClockwise());
        else if (mode2_XLeft_AntiClockwise)
            currentMotion = StartCoroutine(MotionXLeftAntiClockwise());
        else
            currentMotion = StartCoroutine(MotionYDownClockwise());
    }
    
    void OnDisable()
    {
        if (currentMotion != null)
        {
            StopCoroutine(currentMotion);
            currentMotion = null;
        }
    }

    // 模式1：圆心在上方，逆时针
    IEnumerator MotionYUpAntiClockwise()
    {
        Vector3 center = (Vector3)(localStartPos2D + Vector2.up * radius);
        Vector3 r = (Vector3)localStartPos2D - center;  // 半径向量：圆心指向物体
        float rotationSpeed = 360f / duration;            // 每秒旋转度数
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 逆时针旋转半径向量
            r = Quaternion.AngleAxis(rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            // 圆心 + 半径 = 圆上的点
            Vector3 pos = center + r;
            transform.localPosition = new Vector3(pos.x, pos.y, transform.localPosition.z);
            
            yield return null;
        }

        transform.localPosition = new Vector3(localStartPos2D.x, localStartPos2D.y, transform.localPosition.z);
    }

    // 模式2：圆心在左边，逆时针
    IEnumerator MotionXLeftAntiClockwise()
    {
        Vector3 center = (Vector3)(localStartPos2D + Vector2.left * radius);
        Vector3 r = (Vector3)localStartPos2D - center;  // 半径向量：圆心指向物体
        float rotationSpeed = 360f / duration;            // 每秒旋转度数
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 逆时针旋转半径向量
            r = Quaternion.AngleAxis(rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            // 圆心 + 半径 = 圆上的点
            Vector3 pos = center + r;
            transform.localPosition = new Vector3(pos.x, pos.y, transform.localPosition.z);
            
            yield return null;
        }

        transform.localPosition = new Vector3(localStartPos2D.x, localStartPos2D.y, transform.localPosition.z);
    }

    // 模式3：圆心在下方，顺时针
    IEnumerator MotionYDownClockwise()
    {
        Vector3 center = (Vector3)(localStartPos2D + Vector2.down * radius);
        Vector3 r = (Vector3)localStartPos2D - center;  // 半径向量：圆心指向物体
        float rotationSpeed = 360f / duration;            // 每秒旋转度数
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 顺时针旋转半径向量（负角度）
            r = Quaternion.AngleAxis(-rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            // 圆心 + 半径 = 圆上的点
            Vector3 pos = center + r;
            transform.localPosition = new Vector3(pos.x, pos.y, transform.localPosition.z);
            
            yield return null;
        }

        transform.localPosition = new Vector3(localStartPos2D.x, localStartPos2D.y, transform.localPosition.z);
    }
}
