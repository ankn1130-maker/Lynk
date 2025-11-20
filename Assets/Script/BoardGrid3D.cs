using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine;
using static UnityEngine.ParticleSystem;
using UnityEngine.Experimental.GlobalIllumination;
using TMPro;



#if UNITY_EDITOR
using UnityEditor; // ← Đảm bảo có dòng này ở trên cùng file
#endif
public class BoardGrid3D : MonoBehaviour
{
    [Header("Grid Config")]
    public int sizeX = 3, sizeY = 1, sizeZ = 3;
    public float cellSize = 1f;
    public Material lineMat;
    public Material boardMat;
    public GameObject piecePrefab;
    public Color highlightColor = Color.yellow;

    public Dictionary<Vector3Int, Transform> intersections = new();
    private GameObject[,,] occupied;
    private GameObject highlightTile;
    private LineRenderer horizontalLines, verticalLines;
    public List<Transform> sphereList = new List<Transform>(); // Danh sách sphere
    public Vector3Int gridPos;
    public Dictionary<string, LineRenderer> lineRenderers = new Dictionary<string, LineRenderer>();
    // Center points
    private Dictionary<Vector2Int, Transform> centerPoints = new Dictionary<Vector2Int, Transform>();
    int index = 0;
    // Biến duy nhất lưu màu
    public Color[] mainColors = { Color.red, Color.blue };
    [Header("Center Points")]
    public bool generateCenterPoints = true;   // bật/tắt
    public float centerPointScale = 0.25f;    // kích thước cube/point ở giữa
    [SerializeField] public string jsonPath = "Assets/Levels/DefaultLevel.json"; // Biến public để LevelEditor có thể gán
    public bool showPointLabels = true;  // Bật/tắt hiển thị tên point trong Inspector
    public Color pointLabelColor = Color.black;  // Màu text
    public float pointLabelScale = 0.2f;  // Kích thước text
    void Start()
    {
        // Siêu chắc ăn: xóa thủ công mọi Line_ còn sót
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Line_"))
                Destroy(child.gameObject);
        }
       
