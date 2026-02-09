using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class PlatformCreator : MonoBehaviour
{
    [MenuItem("UPWARD/Create Simple Platform")]
    static void CreatePlatform()
    {
        // Create cube
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "RisingPlatform";

        // Scale to platform shape
        platform.transform.localScale = new Vector3(6f, 0.8f, 2f);

        // Add material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.8f, 0.4f, 0.9f);
        platform.GetComponent<Renderer>().material = mat;

        Debug.Log("Platform created! Apply your texture to it!");
    }
}