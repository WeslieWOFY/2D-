using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using System;

public class EnemyManager: MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] GameObject[] enemyPrefabs;
    [SerializeField] float timeBetweenSpawns=1f;
    WaitForSeconds waitTimeBetweenSpawns;

    //int waveNumber=1;
    //int enemyAmount;
    //int Maxwave=7;
    //int wave=0;
    void Awake()
    {
        waitTimeBetweenSpawns=new WaitForSeconds(timeBetweenSpawns);
    }

    /*void Start()
    {
        StartCoroutine(SpawnWaveCoroutine());
    }
    IEnumerator SpawnWaveCoroutine()
    {
        for (int i = 1; i <=Maxwave; i++)
        {
            switch(i)
            {
                case 1:
                    yield return StartCoroutine(Wave1());
                    break; 
                case 2:
                    yield return StartCoroutine(Wave2());
                    break;
                case 3:
                    yield return StartCoroutine(Wave3());
                    break;
                case 4:
                    yield return StartCoroutine(Wave4());
                    break;
                case 5:
                    yield return StartCoroutine(Wave5());
                    break;
            }
            yield return new WaitForSeconds(2f);
        }
    }
    IEnumerator SpawnRoutine(Wave wave)
    {
        float elapsedTime = 0f;
        float nextSpawnTime = 0f;
        
        while (elapsedTime < wave.duration)
        {
            // 到达刷怪时间
            if (elapsedTime >= nextSpawnTime)
            {
                // 生成一波怪物
                int spawnCount = Random.Range(wave.minSpawnPerBatch, wave.maxSpawnPerBatch + 1);
                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnMonster(wave);
                    
                    // 如果一次生成多个怪物，之间加个小间隔（可选）
                    if (i < spawnCount - 1 && wave.spawnIntervalPerBatch > 0)
                        yield return new WaitForSeconds(wave.spawnIntervalPerBatch);
                }
                
                // 计算下次刷怪时间
                nextSpawnTime += wave.spawnInterval;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        isSpawning = false;
    }*/
}
