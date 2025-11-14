using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target; // BoardManager
    public float distance = 10f, height = 5f, angleX = 45f, angleY = 45f;
    public float sensitivity = 2f;

    void Start() { target = FindObjectOfType<BoardGrid3D>().transform; }

    void LateUpdate()
    {
        angleY += Input.GetAxis("Mouse X") * sensitivity;
        angleX -= Input.GetAxis("Mouse Y") * sensitivity;
        angleX = Mathf.Clamp(angleX, 10f, 80f);

        Quaternion rot = Quaternion.Euler(angleX, angleY, 0);
        transform.position = target.position - rot * Vector3.forward * distance + Vector3.up * height;
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}