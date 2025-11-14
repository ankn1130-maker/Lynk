using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode] // cho phép chạy cả trong editor
public class AutoCenterCamera : MonoBehaviour
{
    [Header("Target")]
    public BoardGrid3D board; // Kéo BoardGrid3D vào đây

    [Header("Camera Settings")]
    public float height = 20f;                    // Chiều cao camera
    public float zoomMultiplier = 1.3f;           // Zoom tự động (càng lớn càng xa)
    public bool lockRotation = true;              // Luôn nhìn thẳng xuống (90°)
    // Start is called before the first frame update

    private Camera cam;
    void Start()
    {
        cam = GetComponent<Camera>();

    }

    // Update is called once per frame
    void Update()
    {
        if (board == null)
            board = FindObjectOfType<BoardGrid3D>();

        if (board != null)
            UpdateCamera();
    }

    [ContextMenu("Update Camera Now")]
    void UpdateCamera()
    {
        // 1. Tính tâm board
        Vector3 center = board.transform.position;

        // 2. Đặt camera ngay trên tâm
        transform.position = center + Vector3.up * height;

        // 3. Luôn nhìn thẳng xuống
        if (lockRotation)
            transform.rotation = Quaternion.Euler(90, 0, 0);

        // 4. Orthographic + Zoom tự động theo board
        cam.orthographic = true;
        float boardSize = Mathf.Max(board.sizeX, board.sizeZ) * board.cellSize;
        cam.orthographicSize = boardSize * zoomMultiplier;
    }

    // Hiển thị tâm board trong Scene View
    void OnDrawGizmosSelected()
    {
        if (board != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(board.transform.position, Vector3.one * 0.5f);
            Gizmos.DrawRay(board.transform.position, Vector3.up * 3f);
        }
    }
}
