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
    
    private Vector3 worldStartPos;       // 世界坐标起点
    private Vector3 parentStartPos;      // 父物体世界坐标起点
    private Coroutine currentMotion;

    void OnEnable()
    {
        worldStartPos = transform.position;
        parentStartPos = transform.parent.position;
        
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
        // 圆心：世界空间中的起始位置 + 世界Y轴上方 * 半径
        Vector3 worldCenter = worldStartPos + Vector3.up * radius;
        Vector3 r = worldStartPos - worldCenter;
        float rotationSpeed = 360f / duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 逆时针旋转半径向量
            r = Quaternion.AngleAxis(rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            // 圆心跟随父物体平移（不跟随旋转）
            Vector3 parentDelta = transform.parent.position - parentStartPos;
            transform.position = worldCenter + parentDelta + r;
            
            yield return null;
        }

        // 回到跟随父物体平移后的初始位置
        transform.position = worldStartPos + (transform.parent.position - parentStartPos);
    }

    // 模式2：圆心在左边，逆时针
    IEnumerator MotionXLeftAntiClockwise()
    {
        Vector3 worldCenter = worldStartPos + Vector3.left * radius;
        Vector3 r = worldStartPos - worldCenter;
        float rotationSpeed = 360f / duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            r = Quaternion.AngleAxis(rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            Vector3 parentDelta = transform.parent.position - parentStartPos;
            transform.position = worldCenter + parentDelta + r;
            
            yield return null;
        }

        transform.position = worldStartPos + (transform.parent.position - parentStartPos);
    }

    // 模式3：圆心在下方，顺时针
    IEnumerator MotionYDownClockwise()
    {
        Vector3 worldCenter = worldStartPos + Vector3.down * radius;
        Vector3 r = worldStartPos - worldCenter;
        float rotationSpeed = 360f / duration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 顺时针旋转半径向量（负角度）
            r = Quaternion.AngleAxis(-rotationSpeed * Time.deltaTime, Vector3.forward) * r;
            
            Vector3 parentDelta = transform.parent.position - parentStartPos;
            transform.position = worldCenter + parentDelta + r;
            
            yield return null;
        }

        transform.position = worldStartPos + (transform.parent.position - parentStartPos);
    }
}
