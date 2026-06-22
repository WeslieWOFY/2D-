using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplittingBullet : Bullet
{
    [Header("分裂设置")]
    [SerializeField] private float splitAngle = 60f;
    [SerializeField] private GameObject splitBulletPrefab; // 分裂出的普通子弹预制体
    
    private bool hasSplit = false; // 只能分裂一次

    void OnEnable()
    {
        hasSplit = false;
    }
    void SetSplittrue()
    {
        hasSplit=true;
    }
    void OnTriggerEnter2D(Collider2D  other)
    {
        if (!hasSplit && other.CompareTag("Enemy"))
        {
            Split();
        }
    }


    private void Split()
    {
        hasSplit = true;
        
        // 向上60度
        Quaternion upRotation = transform.rotation * Quaternion.Euler(0, 0, splitAngle);
        CreateSplitBulletFromPool(upRotation);
        
        // 向下60度
        Quaternion downRotation = transform.rotation * Quaternion.Euler(0, 0, -splitAngle);
        CreateSplitBulletFromPool(downRotation);
        
    }

    private void CreateSplitBulletFromPool(Quaternion rotation)
    {
        if (splitBulletPrefab == null)
        {
            return;
        }
        
        // 使用对象池生成分裂子弹，传入位置和旋转
        GameObject splitBullet = PoolManager.Release(splitBulletPrefab, transform.position, rotation);
        if (splitBullet != null)
        {
            // 设置伤害值
            Bullet bulletComp = splitBullet.GetComponent<Bullet>();
            if (bulletComp != null)
            {
                bulletComp.damage = damage; // 继承伤害
            }
            // 如果是分裂子弹类型，确保它不能再分裂
            SplittingBullet splittingComp = splitBullet.GetComponent<SplittingBullet>();
            if (splittingComp != null)
            {
                splittingComp.SetSplittrue();
            }
        }
    }
}
