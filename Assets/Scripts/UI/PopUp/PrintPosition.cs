using UnityEngine;

public class PrintPosition : MonoBehaviour
{
    public GameObject targetObject;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Object position: " + targetObject.transform.position);
    }

}
