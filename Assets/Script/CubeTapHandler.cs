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
    public bool isConnected = false;
    private Color[] mainColors;
    Color cubeColor;
    // Hàm này sẽ được gọi từ BoardGrid3D
    public void Init(BoardGrid3D boardRef, Vector3Int pos)
    {
        board = boardRef;
        gridPos = pos;
        isConnected = false;
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
            if (board.CheckAllPrefabColors(currentColor) && gameObject.CompareTag("Specical"))
            {
                firstSelectedCube = this;
                cubeColor = firstSelectedCube.GetComponent<Renderer>().material.color;
                Debug.Log($"[SELECT] Đã chọn cube đầu tiên tại {gridPos} (màu: {currentColor} 1)");
                this.isConnected = true;
                selectedColor = currentColor;
                board.CheckWinCondition();
            }
            else
            {
                Debug.Log("Vui lòng chọn cube có màu  khác ");
            }
        }
        else
        {
            bool hasLine = board.HasDirectLineConnectionToSelected(firstSelectedCube.gridPos,gridPos);
            // Chỉ lưu nếu cube có màu hợp lệ (trong mainColors)
            if ((board.CheckAllPrefabColors(currentColor) && gameObject.CompareTag("Specical") && !hasLine) || (!IsSameColor(currentColor,firstSelectedCube.cubeColor) && gameObject.CompareTag("Specical")))
            {   
                //lấy màu hiện tại của cube khi tap
                cubeColor = firstSelectedCube.GetComponent<Renderer>().material.color;

                Debug.Log($"[SELECT] Đã chọn lại cube đầu tiên tại {gridPos} (màu: {currentColor} , màu: {firstSelectedCube.mainColors} , name : {firstSelectedCube.name}2)");
                selectedColor = currentColor;
                cubeColor = render.material.color;
                string prefabname1 = GetBasePrefabName(this.name);
                firstSelectedCube = this;
                this.isConnected = true;
                board.ResetAllLinesAppearance(prefabname1, cubeColor);
                board.CheckWinCondition();
            }
            else
            {
                string prefabname1 = GetBasePrefabName(this.name);
                string prefabname2 = GetBasePrefabName(firstSelectedCube.name);
                if (gameObject.CompareTag("Bridge"))
                {
                    foreach (Transform child in transform)
                    {
                        string childName = child.name;
                        if (prefabname2.Contains(childName))
                        {
                            
                            Debug.Log($"<color=green>Khớp tên child '{childName}' trong Bridge '{gameObject.name}' với prefabname2 = {prefabname2}</color>");

                            // Ví dụ xử lý child khớp (đổi màu hoặc logic khác)
                            Renderer childRenderer = child.GetComponentInChildren<Renderer>();  // GetInChildren để tìm Renderer con nếu có
                            if (childRenderer != null && childRenderer.material != null)
                            {
                                // ← FIX: CLONE MATERIAL ĐỂ INSTANCE RIÊNG (KHÔNG SHARED)
                                Material newMaterial = new Material(childRenderer.material);  // Clone
                                childRenderer.material = newMaterial;  // Assign instance mới

                                // Đổi màu
                                newMaterial.color = firstSelectedCube.cubeColor;
                                childRenderer.enabled = true;  // Bật nếu ẩn
                                if (board.HightLightLineBetween(firstSelectedCube.gridPos, gridPos, (Color)selectedColor))
                                {
                                    this.isConnected = true;
                                    firstSelectedCube = this;
                                    board.CheckWinCondition();
                                    firstSelectedCube.cubeColor = (Color)selectedColor;
                                    firstSelectedCube.name = prefabname2;
                                }

                                    Debug.Log($"<color=lime>ĐÃ ĐỔI MÀU CHILD KHỚP '{childName}' thành {firstSelectedCube.cubeColor} (old color: {childRenderer.material.color})</color>");
                            }
                        }
                    }
                }
                else
                {
                    if (prefabname1.Contains(prefabname2) || prefabname2.Contains(prefabname1))
                    {
                        if (!gameObject.CompareTag("Specical") && !IsSameColor(currentColor, firstSelectedCube.cubeColor))
                        {
                            Debug.Log($"🔍 VỊ TRÍ GRID: {gridPos} , vị trí firstSelectedCube : {firstSelectedCube.gridPos} , color : {selectedColor}"); // ← IN RA (x,y,z)

                            if (board.HightLightLineBetween(firstSelectedCube.gridPos, gridPos, (Color)selectedColor))
                            {
                                render.material.color = (Color)selectedColor;
                                cubeColor = render.material.color;
                                firstSelectedCube = this;
                                this.isConnected = true;
                                board.CheckWinCondition();
                                Debug.Log($"[SELECT] Đã tap và nối line thành công 3)");
                            }
                           
                        }
                        else
                        {
                            if (!IsSameColor(currentColor, firstSelectedCube.cubeColor))
                            {
                                Debug.Log($"[SELECT] ko thể nối do cùng là specical prefab )");
                            }
                            else
                            {
                                // Highlight cube 1 màu
                                if (board.HightLightLineBetween(firstSelectedCube.gridPos, gridPos, (Color)selectedColor))
                                {
                                    firstSelectedCube = this;
                                    this.isConnected = true;
                                    board.CheckWinCondition();
                                }

                            }
                        }
                    }
                    else
                    {
                        Debug.Log("ko thể nối giữa 2 hình khác nhau");
                    }
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
    // lấy tên game object
    public string GetBasePrefabName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "";

        int atIndex = fullName.IndexOf("_at_(");
        if (atIndex > 0)
        {
            return fullName.Substring(0, atIndex);  // Lấy phần trước "_at_"
        }

        Debug.LogWarning($"<color=yellow>Tên '{fullName}' không chứa '_at_' – trả về full name!</color>");
        return fullName;  // Fallback nếu không có "_at_"
    }
}