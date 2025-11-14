using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
public class CubeTapHandler : MonoBehaviour
{
    public Vector3Int gridPos;
    private BoardGrid3D board;
    private static Color? selectedColor = null;
    // Biến static lưu cube đầu tiên được chọn
    private static CubeTapHandler firstSelectedCube = null;
    private static CubeTapHandler secondSelectedCube = null;

    private Color[] mainColors;
    Color cubeColor;
    // Hàm này sẽ được gọi từ BoardGrid3D
    public void Init(BoardGrid3D boardRef, Vector3Int pos)
    {
        board = boardRef;
        gridPos = pos;
    }
    void Start()
    {
        board = FindObjectOfType<BoardGrid3D>();
    }
    // Update is called once per frame
    void Update()
    {
    }
    private void OnMouseDown()
    {
        if (board == null)
        {
            Debug.Log("null do board");
            return;

        }
        Renderer render = GetComponent<Renderer>();
        if (render == null)
        {
            Debug.Log("null do render");
            return;
        }
        bool isMainColor = false;

        Color currentColor = render.material.color;
        mainColors = board.mainColors;
        foreach (Color c in mainColors)
        {
            IsMainColor(c);
        }
        Transform cube = transform; // chính là cube này
        cube.DORotate(cube.eulerAngles + Vector3.up * 90f, 0.3f)
            .SetEase(Ease.OutQuad);

        // Nếu chưa chọn cube đầu tiên
        if (firstSelectedCube == null)
        {
            // Chỉ lưu nếu cube có màu hợp lệ (trong mainColors)
            if (IsMainColor(currentColor) && gameObject.CompareTag("Specical"))
            {
                firstSelectedCube = this;
                cubeColor = firstSelectedCube.GetComponent<Renderer>().material.color;
                Debug.Log($"[SELECT] Đã chọn cube đầu tiên tại {gridPos} (màu: {currentColor})");
                selectedColor = currentColor;
            }
            else
            {
                Debug.Log("Vui lòng chọn cube có màu  khác ");
            }

        }
        else
        {
            bool hasLine = board.HasDirectLineConnection(firstSelectedCube.gridPos, gridPos);
            // Chỉ lưu nếu cube có màu hợp lệ (trong mainColors)
            if (IsMainColor(currentColor) && gameObject.CompareTag("Specical") && !hasLine)
            {
                firstSelectedCube = this;
                //lấy màu hiện tại của cube khi tap
                cubeColor = firstSelectedCube.GetComponent<Renderer>().material.color;
                Debug.Log($"[SELECT] Đã chọn lại cube đầu tiên tại {gridPos} (màu: {currentColor})");
                selectedColor = currentColor;
            }
            else
            {
                if (!gameObject.CompareTag("Specical"))
                {
                    Debug.Log($"🔍 VỊ TRÍ GRID: {gridPos} , vị trí firstSelectedCube : {firstSelectedCube.gridPos}"); // ← IN RA (x,y,z)
                    render.material.color = (Color)selectedColor;
                    board.HightLightLineBetween(gridPos, firstSelectedCube.gridPos, (Color)selectedColor);
                    firstSelectedCube = this;

                }
                else
                {
                    board.HightLightLineBetween(gridPos, firstSelectedCube.gridPos, (Color)selectedColor);
                    firstSelectedCube = null;
                }
            }
        }

    }
    private bool IsSameColor(Color cubeColor, Color currentColor)
    {
        if (Mathf.Approximately(cubeColor.r, currentColor.r) &&
                Mathf.Approximately(cubeColor.g, currentColor.g) &&
                Mathf.Approximately(cubeColor.b, currentColor.b))
            return true;
        return false;
    }
    // === Kiểm tra xem màu có trong mainColors không ===
    private bool IsMainColor(Color color)
    {
        foreach (Color c in mainColors)
        {
            if (Mathf.Approximately(color.r, c.r) &&
                Mathf.Approximately(color.g, c.g) &&
                Mathf.Approximately(color.b, c.b))
                return true;
        }
        return false;
    }
    //kiểm tra đường nối trực tiếp
    public bool HasDirectLine(CubeTapHandler other)
    {
        // cùng hàng hoặc cùng cột
        if (gridPos.x != other.gridPos.x && gridPos.z != other.gridPos.z)
            return false;
        //xác định hướng
        Vector3Int direction = new Vector3Int(
            Mathf.Clamp(other.gridPos.x - gridPos.x, -1, 1),
            0,
            Mathf.Clamp(other.gridPos.z - gridPos.z, -1, 1)
            );
        Vector3Int checkPos = gridPos + direction;
        while (checkPos != other.gridPos)
        {
            var cubeBetween = board.GetCubeAt(checkPos);
            if (cubeBetween != null) return false;
            checkPos += direction;
        }
        return false;
    }
}