        showPointLabels = false;
        ClearAllLines();
        //ClearAllCubes();
        ClearAllCenterPoints();
        GenerateBoard();
        RebuildAllLines();
        LogAllLines(); // giờ chắc chắn 0 line
        CreateHighlight();
    }
    // 🆕 HÀM MỚI: Map DotData từ JSON thành SpecialDot để dùng ApplySpecialDots

    public void LogAllLines()
    {
        // 🆕 THÊM: Kiểm tra null
        if (lineRenderers == null)
        {
            Debug.LogError("<color=red>lineRenderers is NULL! Khởi tạo dictionary chưa?</color>");
            return;
        }

        if (lineRenderers.Count == 0)
        {
            Debug.Log("<color=orange>lineRenderers rỗng (0 lines)!</color>");
            return;
        }

        Debug.Log($"<color=blue>=== LOG {lineRenderers.Count} LINES TRONG lineRenderers ===</color>");

        foreach (var kvp in lineRenderers)
        {
            string key = kvp.Key;
            LineRenderer lr = kvp.Value;

            // 🆕 THÊM: Kiểm tra lr null
            if (lr == null)
            {
                Debug.LogError($"<color=red>LineRenderer NULL cho key '{key}'! Xóa key này.</color>");
                lineRenderers.Remove(key);
                continue;
            }

            Vector3 start = lr.GetPosition(0);
            Vector3 end = lr.GetPosition(1);
            Color lineColor = lr.material?.color ?? Color.white;  // Null-safe color
            float width = lr.startWidth;  // Giả sử startWidth = endWidth

            string lineName = lr.gameObject.name;  // Tên GameObject

            // Log chi tiết
            Debug.Log($"<color=cyan>[LINE #{lineRenderers.Keys.ToList().IndexOf(key)}]</color> " +
                      $"<color=yellow>Key: {key}</color> | " +
                      $"<color=lime>Name: {lineName}</color> | " +
                      $"Start: {start:F2} ↔ End: {end:F2} | " +
                      $"<color={lineColor}>Màu: {lineColor}</color> | " +
                      $"<color=orange>Width: {width:F3}</color>");

            // 🆕 THÊM: Kiểm tra line có valid (pos khác nhau)
            if (Vector3.Distance(start, end) < 0.01f)
            {
                Debug.LogWarning($"<color=red>Line '{key}' có pos trùng (zero length) – Xóa?</color>");
            }
        }

        Debug.Log($"<color=blue>=== KẾT THÚC LOG {lineRenderers.Count} LINES ===</color>");
    }
    public Vector3Int WorldToGridPos(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x + sizeX / 2f * cellSize) / cellSize);
        int y = Mathf.RoundToInt((worldPos.y + sizeY / 2f * cellSize) / cellSize);
        int z = Mathf.RoundToInt((worldPos.z + sizeZ / 2f * cellSize) / cellSize);
        return new Vector3Int(x, y, z);
    }

    public bool PlacePiece(Vector3 worldPos)
    {
        gridPos = WorldToGridPos(worldPos);
        if (IsValidPosition(gridPos) && occupied[gridPos.x, gridPos.y, gridPos.z] == null)
        {
            // LẤY CUBE TẠI VỊ TRÍ
            Transform cubeTransform = sphereList[gridPos.z * sizeX + gridPos.x]; // index = z*sizeX + x

            // ĐẶT PIECE
            Vector3 worldPiecePos = GridToWorldPos(gridPos);
            GameObject piece = Instantiate(piecePrefab, worldPiecePos + Vector3.up * 0.5f, Quaternion.identity);
            occupied[gridPos.x, gridPos.y, gridPos.z] = piece;

            return true;
        }
        return false;
    }

    public Vector3 GridToWorldPos(Vector3Int gridPos)
    {
        return new Vector3(
            (gridPos.x - sizeX / 2f + 0.5f) * cellSize,  // +0.5f để đặt giữa ô
            (gridPos.y - sizeY / 2f + 0.5f) * cellSize,
            (gridPos.z - sizeZ / 2f + 0.5f) * cellSize
        );
    }

    public bool IsValidPosition(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < sizeX &&
               pos.y >= 0 && pos.y < sizeY &&
               pos.z >= 0 && pos.z < sizeZ;
    }

    // SỬA: Chỉ ShowHighlight nếu valid + có intersection
    public void ShowHighlight(Vector3Int gridPos)
    {
        if (IsValidPosition(gridPos) && intersections.TryGetValue(gridPos, out Transform pos))
        {
            highlightTile.transform.position = pos.position + Vector3.up * 0.01f;
            highlightTile.SetActive(true);
        }
    }

    public void HideHighlight()
    {
        highlightTile.SetActive(false);
    }

    void CreateHighlight()
    {
        // SỬA: Dùng Plane thay Quad → luôn thấy được từ trên xuống
        highlightTile = GameObject.CreatePrimitive(PrimitiveType.Plane);
        highlightTile.transform.localScale = Vector3.one * cellSize * 0.1f; // Plane mặc định 10x10 → scale xuống
        highlightTile.GetComponent<Renderer>().material.color = highlightColor;
        highlightTile.SetActive(false);
        highlightTile.transform.parent = transform;
    }
    public void RebuildAllLines()
    {
        ClearAllLines();  // Xóa cũ
        foreach (var kvp in intersections)
        {
            Vector3Int pos = kvp.Key;
            if (GetPieceAt(pos) != null)
            {
                DrawLinesFromCube(pos);  // Vẽ từ piece có
            }
        }
        Debug.Log($"<color=green>Rebuilt {lineRenderers.Count} dynamic lines!</color>");
    }
    public void GenerateBoard()
    {
        // Tạo plane trắng (giữ nguyên)
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Plane);
        board.transform.parent = transform;
        board.transform.localScale = new Vector3(sizeX * 0.1f, 1, sizeZ * 0.1f);
        Material whiteMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        whiteMat.color = Color.white;
        board.GetComponent<Renderer>().material = whiteMat;

        // XÓA DỮ LIỆU CŨ (giữ nguyên)
        sphereList.Clear();
        intersections.Clear();
        index = 0;
        occupied = new GameObject[sizeX, sizeY, sizeZ];

        Debug.Log("<color=blue>=== BẮT ĐẦU TẠO POINTS (X → Y → Z) ===</color>");

        // TẠO POINT VÀ CUBE (giữ nguyên, nhưng bật renderer tạm để test – sau tắt lại)
        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                {
                    Vector3Int gridPos = new Vector3Int(x, y, z);
                    Vector3 worldPos = GridToWorldPos(gridPos);
                    GameObject point = new GameObject($"Point_{x}_{y}_{z}");
                    point.transform.parent = transform;
                    point.transform.position = worldPos;
                    intersections[gridPos] = point.transform;

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(point.transform);
                    cube.transform.localPosition = Vector3.zero;
                    cube.transform.localScale = Vector3.one * 0.3f;
                    cube.GetComponent<Renderer>().enabled = false;  // ← SỬA TẠM: BẬT ĐỂ THẤY (sau test tắt = false)
                    cube.GetComponent<Renderer>().material.color = Color.gray;  // Màu xám để phân biệt
                    cube.GetComponent<BoxCollider>().enabled = false;
                    sphereList.Add(cube.transform);
                    // ← THÊM MỚI: TEXT LABEL CHO POINT (hiển thị "(x,y,z)")
                    //if (showPointLabels)
                    //{
                    //    CreatePointLabel(point, gridPos);  // Gọi hàm hỗ trợ dưới
                    //}
                    index++;
                    Debug.Log($"<color=cyan>[{index:00}] TẠO POINT: {point.name} | Grid: {gridPos} | World: {worldPos:F2}</color>");
                }

        // 🆕 SỬA: CENTER POINTS WORLD POS SAI (z = x → z)
        if (generateCenterPoints)
        {
            for (int x = 0; x < sizeX - 1; x++)
                for (int z = 0; z < sizeZ - 1; z++)
                {
                    Vector3 centerGrid = new Vector3(x + 0.5f, 0, z + 0.5f);
                    // 🆕 SỬA: Tính worldPos đúng (trung bình 4 góc)
                    Vector3 worldPos = (GridToWorldPos(new Vector3Int(x, 0, z)) + GridToWorldPos(new Vector3Int(x + 1, 0, z)) +
                                       GridToWorldPos(new Vector3Int(x, 0, z + 1)) + GridToWorldPos(new Vector3Int(x + 1, 0, z + 1))) * 0.25f;

                    GameObject centerObj = new GameObject($"Center_{x}_{z}");
                    centerObj.transform.parent = transform;
                    centerObj.transform.position = worldPos;

                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(centerObj.transform);
                    cube.transform.localPosition = Vector3.zero;
                    cube.transform.localScale = Vector3.one * centerPointScale;
                    cube.GetComponent<Renderer>().enabled = false;  // ← TẠM BẬT ĐỂ THẤY
                    cube.GetComponent<Renderer>().material.color = Color.gray;  // Màu xanh để phân biệt
                    cube.GetComponent<BoxCollider>().enabled = false;
                    centerPoints[new Vector2Int(x, z)] = centerObj.transform;

                    Debug.Log($"<color=magenta>Center {x}_{z} tại {worldPos:F2}</color>");
                }
        }

        Debug.Log($"<color=blue>=== HOÀN THÀNH {index} POINTS + {centerPoints.Count} CENTERS ===</color>");

        // KHÔNG VẼ GRID LINE (comment)
        // CreateGridLines();

        Debug.Log("<color=cyan>Board generated - Ready for dynamic lines</color>");
    }
   
    public void ClearAllCenterPoints()
    {
        List<GameObject> centersToDelete = new List<GameObject>();

        foreach (var kvp in centerPoints)
        {
            if (kvp.Value != null)
                centersToDelete.Add(kvp.Value.gameObject);
        }

        XoaDanhSachObject(centersToDelete);

        centerPoints.Clear();
        Debug.Log($"<color=purple>ĐÃ XÓA HẾT {centersToDelete.Count} CENTER POINT!</color>");
    }
    private void XoaDanhSachObject(List<GameObject> list)
    {
        foreach (GameObject obj in list)
        {
            if (obj == null) continue;

#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(obj);
            else
                Undo.DestroyObjectImmediate(obj);   // ← Ctrl+Z hoạt động ngon lành
#else
        Destroy(obj);
#endif
        }
    }
    // 🆕 HÀM MỚI: Clear TẤT CẢ lines (gọi khi regenerate để hủy gap lines)
    public void ClearAllLines()
    {
        // Cách chắc ăn nhất: tìm và xóa trực tiếp mọi object có tên bắt đầu bằng Line_
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Line_"))
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    Undo.DestroyObjectImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
            }
        }

        lineRenderers.Clear();
        Debug.Log("<color=red>ĐÃ XÓA HOÀN TOÀN MỌI LINE TRONG HIỆRARCHY!</color>");
    }


    // 🆕 HÀM MỚI: KIỂM TRA ADJACENT TRỰC TIẾP (CÁCH ĐÚNG 1 GRID, BAO GỒM CHÉO)
    public bool IsAdjacent(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.z - b.z);
        // Adjacent: ngang/dọc (dx=1 dz=0 hoặc dx=0 dz=1) HOẶC chéo (dx=1 dz=1)
        // Không gap: Không cho dx>1 hoặc dz>1

        return (dx <= 1 && dz <= 1) && (dx + dz == 1 || (dx == 1 && dz == 1));
    }

    public void DrawLinesFromCube(Vector3Int center)
    {
        GameObject pieceA = GetPieceAt(center);
        if (pieceA == null) return;

        Transform a = pieceA.transform;

        Vector3Int[] directions = {
        new Vector3Int(1,0,0), new Vector3Int(-1,0,0),  // Ngang
        new Vector3Int(0,0,1), new Vector3Int(0,0,-1),  // Dọc
        new Vector3Int(1,0,1), new Vector3Int(-1,0,1),  // Chéo phải
        new Vector3Int(1,0,-1), new Vector3Int(-1,0,-1) // Chéo trái
    };

        string[] names = { "PHẢI", "TRÁI", "XUỐNG", "LÊN", "CHÉO↘", "CHÉO↙", "CHÉO↗", "CHÉO↖" };

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3Int neighbor = center + directions[i];
            if (!IsValidPosition(neighbor))
            {
                continue;
            }

            GameObject pieceB = GetPieceAt(neighbor);
            if (pieceB == null) continue;

            string key = GetLineKey(center, neighbor);
            if (lineRenderers.ContainsKey(key)) continue;

            if (!ArePointsAdjacent(center, neighbor)) continue;

            // 🆕 SỬA: Nếu hướng chéo (i >= 4), vẽ qua center thay vì trực tiếp
            if (i >= 4)  // Chéo (4-7)
            {
                Vector2Int centerKey = GetCenterKeyForDiagonal(center, neighbor);  // Hàm mới dưới
                if (centerPoints.TryGetValue(centerKey, out Transform centerTrans))
                {
                    // ← THÊM: KIỂM TRA MÀU CENTER CÓ PHẢI GRAY KHÔNG
                    Renderer centerRenderer = centerTrans.GetChild(0)?.GetComponent<Renderer>();
                    Color centerColor = centerRenderer?.material?.color ?? Color.white;
                    if (!IsColorGray(centerColor))
                    {
                        Debug.Log($"<color=red>SKIP NỐI CHÉO {center} ↔ {neighbor}: Center '{centerTrans.name}' có màu {centerColor} (không phải gray)!</color>");
                        continue;  // Skip vẽ line chéo
                    }
                    else
                    {
                        Debug.Log("Vẫn tiến hành nối line chéoooooooooooooooooooooooooooooooooooooooooooo");
                    }

                        // Vẽ 2 line: center → a, center → b (thay 1 line chéo)
                        string key1 = $"C{centerKey.x}_{centerKey.y}_{center.x}_{center.z}-{GetCornerLabel(center, neighbor, true)}";
                    CreateLineBetween(a, centerTrans, key1);

                    string key2 = $"C{centerKey.x}_{centerKey.y}_{neighbor.x}_{neighbor.z}-{GetCornerLabel(center, neighbor, false)}";
                    CreateLineBetween(pieceB.transform, centerTrans, key2);

                    
                    Debug.Log($"<color=lime>CHÉO QUA CENTER: {key1} & {key2} | {center} ↔ Center ↔ {neighbor}</color>");
                    continue;  // Skip line trực tiếp
                }
            }

            // Line thường (ngang/dọc)
            CreateLineBetween(a, pieceB.transform, key);
            Debug.Log($"<color=lime>LINE THƯỜNG: {key} | {center} → {neighbor}</color>");
        }
    }

    // 🆕 HÀM HỖ TRỢ: Lấy key center cho chéo
    public Vector2Int GetCenterKeyForDiagonal(Vector3Int pos1, Vector3Int pos2)
    {
        int minX = Mathf.Min(pos1.x, pos2.x);
        int minZ = Mathf.Min(pos1.z, pos2.z);
        return new Vector2Int(minX, minZ);  // Center của ô bao quanh
    }

    // 🆕 HÀM HỖ TRỢ: Label góc (TL/TR/BL/BR)
    private string GetCornerLabel(Vector3Int pos1, Vector3Int pos2, bool isFirst)
    {
        Vector3Int corner = isFirst ? pos1 : pos2;
        if (corner.x == pos1.x && corner.z == pos1.z) return "TL";
        if (corner.x == pos2.x && corner.z == pos1.z) return "TR";
        if (corner.x == pos1.x && corner.z == pos2.z) return "BL";
        return "BR";
    }

    // 🆕 HÀM MỚI: Kiểm tra 2 vị trí Point có ở cạnh nhau không, nếu có thì nối line
    public bool ConnectIfAdjacent(Vector3Int pos1, Vector3Int pos2)
    {
        // Bước 1: Kiểm tra valid position
        if (!IsValidPosition(pos1) || !IsValidPosition(pos2))
        {
            Debug.LogError($"<color=red>INVALID POS: {pos1} hoặc {pos2} (ngoài board)</color>");
            return false;
        }

        // Bước 2: Kiểm tra point tồn tại tại 2 vị trí
        if (!intersections.TryGetValue(pos1, out Transform point1) || !intersections.TryGetValue(pos2, out Transform point2))
        {
            Debug.LogWarning($"<color=yellow>NO POINT tại {pos1} hoặc {pos2}!</color>");
            return false;
        }

        // Bước 3: Kiểm tra có cube/piece tại 2 vị trí (GetPieceAt)
        GameObject piece1 = GetPieceAt(pos1);
        GameObject piece2 = GetPieceAt(pos2);
        if (piece1 == null || piece2 == null)
        {
            Debug.Log($"<color=gray>Skip nối {pos1} ↔ {pos2}: Không có piece tại một vị trí!</color>");
            return false;
        }

        // Bước 4: Kiểm tra adjacent (không gap)
        int dx = Mathf.Abs(pos1.x - pos2.x);
        int dz = Mathf.Abs(pos1.z - pos2.z);
        bool isAdjacent = (dx <= 1 && dz <= 1) && (dx + dz == 1 || (dx == 1 && dz == 1));  // Ngang/dọc/chéo
        if (!isAdjacent)
        {
            Debug.Log($"<color=red>Skip nối {pos1} ↔ {pos2}: Không cạnh nhau (gap! dx={dx}, dz={dz})</color>");
            return false;
        }

        // Bước 5: Nối line nếu OK
        string key = GetLineKey(pos1, pos2);
        if (lineRenderers.ContainsKey(key))
        {
            Debug.Log($"<color=yellow>Line {key} đã tồn tại giữa {pos1} ↔ {pos2}</color>");
            return true;  // Vẫn coi là OK
        }

        CreateLineBetween(piece1.transform, piece2.transform, key);
        Debug.Log($"<color=lime>NỐI THÀNH CÔNG: {key} | {pos1} ↔ {pos2} (adjacent OK)</color>");
        return true;
    }

    // 🆕 HÀM MỚI: Kiểm tra 2 điểm Point (vị trí đặt Cube) có ở cạnh nhau không
    public bool ArePointsAdjacent(Vector3Int gridPos1, Vector3Int gridPos2)
    {
        // Bước 1: Kiểm tra valid position
        if (!IsValidPosition(gridPos1) || !IsValidPosition(gridPos2))
        {
            Debug.LogError($"<color=red>INVALID GRID POS: {gridPos1} hoặc {gridPos2} (ngoài board {sizeX}x{sizeZ})</color>");
            return false;
        }

        // Bước 2: Kiểm tra point tồn tại
        if (!intersections.TryGetValue(gridPos1, out Transform point1) || !intersections.TryGetValue(gridPos2, out Transform point2))
        {
            Debug.LogWarning($"<color=yellow>NO POINT tại {gridPos1} hoặc {gridPos2}! (Kiểm tra GenerateBoard đã chạy chưa?)</color>");
            return false;
        }

        // Bước 3: Log vị trí world của 2 points để debug
        Vector3 worldPos1 = point1.position;
        Vector3 worldPos2 = point2.position;
        float distance = Vector3.Distance(worldPos1, worldPos2);
        Debug.Log($"<color=cyan>Point 1 ({gridPos1}): WorldPos = {worldPos1:F2} | Point 2 ({gridPos2}): WorldPos = {worldPos2:F2} | Distance = {distance:F2}</color>");

        // Bước 4: Kiểm tra adjacent dựa trên grid (không gap)
        int dx = Mathf.Abs(gridPos1.x - gridPos2.x);
        int dz = Mathf.Abs(gridPos2.z - gridPos1.z);  // Sửa dz nếu cần
        bool isAdjacent = (dx <= 1 && dz <= 1) && (dx + dz == 1 || (dx == 1 && dz == 1));  // Ngang/dọc/chéo strict

        if (isAdjacent)
        {
            Debug.Log($"<color=green>✅ Điểm Point {gridPos1} và {gridPos2} ở CẠNH NHAU (adjacent OK, dx={dx}, dz={dz})</color>");
        }
        else
        {
            Debug.Log($"<color=red>❌ Điểm Point {gridPos1} và {gridPos2} KHÔNG ở cạnh nhau (có gap! dx={dx}, dz={dz})</color>");
        }

        return isAdjacent;
    }

    public GameObject GetPieceAt(Vector3Int pos)
    {
        string searchName = $"_at_({pos.x}, {pos.y}, {pos.z})";
        foreach (Transform child in transform)
        {
            if (child.name.Contains(searchName) &&
                !child.name.StartsWith("Point_") &&
                !child.name.StartsWith("Line_"))
            {
                return child.gameObject;
            }
        }
        return null;
    }

    public void CreateGridLines()
    {
        lineRenderers.Clear();
        Debug.Log("<color=blue>=== TẠO GRID LINES (NGANG/DỌC + 4 LINE CHÉO TỪ CENTER) ===</color>");

        // Duyệt tất cả các ô (x, z) – giữ nguyên ngang/dọc
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector3Int current = new Vector3Int(x, 0, z);
                Transform a = intersections[current];

                // === 1. NỐI PHẢI (NGANG) – Giữ nguyên ===
                if (x < sizeX - 1)
                {
                    Vector3Int right = new Vector3Int(x + 1, 0, z);
                    Transform b = intersections[right];
                    string key = GetLineKey(current, right);
                    CreateLineBetween(a, b, key);
                    Debug.Log($"<color=green>NGANG: {key}</color>");
                }

                // === 2. NỐI XUỐNG (DỌC) – Giữ nguyên ===
                if (z < sizeZ - 1)
                {
                    Vector3Int down = new Vector3Int(x, 0, z + 1);
                    Transform b = intersections[down];
                    string key = GetLineKey(current, down);
                    CreateLineBetween(a, b, key);
                    Debug.Log($"<color=green>DỌC: {key}</color>");
                }
            }
        }

        // 🆕 SỬA: THÊM 4 LINE CHÉO TỪ CENTER ĐẾN 4 GÓC (thay vì 2 line chéo trực tiếp)
        if (generateCenterPoints)
        {
            for (int x = 0; x < sizeX - 1; x++)  // Duyệt từng ô 1x1
            {
                for (int z = 0; z < sizeZ - 1; z++)
                {
                    // Lấy center transform (nếu không có, tạo tạm hoặc skip)
                    if (!centerPoints.TryGetValue(new Vector2Int(x, z), out Transform centerTrans))
                    {
                        Debug.LogWarning($"<color=yellow>No center at {x}_{z}, skip chéo</color>");
                        continue;
                    }

                    // 4 GÓC CỦA Ô NÀY
                    Vector3Int[] corners = {
                    new Vector3Int(x, 0, z),      // Góc trên-trái
                    new Vector3Int(x + 1, 0, z),  // Góc trên-phải
                    new Vector3Int(x, 0, z + 1),  // Góc dưới-trái
                    new Vector3Int(x + 1, 0, z + 1)  // Góc dưới-phải
                };

                    string[] cornerNames = { "TL", "TR", "BL", "BR" };  // Top-Left, Top-Right, Bottom-Left, Bottom-Right

                    for (int i = 0; i < corners.Length; i++)
                    {
                        Vector3Int corner = corners[i];
                        if (!intersections.TryGetValue(corner, out Transform cornerTrans))
                        {
                            Debug.LogWarning($"<color=yellow>No corner at {corner}, skip</color>");
                            continue;
                        }

                        // Tính key cho line từ center đến corner
                        string key = GetLineKey(corner, new Vector3Int(x, 0, z));  // Dùng grid approx cho center (x,z)
                        key += $"_C{cornerNames[i]}";  // Phân biệt (ví dụ: 0_0-0_0_C TL)

                        if (lineRenderers.ContainsKey(key))
                        {
                            Debug.Log($"<color=orange>Line chéo {key} đã tồn tại</color>");
                            continue;
                        }

                        // Tạo line từ corner đến center
                        CreateLineBetween(cornerTrans, centerTrans, key);
                        Debug.Log($"<color=lime>CHÉO 4 LINE: {key} | Corner {cornerNames[i]} ({corner}) → Center ({x + 0.5f}, {z + 0.5f})</color>");
                    }
                }
            }
        }
        else
        {
            Debug.Log("<color=yellow>generateCenterPoints = false → Không tạo 4 line chéo từ center</color>");
        }

        Debug.Log($"<color=magenta>ĐÃ TẠO {lineRenderers.Count} LINES (NGANG/DỌC + 4 CHÉO/CENTER)!</color>");
    }
    public string GetLineKey(Vector3Int a, Vector3Int b)
    {
        int dx = b.x - a.x;
        int dz = b.z - a.z;

        // Phân biệt 2 đường chéo
        if (Mathf.Abs(dx) == 1 && Mathf.Abs(dz) == 1)
        {
            // Dùng tọa độ bắt đầu + hướng
            string dir = dx > 0 ? "R" : "L";
            string dirZ = dz > 0 ? "D" : "U";
            return $"{a.x}_{a.z}_{dir}{dirZ}"; // Ví dụ: 0_0_RD, 1_0_LU
        }

        // Ngang/dọc: dùng min-max như cũ
        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minZ = Mathf.Min(a.z, b.z);
        int maxZ = Mathf.Max(a.z, b.z);
        return $"{minX}_{minZ}-{maxX}_{maxZ}";
    }

    // === HÀM HỖ TRỢ: TẠO LINE RENDERER ===
    void CreateLineBetween(Transform a, Transform b, string key)
    {
        // 🆕 THÊM: Check trùng pos với line cũ (tránh chồng line chéo)
        Vector3 posA = a.position;
        Vector3 posB = b.position;
        bool isDuplicate = false;
        foreach (var kvp in lineRenderers)
        {
            Vector3 existingP0 = kvp.Value.GetPosition(0);
            Vector3 existingP1 = kvp.Value.GetPosition(1);
            if ((Vector3.Distance(posA, existingP0) < 0.01f && Vector3.Distance(posB, existingP1) < 0.01f) ||
                (Vector3.Distance(posA, existingP1) < 0.01f && Vector3.Distance(posB, existingP0) < 0.01f))
            {
                isDuplicate = true;
                Debug.Log($"<color=yellow>Skip CreateLineBetween: Pos trùng line cũ {kvp.Key}</color>");
                break;
            }
        }
        if (isDuplicate || lineRenderers.ContainsKey(key)) return;  // Skip nếu trùng

        GameObject lineObj = new GameObject($"Line_{key}");
        lineObj.transform.parent = transform;
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = lineMat ?? new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = Color.black;
        lr.startWidth = lr.endWidth = 0.02f;
        lr.useWorldSpace = true;
        lr.SetPosition(0, posA);
        lr.SetPosition(1, posB);
        lineRenderers[key] = lr;
        Debug.Log($"<color=lime>Tạo line mới: {key} ({posA:F1} ↔ {posB:F1})</color>");
    }

    // SỬA: HasDirectLineConnection - Chỉ true nếu adjacent (bao gồm chéo), không gap
    public bool HasDirectLineConnection(Vector3Int a, Vector3Int b)
    {
        // 🆕 THAY ĐỔI: Check strict adjacent trước, rồi mới xem line có tồn tại
        if (!IsAdjacent(a, b)) return false;

        string key = GetLineKey(a, b);
        return lineRenderers.ContainsKey(key);
    }

    // 2. SỬA HÀM HIGHLIGHT ĐỂ DÙNG KEY MỚI
    public bool HightLightLineBetween(Vector3Int a, Vector3Int b, Color color)
    {
        if (!IsAdjacent(a, b))
        {
            Debug.Log($"<color=red>Không adjacent (gap hoặc xa): {a} ↔ {b}</color>");
            return false;
        }
        Debug.Log($"<color=cyan>Đang highlight từ {a} ↔ {b}</color>");

        // ================== TRƯỜNG HỢP NGANG / DỌC ==================
        if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z) == 1)
        {
            string key = GetLineKey(a, b);
            if (lineRenderers.TryGetValue(key, out LineRenderer lr))
            {
                lr.material.color = color;
                lr.startWidth = lr.endWidth = 0.08f;
                Debug.Log($"<color=green>ĐÃ TÔ MÀU LINE THƯỜNG: <b>{key}</b></color>");
                return true;  // ← RETURN TRUE: Nối thành công
            }
            Debug.Log($"<color=red>Không tìm thấy line thường: {key}</color>");
            return false;  // ← RETURN FALSE: Không nối
        }

        // ================== TRƯỜNG HỢP CHÉO ==================
        if (Mathf.Abs(a.x - b.x) == 1 && Mathf.Abs(a.z - b.z) == 1)
        {
            Vector2Int centerKey = GetCenterKeyForDiagonal(a, b);
            string prefix1 = $"C{centerKey.x}_{centerKey.y}_{a.x}_{a.z}-";
            string prefix2 = $"C{centerKey.x}_{centerKey.y}_{b.x}_{b.z}-";

            if (centerPoints.TryGetValue(centerKey, out Transform centerTrans))
            {
                Renderer centerRenderer = centerTrans.GetChild(0)?.GetComponent<Renderer>(); // Cube con của center
                if (centerRenderer != null && centerRenderer.material != null)
                {
                    if (!IsColorGray(centerRenderer.material.color))
                    {
                        Debug.Log($"<color=red>HỦY HIGHLIGHT LINE CHÉO {a} ↔ {b}: Center '{centerTrans.name}' có màu {centerRenderer.material.color} (không phải gray)!</color>");
                        return false;  // ← RETURN FALSE: Không nối thành công
                    }
                }
                else
                {
                    Debug.LogWarning($"<color=orange>Không tìm thấy Renderer cho center '{centerTrans.name}'!</color>");
                    return false;  // ← RETURN FALSE
                }
            }
            else
            {
                Debug.LogWarning($"<color=orange>Không tìm thấy center cho chéo {a} ↔ {b}</color>");
                return false;  // ← RETURN FALSE
            }

            int count = 0;
            foreach (var kvp in lineRenderers)
            {
                string key = kvp.Key;
                if (key.StartsWith(prefix1) || key.StartsWith(prefix2))
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.material.color = color;
                        kvp.Value.startWidth = kvp.Value.endWidth = 0.08f;
                        count++;
                        Debug.Log($"<color=lime>ĐÃ TÔ ĐOẠN CHÉO: <b>{key}</b></color>");
                    }
                }
            }

            if (count > 0)
            {
                // Đổi màu center theo color (vì center gray OK)
                Renderer centerRenderer = centerTrans.GetChild(0)?.GetComponent<Renderer>();
                if (centerRenderer != null && centerRenderer.material != null)
                {
                    centerRenderer.material.color = color;
                    centerRenderer.enabled = false;
                    Debug.Log($"<color=magenta>ĐÃ ĐỔI MÀU CENTER '{centerTrans.name}' thành {color} khi highlight chéo {a} ↔ {b}</color>");
                }

                Debug.Log($"<color=green>ĐÃ TÔ THÀNH CÔNG <b>{count}/2</b> ĐOẠN CHÉO + CENTER từ {a} ↔ {b}!</color>");
                return true;  // ← RETURN TRUE: Nối thành công
            }

            Debug.Log($"<color=red>Không tìm thấy 2 đoạn line chéo để tô: {a} ↔ {b}</color>");
            return false;  // ← RETURN FALSE: Không nối
        }

        Debug.Log($"<color=red>Không xác định được loại line giữa {a} ↔ {b}</color>");
        return false;  // ← RETURN FALSE: Không nối
    }

    public void ResetAllLinesAppearance(string name, Color color)
    {
        if (lineRenderers == null || lineRenderers.Count == 0)
            return;

        int count = 0;
        foreach (var lr in lineRenderers.Values)
        {
            if (lr != null && lr.material.color==color)
            {
                lr.material.color = Color.gray;
                lr.startWidth = 0.01f;
                lr.endWidth = 0.01f;
                count++;
            }
        }

        int cubeCount = 0;
        int specialCubeCount = 0;
        Color defaultCubeColor = Color.gray;  // Màu ban đầu cho cube (thay đổi nếu cần, ví dụ Color.gray)
        foreach (Transform child in transform)
        {
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
            // Chỉ reset prefab piece (cube), bỏ qua Point_, Line_, Center_, BoardPlane, Highlight
            if (!child.name.StartsWith("Point_") &&
                !child.name.StartsWith("Line_") &&
                !child.name.StartsWith("Center_") &&
                child.name != "BoardPlane" &&
                child != highlightTile?.transform &&
                child.name.Contains("_at_"))  // Đảm bảo là prefab spawn từ SpawnPrefabAt
            {
                if (child.CompareTag("Specical"))
                {
                    // ← THÊM: Nếu tag "Special" thì KHÔNG ĐỔI MÀU (giữ nguyên màu đặc biệt)
                    specialCubeCount++;
                    continue;
                }
                foreach (var rend in renderers)
                {
                    if (rend.material != null && child.name.Contains(name) && rend.material.color==color)
                    {
                        rend.material.color = defaultCubeColor;
                    }
                }
                cubeCount++;
            }
            Debug.Log($"name : {name} ,childnaem : {child.name}");
        }

        Debug.Log($"<color=cyan>ĐÃ RESET {count} LINE VỀ MÀU + WIDTH BAN ĐẦU!</color>");
    }

    public GameObject SpawnPrefabAt(Vector3Int gridPos, GameObject prefab)
    {
        if (prefab == null || !IsValidPosition(gridPos))
        {
            Debug.LogError($"<color=red>Spawn thất bại: Prefab null hoặc vị trí {gridPos} không hợp lệ!</color>");
            return null;
        }
        // Sử dụng intersections để lấy vị trí chính xác (fix bug index)
        if (!intersections.TryGetValue(gridPos, out Transform pointTransform))
        {
            Debug.LogError($"<color=red>Spawn thất bại: Không tìm thấy Point tại {gridPos}!</color>");
            return null;
        }
        Vector3 worldPos = pointTransform.position; // Hoặc + Vector3.up * 0.4f nếu cần offset
        GameObject instance = null;
        if (Application.isPlaying)
        {
            instance = Instantiate(prefab, worldPos, Quaternion.identity, transform);
        }
        else
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
            if (instance != null)
            {
                instance.transform.position = worldPos;
            }
        }
        if (instance != null)
        {
            // ← THÊM ĐOẠN NÀY: CĂN GIỮA VÀ SCALE GIỐNG CUBE
        

            instance.name = $"{prefab.name}_at_{gridPos}";

            CubeTapHandler handler = instance.GetComponent<CubeTapHandler>();
            if (handler == null)
            {
                handler = instance.AddComponent<CubeTapHandler>();  // Add nếu chưa có trên prefab
            }
            handler.Init(this, gridPos);  // Gọi Init để set board + gridPos (bỏ qua Start())
            handler.enabled = true;  // Enable nếu disabled
            handler.isConnected = false;

            // 🆕 THÊM: Log kiểm tra spawn thành công + handler
            Debug.Log($"<color=green>✅ Spawn thành công: '{prefab.name}' tại {gridPos} (Point: {pointTransform.name}, World Pos: {worldPos:F2}) + Handler inited</color>");
            // Xóa line cũ và vẽ mới (giữ nguyên logic)
            RemoveLinesAtPosition(gridPos);
            DrawLinesFromCube(gridPos);
            Vector3Int[] dirs = {
            new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1),
            new Vector3Int(1,0,1), new Vector3Int(-1,0,1),
            new Vector3Int(1,0,-1), new Vector3Int(-1,0,-1)
        };
            foreach (var dir in dirs)
            {
                Vector3Int neighbor = gridPos + dir;
                if (IsValidPosition(neighbor) && IsAdjacent(gridPos, neighbor) && HasPrefabAt(neighbor))
                {
                    DrawLinesFromCube(neighbor);
                }
            }
            return instance;
        }
        else
        {
            // 🆕 THÊM: Log kiểm tra spawn thất bại
            Debug.LogError($"<color=red>❌ Spawn thất bại: Không tạo được instance cho '{prefab.name}' tại {gridPos}!</color>");
            return null;
        }
    }

    // 🆕 HÀM MỚI: Xóa line liên quan đến một vị trí (gọi trước spawn)
    public void RemoveLinesAtPosition(Vector3Int pos)
    {
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in lineRenderers)
        {
            Vector3 p0 = kvp.Value.GetPosition(0);
            Vector3 p1 = kvp.Value.GetPosition(1);
            Vector3 centerPos = intersections[pos].position;  // Dùng intersection pos để check
            if (Vector3.Distance(p0, centerPos) < 0.1f || Vector3.Distance(p1, centerPos) < 0.1f)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            if (Application.isPlaying)
                Destroy(lineRenderers[key].gameObject);
            else
                DestroyImmediate(lineRenderers[key].gameObject);
            lineRenderers.Remove(key);
        }
    }

    public bool HasPrefabAt(Vector3Int pos)
    {
        string searchName = $"_at_({pos.x}, {pos.y}, {pos.z})"; // ĐÚNG ĐỊNH DẠNG!
        foreach (Transform child in transform)
        {
            if (child.name.Contains(searchName) &&
                !child.name.StartsWith("Point_") &&
                !child.name.StartsWith("Line_"))
            {
                return true;
            }
        }
        return false;
    }

    
    public void RemovePrefabAndLines(Vector3Int gridPos)
    {
        // Xóa prefab
        string searchName = $"_at_{gridPos}";
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains(searchName) && !child.name.StartsWith("Point_") && !child.name.StartsWith("Line_"))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        // Xóa line liên quan
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in lineRenderers)
        {
            Vector3 p0 = kvp.Value.GetPosition(0);
            Vector3 p1 = kvp.Value.GetPosition(1);
            Vector3 center = intersections[gridPos].position + Vector3.up * 0.02f;
            if (Vector3.Distance(p0, center) < 0.1f || Vector3.Distance(p1, center) < 0.1f)
                keysToRemove.Add(kvp.Key);
        }

        foreach (var key in keysToRemove)
        {
            if (Application.isPlaying)
                Destroy(lineRenderers[key].gameObject);
            else
                DestroyImmediate(lineRenderers[key].gameObject);
            lineRenderers.Remove(key);
        }
    }

    internal void RemoveLineByKey(string key)
    {
        if (lineRenderers.TryGetValue(key, out LineRenderer lr))
        {
            if (Application.isPlaying)
                Destroy(lr.gameObject);
            else
                DestroyImmediate(lr.gameObject);
            lineRenderers.Remove(key);
            Debug.Log($"<color=orange>Removed line {key}</color>");
        }
        else
        {
            Debug.LogWarning($"<color=yellow>No line with key {key}</color>");
        }
    }

    //kiểm tra đường line nối giữa 2 gameobject
    public bool HasDirectLineConnectionToSelected(Vector3Int pos1, Vector3Int pos2)
    {
        if (!IsAdjacent(pos1, pos2)) return false;

        // LINE THƯỜNG (ngang/dọc)
        if (Mathf.Abs(pos1.x - pos2.x) + Mathf.Abs(pos1.z - pos2.z) == 1)
        {
            string key = GetLineKey(pos1, pos2);
            return lineRenderers.ContainsKey(key);
        }

        // LINE CHÉO (2 đoạn) – DÙNG CÁCH 1 (centerKey đầy đủ để tránh trùng)
        if (Mathf.Abs(pos1.x - pos2.x) == 1 && Mathf.Abs(pos1.z - pos2.z) == 1)
        {
            Vector2Int centerKey = GetCenterKeyForDiagonal(pos1, pos2);
            string prefix1 = $"C{centerKey.x}_{centerKey.y}_{pos1.x}_{pos1.z}-";
            string prefix2 = $"C{centerKey.x}_{centerKey.y}_{pos2.x}_{pos2.z}-";

            bool hasSegment1 = false, hasSegment2 = false;
            foreach (var kvp in lineRenderers)
            {
                if (kvp.Key.StartsWith(prefix1)) hasSegment1 = true;
                if (kvp.Key.StartsWith(prefix2)) hasSegment2 = true;
            }

            bool hasDiagonal = hasSegment1 && hasSegment2;
            Debug.Log(hasDiagonal ? $"<color=green>✅ LINE CHÉO TỒN TẠI: {prefix1} & {prefix2}</color>" : $"<color=red>LINE CHÉO KHÔNG TỒN TẠI</color>");
            return hasDiagonal;
        }

        return false;
    }

    // kiểm tra các màu có trong level
    public bool CheckAllPrefabColors(Color targetColor)
    {
        if (transform.childCount == 0)
        {
            Debug.Log("<color=yellow>Không có prefab nào trên board để kiểm tra!</color>");
            return false;
        }

        bool hasTargetColor = false;
        List<Color> allPrefabColors = new List<Color>();  // Lưu tất cả màu để log
        int prefabCount = 0;

        foreach (Transform child in transform)
        {
            // Chỉ kiểm tra prefab spawn (tên chứa "_at_", không phải Point/Line/Center/BoardPlane)
            if (child.name.Contains("_at_") &&
                !child.name.StartsWith("Point_") &&
                !child.name.StartsWith("Line_") &&
                !child.name.StartsWith("Center_") &&
                child.name != "BoardPlane")
            {
                prefabCount++;
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                bool hasRendererColor = false;

                foreach (var rend in renderers)
                {
                    if (rend == null || rend.material == null)
                    {
                        Debug.LogWarning($"<color=orange>Renderer hoặc material null trong prefab '{child.name}'!</color>");
                        continue;  // Skip nếu null
                    }

                    Color prefabColor = rend.material.color;

                    // So sánh với tolerance 0.01f (an toàn cho màu RGB 0-1)
                    bool colorMatch = Mathf.Approximately(prefabColor.r, targetColor.r) &&
                                      Mathf.Approximately(prefabColor.g, targetColor.g) &&
                                      Mathf.Approximately(prefabColor.b, targetColor.b) &&
                                      Mathf.Approximately(prefabColor.a, targetColor.a);

                    if (colorMatch)
                    {
                        hasTargetColor = true;
                        Debug.Log($"<color=green>✅ TÌM THẤY MÀU KHỚP '{targetColor}' trong prefab '{child.name}'!</color>");
                    }

                    Debug.Log($"<color=cyan>Prefab '{child.name}': Màu = {prefabColor} (so với target {targetColor}) | Match: {colorMatch}</color>");
                }

                if (!hasRendererColor)
                    Debug.Log($"<color=orange>Prefab '{child.name}' không có Renderer hoặc material!</color>");
            }
        }

        // Log tóm tắt
        Debug.Log($"<color=green>=== TÓM TẮT KIỂM TRA MÀU '{targetColor}' ===");
        Debug.Log(hasTargetColor ? $"<color=lime>✅ MÀU '{targetColor}' TỒN TẠI trong {prefabCount} prefab!</color>" : $"<color=red>❌ MÀU '{targetColor}' KHÔNG TỒN TẠI trong {prefabCount} prefab.</color>");
        Debug.Log($"<color=cyan>Tất cả màu prefab: {string.Join(", ", allPrefabColors.Distinct().Select(c => c.ToString("F2")))}</color>");
        Debug.Log($"<color=green>=== KẾT THÚC ===</color>");

        return hasTargetColor;
    }

    public bool CheckWinCondition()
    {
        List<Transform> specialPrefabs = new List<Transform>();
        List<Transform> allPrefabs = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.Contains("_at_") &&
                !child.name.StartsWith("Point_") &&
                !child.name.StartsWith("Line_") &&
                !child.name.StartsWith("Center_") &&
                child.name != "BoardPlane") 
            {
                if (child.CompareTag("Specical")){
                    specialPrefabs.Add(child);
                }
                allPrefabs.Add(child);
            }
        }

        if (allPrefabs.Count == 0)
        {
            Debug.Log("<color=yellow>Không có cube để kiểm tra!</color>");
            return false;  // Hoặc true tùy logic game
        }
        bool allConnected = true;
        bool SpecicalConnected = true;
        int unconnectedCount = 0;
        foreach (Transform prefab in allPrefabs)
        {
            CubeTapHandler handler = prefab.GetComponent<CubeTapHandler>();
           
            if (handler != null)
            {
                if (!handler.isConnected )
                {
                    allConnected = false;
                    unconnectedCount++;
                    Debug.Log($"<color=red>Cube '{prefab.name}' tại {GetGridPosFromName(prefab.name)}: isConnected = FALSE</color>");
                }
                else
                {
                    Debug.Log($"<color=green>Cube '{prefab.name}': isConnected = TRUE</color>");
                }
            }
            else
            {
                Debug.LogWarning($"<color=orange>Cube '{prefab.name}' thiếu CubeTapHandler!</color>");
                allConnected = false;
            }
        }

        if (allConnected)
        {
            Debug.Log($"<color=lime>🎉 TẤT CẢ {allPrefabs.Count} CUBE ĐỀU CONNECTED! YOU WIN !!!</color>");
        }
        else
        {
            Debug.Log($"<color=red>Có {unconnectedCount}/{allPrefabs.Count} cube chưa connected.</color>");
            foreach (Transform prefab in specialPrefabs)
            {
                CubeTapHandler handler = prefab.GetComponent<CubeTapHandler>();
                if (!handler.isConnected)
                {
                    SpecicalConnected = false;
                }
                
            }
            if (SpecicalConnected)
            {
                Debug.Log("<color=red>YOU LOSEEE!</color>");
            }
           
        }

        return allConnected;
    }
    // Hàm hỗ trợ lấy gridPos từ tên (nếu chưa có thì thêm)
    private Vector3Int GetGridPosFromName(string name)
    {
        int startIndex = name.IndexOf("_at_(") + 5;
        int endIndex = name.LastIndexOf(")");
        if (startIndex > 4 && endIndex > startIndex)
        {
            string posStr = name.Substring(startIndex, endIndex - startIndex);
            string[] parts = posStr.Split(',');
            if (parts.Length == 3 && int.TryParse(parts[0].Trim(), out int x) &&
                int.TryParse(parts[1].Trim(), out int y) &&
                int.TryParse(parts[2].Trim(), out int z))
            {
                return new Vector3Int(x, y, z);
            }
        }
        return Vector3Int.zero;
    }

    // ← THÊM HÀM HỖ TRỢ KIỂM TRA MÀU GRAY (tolerance 0.01f)
    private bool IsColorGray(Color color)
    {
        return Mathf.Approximately(color.r, 0.5f) &&
               Mathf.Approximately(color.g, 0.5f) &&
               Mathf.Approximately(color.b, 0.5f) &&
               Mathf.Approximately(color.a, 1.0f);
    }
}