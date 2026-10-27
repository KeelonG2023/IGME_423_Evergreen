using UnityEngine;

/// <summary>Spins a transform around one of its local axes. Used for wind turbine blades.</summary>
public class Spinner : MonoBehaviour
{
    [SerializeField] Vector3 localAxis = Vector3.forward;
    [SerializeField] float degreesPerSecond = 90f;

    void Update() => transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self);
}
