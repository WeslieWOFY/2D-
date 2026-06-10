using UnityEngine;

public class PersistentSingleton<T> : MonoBehaviour where T:Component
{
    public static T Instance {get;set;}
    protected virtual void Awake()
    {
        if(Instance==null)
        {
            Instance=this as T;
        }
        else if(Instance!=this)
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }
     protected virtual void Start()
    {
            
    }
}
