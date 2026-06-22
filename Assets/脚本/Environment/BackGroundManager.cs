using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackGroundManager : MonoBehaviour
{
    [Header("对象池设置")]
    [SerializeField] private GameObject backgroundPrefab;
    
    [Header("生成参数")]
    [SerializeField] private float firstspawn = 10f;
    [SerializeField] private float spawnInterval = 10f;
    [SerializeField] private float randomY =2f;

    [SerializeField] private float limit =12f;

    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        StartCoroutine(SpawnLoop());
    }
    
    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(firstspawn);
        while (true)
        {
            SpawnObject();
            yield return new WaitForSeconds(spawnInterval);
        }
    }
    
    void SpawnObject()
    {
        if (backgroundPrefab == null) return;
        
        // 获取摄像机右边界
        float rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1, 0, 0)).x;
        
        
        // 生成位置（屏幕外）
        Vector3 spawnPos = new Vector3(rightEdge + limit, randomY, 0);
        
        // 从对象池获取
        PoolManager.Release(backgroundPrefab, spawnPos);
    }
}