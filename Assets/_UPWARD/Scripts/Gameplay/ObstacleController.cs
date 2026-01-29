using UnityEngine;

public class ObstacleController : MonoBehaviour
{
    Rigidbody rb;
    [SerializeField] float force;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
 
    }

}
