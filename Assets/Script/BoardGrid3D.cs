using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
    int index = 0;
    // Biến duy nhất lưu màu
    public Color[] mainColors = { Color.red, Color.blue };

    void Start()
    {
        occupied = new GameObject[sizeX, sizeY, sizeZ];
        GenerateBoard();
        CreateHighlight();
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

    public void GenerateBoard()
    {
        // === TẠO PLANE TRẮNG ===
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Plane);
        board.transform.parent = transform;
        board.transform.localScale = new Vector3(sizeX * 0.1f, 1, sizeZ * 0.1f);
        Material whiteMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        whiteMat.color = Color.white;
        board.GetComponent<Renderer>().material = whiteMat;

        // XÓA DỮ LIỆU CŨ
        sphereList.Clear();
        intersections.Clear();
        index = 0;
        occupied = new GameObject[sizeX, sizeY, sizeZ];

        Debug.Log("<color=blue>=== BẮT ĐẦU TẠO POINTS (Thứ tự: X → Y → Z) ===</color>");

        // === TẠO POINT (CHỈ ĐỂ LƯU TỌA ĐỘ) ===
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

                    // TẠO CUBE NHỎ NHƯNG ẨN ĐI
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(point.transform);
                    cube.transform.localPosition = Vector3.zero;
                    cube.transform.localScale = Vector3.one * 0.3f;
                    cube.GetComponent<Renderer>().enabled = false; // ẨN CUBE
                    cube.GetComponent<BoxCollider>().enabled = false; // TẮT COLLIDER
                    sphereList.Add(cube.transform);

                    // 🆕 THÊM: Log thứ tự tạo point
                    index++;
                    Debug.Log($"<color=cyan>[{index:00}] TẠO POINT: {point.name} | Grid: {gridPos} | World Pos: {worldPos:F2} (X={x}, Y={y}, Z={z})</color>");
                }


        Debug.Log($"<color=blue>=== HOÀN THÀNH TẠO {index} POINTS ===</color>");

        // KHÔNG GỌI CreateGridLines() → KHÔNG CÓ LINE
        // CreateGridLines(); // COMMENT DÒNG NÀY
        ClearAllLines();
        Debug.Log("<color=cyan>Board generated - NO GRID LINES, only dynamic adjacent lines</color>");
    }

    // 🆕 HÀM MỚI: Clear TẤT CẢ lines (gọi khi regenerate để hủy gap lines)
    public void ClearAllLines()
    {
        foreach (var kvp in lineRenderers)
        {
            if (Application.isPlaying)
                Destroy(kvp.Value.gameObject);
            else
                DestroyImmediate(kvp.Value.gameObject);
        }
        lineRenderers.Clear();
        Debug.Log("<color=orange>🗑️ Cleared ALL lines (no gap lines left)</color>");
    }

    // 🆕 HÀM MỚI: ÁP DỤNG SPECIAL DOTS
    public void ApplySpecialDots(List<LevelEditor.SpecialDot> specialDots = null)
    {
        // NẾU KHÔNG CÓ DỮ LIỆU → KHÔNG LÀM GÌ CẢ
        if (specialDots == null || specialDots.Count == 0)
            return;

        // CHỈ TÔ MÀU + TAG CHO CÁC CUBE TRONG specialDots
        foreach (var dot in specialDots)
        {
            if (IsValidPosition(dot.gridPos))
            {
                int idx = dot.gridPos.z * sizeX + dot.gridPos.x;
                if (idx < sphereList.Count)
                {
                    Renderer rend = sphereList[idx].GetComponent<Renderer>();
                   
                    sphereList[idx].gameObject.tag = "Specical";
                }
            }
        }
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

    private void DrawLinesFromCube(Vector3Int center)
    {
        // LẤY PIECE TẠI CENTER
        GameObject pieceA = GetPieceAt(center);
        if (pieceA == null) return;
        Transform a = pieceA.transform; // ← DÙNG PIECE, KHÔNG DÙNG POINT!
        Vector3Int[] directions = {
        new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
        new Vector3Int(0,0,1), new Vector3Int(0,0,-1),
        new Vector3Int(1,0,1), new Vector3Int(-1,0,1),
        new Vector3Int(1,0,-1), new Vector3Int(-1,0,-1)
    };
        string[] names = { "PHẢI", "TRÁI", "XUỐNG", "LÊN", "CHÉO↘", "CHÉO↙", "CHÉO↗", "CHÉO↖" };
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3Int neighbor = center + directions[i];
            if (!IsValidPosition(neighbor))
            {
                Debug.Log($"<color=gray>Skip {names[i]}: Invalid pos {neighbor}</color>");
                continue;
            }

            // Nếu point OK, mới check pieceB
            GameObject pieceB = GetPieceAt(neighbor);
            if (pieceB == null)
            {
                Debug.Log($"<color=gray>Skip {names[i]}: No pieceB at {neighbor}</color>");
                continue;
            }
            string key = GetLineKey(center, neighbor);
            string key1 = GetLineKey(neighbor,center);
            if (lineRenderers.ContainsKey(key) || lineRenderers.ContainsKey(key1))
            {
                Debug.Log($"<color=yellow>Skip {names[i]}: Line {key} already exists (không tạo lại)</color>");
                continue;
            }
            // 🆕 SỬA: Kiểm tra điểm point của center và neighbor có ở cạnh nhau không
            if (!ArePointsAdjacent(center, neighbor))  // 🆕 SỬA: Inverted condition - skip nếu KHÔNG adjacent (gap!)
            {
                Debug.Log($"<color=red>Skip {names[i]}: Point {center} và {neighbor} KHÔNG ở cạnh nhau (gap!)</color>");
                continue;  // 🆕 SỬA: Continue nếu không adjacent
            }
            // Nếu adjacent và chưa tồn tại key, tạo line mới
            CreateLineBetween(pieceA.transform, pieceB.transform, key); // ← DÙNG PIECE
            Debug.Log($"<color=lime>LINE MỚI: {key} | {center} → {neighbor} | {names[i]} (adjacent OK)</color>");
        }
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

    private GameObject GetPieceAt(Vector3Int pos)
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

        // Duyệt tất cả các ô (x, z)
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector3Int current = new Vector3Int(x, 0, z);
                Transform a = intersections[current];

                // === 1. NỐI PHẢI (ngang) ===
                if (x < sizeX - 1)
                {
                    Vector3Int right = new Vector3Int(x + 1, 0, z);
                    Transform b = intersections[right];
                    string key = GetLineKey(current, right);
                    CreateLineBetween(a, b, key);
                    Debug.Log("ngang");
                }

                // === 2. NỐI XUỐNG (dọc) ===
                if (z < sizeZ - 1)
                {
                    Vector3Int down = new Vector3Int(x, 0, z + 1);
                    Transform b = intersections[down];
                    string key = GetLineKey(current, down);
                    CreateLineBetween(a, b, key);
                    Debug.Log("doc");
                }

                // === 3. NỐI CHÉO XUỐNG-PHẢI ( \ ) ===
                if (x < sizeX - 1 && z < sizeZ - 1)
                {
                    Vector3Int diagDownRight = new Vector3Int(x + 1, 0, z + 1);
                    Transform b = intersections[diagDownRight];
                    string key = GetLineKey(current, diagDownRight);
                    CreateLineBetween(a, b, key);
                    Debug.Log("xuong phai");
                }

                // === 4. NỐI CHÉO XUỐNG-TRÁI ( / ) ===
                if (x > 0 && z < sizeZ - 1)
                {
                    Vector3Int diagDownLeft = new Vector3Int(x - 1, 0, z + 1);
                    Transform b = intersections[diagDownLeft];
                    string key = GetLineKey(current, diagDownLeft);
                    CreateLineBetween(a, b, key);
                    Debug.Log("xuong trai");
                }
            }
        }

        Debug.Log($"<color=magenta>ĐÃ TẠO {lineRenderers.Count} ĐƯỜNG LINE (NGANG, DỌC, CHÉO)!</color>");
    }
    private string GetLineKey(Vector3Int a, Vector3Int b)
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
        GameObject lineObj = new GameObject($"Line_{key}");
        lineObj.transform.parent = transform;
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = lineMat ?? new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color =Color.black;
        lr.startWidth = lr.endWidth = 0.02f;
        lr.useWorldSpace = true;

        // DÙNG VỊ TRÍ CỦA PIECE
        Vector3 posA = a.position;
        Vector3 posB = b.position;

        lr.SetPosition(0, posA);
        lr.SetPosition(1, posB);

        lineRenderers[key] = lr;
    }

   

    public CubeTapHandler GetCubeAt(Vector3Int pos)
    {
        // giả sử bạn lưu cube trong dictionary hoặc mảng
        if (intersections.ContainsKey(pos))
        {
            Transform point = intersections[pos];
            if (point.childCount > 0)
            {
                GameObject cube = point.GetChild(0).gameObject;
                CubeTapHandler handler = cube.GetComponent<CubeTapHandler>();
                return handler;
            }
        }
        return null;
    }

    // SỬA: HasDirectLineConnection - Chỉ true nếu adjacent (bao gồm chéo), không gap
    public bool HasDirectLineConnection(Vector3Int a, Vector3Int b)
    {
        // 🆕 THAY ĐỔI: Check strict adjacent trước, rồi mới xem line có tồn tại
        if (!IsAdjacent(a, b)) return false;

        string key = GetLineKey(a, b);
        return lineRenderers.ContainsKey(key);
    }

    // SỬA: HightLightLineBetween - Thêm check adjacent (bao gồm chéo), không gap
    public void HightLightLineBetween(Vector3Int a, Vector3Int b, Color color)
    {
        Debug.Log($"HightLightLineBetween: {a} , {b}");

        // 🆕 THÊM: Chỉ nếu adjacent (ngang/dọc/chéo), không gap >1
        if (!IsAdjacent(a, b))
        {
            Debug.Log($"<color=red>Không adjacent (gap >1 hoặc xa): {a} ↔ {b}</color>");
            return;
        }

        string key = GetLineKey(a, b);
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.z - b.z);
        // Strict: Chỉ dx<=1 dz<=1 và (ngang/dọc/chéo)
        if (!(dx + dz == 1 || (dx == 1 && dz == 1)))
        {
            Debug.Log($"<color=red>Không liền kề adjacent: {a} ↔ {b}</color>");
            return;
        }

        Debug.Log($"<color=yellow>Tìm line adjacent/chéo: {a} ↔ {b} → <b>{key}</b></color>");
        if (lineRenderers.TryGetValue(key, out LineRenderer lr))
        {
            lr.material.color = color;
            Debug.Log($"<color=green>ĐÃ TÔ MÀU: <b>Line_{key}</b> (adjacent + chéo)</color>");
        }
        else
        {
            Debug.Log($"<color=red>KHÔNG TÌM THẤY line adjacent: <b>Line_{key}</b></color>");
        }
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
            instance.name = $"{prefab.name}_at_{gridPos}";
          
            CubeTapHandler handler = instance.GetComponent<CubeTapHandler>();
            if (handler == null)
            {
                handler = instance.AddComponent<CubeTapHandler>();  // Add nếu chưa có trên prefab
            }
            handler.Init(this, gridPos);  // Gọi Init để set board + gridPos (bỏ qua Start())
            handler.enabled = true;  // Enable nếu disabled

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

    private bool HasPrefabAt(Vector3Int pos)
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

    public List<LevelEditor.LineData> GetAllLines()
    {
        List<LevelEditor.LineData> lines = new List<LevelEditor.LineData>();
        foreach (var kvp in lineRenderers)
        {
            Vector3 start = kvp.Value.GetPosition(0);
            Vector3 end = kvp.Value.GetPosition(1);
            lines.Add(new LevelEditor.LineData { startKey = start.ToString("F1"), endKey = end.ToString("F1"), lineColor = kvp.Value.material.color });
        }
        return lines;
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
}