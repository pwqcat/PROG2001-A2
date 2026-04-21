using UnityEngine;

public class CircleRotateObstacle : MonoBehaviour
{
    public Transform centerPoint; 
    public float rotateSpeed = 50f;

    void Update()
    {
        transform.RotateAround(centerPoint.position, Vector3.up, rotateSpeed * Time.deltaTime);
    }
}