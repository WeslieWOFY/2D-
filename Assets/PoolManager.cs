using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    [SerializeField] Pool[] MountPool;
    [SerializeField] Pool[] Bullet;

    [SerializeField] Pool[] SFX;


    static Dictionary<GameObject,Pool> dictionary;
    void Awake()
    {
        dictionary=new Dictionary<GameObject,Pool>();
        Initialize(MountPool);
        Initialize(Bullet);
        Initialize(SFX);
    }
    void Initialize(Pool[] pools)
    {
        foreach(var pool in pools)
        {
            if(dictionary.ContainsKey(pool.Prefab))
            {
                continue;
            }
            dictionary.Add(pool.Prefab,pool);
            Transform poolParent=new GameObject("Pool: "+pool.Prefab.name).transform;
            poolParent.parent=transform;
            pool.Initialize(poolParent);
        }
    }

    public static GameObject Release(GameObject prefab)
    {
        if(!dictionary.ContainsKey(prefab))
        {
            return null;
        }
        return dictionary[prefab].PreparedObject();
    }
    public static GameObject Release(GameObject prefab,Vector3 position)
    {
        if(!dictionary.ContainsKey(prefab))
        {
            return null;
        }
        return dictionary[prefab].PreparedObject(position);
    }
    public static GameObject Release(GameObject prefab,Vector3 position,Quaternion rotation)
    {
        if(!dictionary.ContainsKey(prefab))
        {
            return null;
        }
        return dictionary[prefab].PreparedObject(position,rotation);
    }

    public static GameObject Release(GameObject prefab,Vector3 position,Quaternion rotation,Vector3 localScale)
    {
        if(!dictionary.ContainsKey(prefab))
        {
            return null;
        }
        return dictionary[prefab].PreparedObject(position,rotation,localScale);
    }
}
