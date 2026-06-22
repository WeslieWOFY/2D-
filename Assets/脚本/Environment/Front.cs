using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Front : MonoBehaviour
{
    [Header("对象池设置")]
    [SerializeField] private GameObject[] FrontPrefab;
    
    [Header("生成参数")]
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private float randomY =2f;
    [SerializeField]    public float maxLifeTime = 15f;  // 最大生命周期（秒），超时强制回收
    [SerializeField] private float sp = 2f;
    [SerializeField] private float first = 2f;

    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        StartCoroutine(SpawnLoop());
    }
    
    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(first);
        while (true)
        {
            StartCoroutine(SpawnObject());
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    IEnumerator SpawnObject()
    {        
        // 获取摄像机右边界
        float rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1, 0, 0)).x;
        
        // 生成位置（屏幕外）
        Vector3 spawnPos = new Vector3(rightEdge + 12f, randomY, 0);
        
        // 从对象池获取
        foreach(var pool in FrontPrefab)
        {
            PoolManager.Release(pool, spawnPos);
            yield return new WaitForSeconds(sp);
        }
    }
}