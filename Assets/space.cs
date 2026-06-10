using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class space : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void OnEnable()
    {
        foreach(Transform bullet in transform)
        {
            bullet.gameObject.SetActive(true);
        }
    }
    void Ondisable()
    {
        foreach(Transform bullet in transform)
        {
            bullet.gameObject.SetActive(false);
        }
    }
}
