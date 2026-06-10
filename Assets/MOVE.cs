using UnityEngine;

public class MOVE : MonoBehaviour
{
    public float scrollSpeed = 0.5f;
    private Material material;
    
    void Start()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            material.mainTexture.wrapMode = TextureWrapMode.Repeat;
        }
    }
    
    void Update()
    {
        if (material != null)
        {
            float offset = scrollSpeed * Time.time;
            material.mainTextureOffset = new Vector2(offset, 0);
        }
    }
}