using UnityEngine;

public class ScrollBG : MonoBehaviour
{
    public float speed = 0.05f;
    Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        Vector2 o = mat.mainTextureOffset;
        o.y += speed * Time.deltaTime;   // scroll down
        mat.mainTextureOffset = o;
    }
}
