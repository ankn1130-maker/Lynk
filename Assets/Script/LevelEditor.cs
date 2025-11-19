using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public class LevelEditor : EditorWindow
{
    // === CẤU HÌNH BOARD ===
    private int sizeX = 2, sizeZ = 3, sizeY = 1;
    private int LineCount;
    private float cellSize = 1f;
    private Color redColor = Color.red;
    private Color blueColor = Color.blue;
    // === DỮ LIỆU ===
    private List<SpecialDot> specialDots = new List<SpecialDot>();
    private Vector2 scrollPos;
    private string levelName = "NewLevel";
    // === PICKING MODE ===
    private int pickMode = 0; // 0=Off, 1=Red, 2=Blue
    private bool isPicking = false;
    // 🆕 THAM CHIẾU BOARD ĐÃ TẠO
    private BoardGrid3D createdBoard;
    private Vector2 scrollPosition;
    [System.Serializable] public class DotData { public int x, y, z; public string prefabPath; public Color prefabColor; }  // 🆕 THÊM: prefabColor
    [System.Serializable] public class LevelData { public string levelName; public int sizeX, sizeZ; public float cellSize; public List<DotData> dots; public List<LineData> lines; };
    [System.Serializable] public class LineData { public string startKey; public string endKey; public Color lineColor; }

    [MenuItem("Tools/Dot Grid Editor")]
    public static void ShowWindow()
    {
        GetWindow<LevelEditor>("Dot Grid Editor");
    }
    [System.Serializable]
    public class SpecialDot
    {
        public Vector3Int gridPos;
        public string prefabPath;
        public Color prefabColor = Color.gray;  // 🆕 THÊM: Màu prefab
    }
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        LoadDefaultLevel();
        AutoGenerateLevelName();
    }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        // 🆕 XÓA BOARD KHI ĐÓNG WINDOW
        if (createdBoard != null)
        {
            DestroyImmediate(createdBoard.gameObject);
            createdBoard = null;
        }
    }
    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Label("Dot Grid Editor", EditorStyles.boldLabel);
        GUILayout.Space(10);
        // === CẤU HÌNH ===
        levelName = EditorGUILayout.TextField("Level Name", levelName);
        EditorGUILayout.BeginHorizontal();
        int newX = EditorGUILayout.IntField("Size X", sizeX, GUILayout.Width(240));
        int newY = EditorGUILayout.IntField("Size Y", sizeY, GUILayout.Width(240));  // 🆕 THÊM: IntField cho sizeY
        int newZ = EditorGUILayout.IntField("Size Z", sizeZ, GUILayout.Width(240));
        EditorGUILayout.EndHorizontal();
        if (newX != sizeX || newY != sizeY || newZ != sizeZ)
        {
            sizeX = Mathf.Max(1, newX);
            sizeY = Mathf.Max(1, newY);  // 🆕 SỬA: Update sizeY
            sizeZ = Mathf.Max(1, newZ);
            RegenerateAllSpecialDots(); // TỰ ĐỘNG TẠO LẠI DANH SÁCH
        }
        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);
        GUILayout.Space(10);
        // === TẤT CẢ VỊ TRÍ (TỰ ĐỘNG) ===
        EditorGUILayout.LabelField($"Tất cả vị trí: {sizeX * sizeY * sizeZ}", EditorStyles.miniBoldLabel);
        for (int x = 0; x < sizeX; x++)  // 🆕 SỬA: X outer (trước)
        {
            for (int y = 0; y < sizeY; y++)  // Y middle
            {
                for (int z = 0; z < sizeZ; z++)  // Z inner (sau)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);  // 🆕 SỬA: pos đầy đủ XYZ
                    int index = x * (sizeY * sizeZ) + y * sizeZ + z;  // 🆕 SỬA: Index 3D theo order X-Y-Z (x outer, y middle, z inner)
                    if (index >= specialDots.Count)
                        specialDots.Add(new SpecialDot { gridPos = pos });
                    var dot = specialDots[index];
                    EditorGUILayout.BeginHorizontal("box");
                    // Vị trí
                    EditorGUILayout.LabelField($"X {x} Y {y} Z {z}", GUILayout.Width(100));  // 🆕 SỬA: Label đầy đủ XYZ
                                                                                             // 🆕 SỬA: Bỏ Toggle Red (không special nữa)
                    GUILayout.Space(20); // Placeholder cho vị trí toggle cũ
                                         // === KÉO PREFAB - LIVE SPAWN NGAY ===
                    Rect dropRect = EditorGUILayout.GetControlRect(GUILayout.Width(120), GUILayout.Height(20));
                    GUI.Box(dropRect, GetPrefabName(dot.prefabPath), "HelpBox");
                    var evt = Event.current;
                    if (evt.type == EventType.DragUpdated && dropRect.Contains(evt.mousePosition))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    }
                    else if (evt.type == EventType.DragPerform && dropRect.Contains(evt.mousePosition))
                    {
                        DragAndDrop.AcceptDrag();
                        var go = DragAndDrop.objectReferences.OfType<GameObject>().FirstOrDefault();
                        if (go != null && AssetDatabase.Contains(go))
                        {
                            dot.prefabPath = AssetDatabase.GetAssetPath(go);
                            // TỰ ĐỘNG TẠO BOARD + SPAWN
                            AutoCreateBoardIfNeeded();
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dot.prefabPath);
                            if (prefab != null)
                            {
                                // XÓA PREFAB CŨ TẠI VỊ TRÍ NÀY (nếu có)
                                ClearPrefabAtPosition(dot.gridPos);
                                // SPAWN MỚI
                                createdBoard.SpawnPrefabAt(dot.gridPos, prefab);
                                // 🆕 THÊM: Log vị trí Point khi kéo/spawn
                                dot.gridPos = new Vector3Int(x, y, z);
                                LogPointPositionAtSpawn(dot.gridPos, go.name);
                                dot.prefabColor = Color.gray;  // Hoặc lấy từ prefab material
                                UpdatePrefabColor(dot.gridPos, dot.prefabColor);  // Update instance

                                Debug.Log($"specialDots.Count :{specialDots.Count}");
                                Debug.Log($"<color=magenta>INSTANT SPAWN: {go.name} tại {dot.gridPos}</color>");
                            }
                        }
                        evt.Use();
                    }
                    // 🆕 THÊM: ColorField cho đổi màu sau drag (nếu prefabPath not empty)
                    if (!string.IsNullOrEmpty(dot.prefabPath))
                    {
                        EditorGUI.BeginChangeCheck();
                        Color newColor = EditorGUILayout.ColorField("Prefab Color", dot.prefabColor, GUILayout.Width(220));
                        if (EditorGUI.EndChangeCheck())
                        {
                            dot.prefabColor = newColor;
                            UpdatePrefabColor(dot.gridPos, newColor);  // Update spawned instance
                            Debug.Log($"<color=yellow>Đổi màu {dot.prefabPath} tại {dot.gridPos} thành {newColor}</color>");
                        }
                    }
                    if (!string.IsNullOrEmpty(dot.prefabPath))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(80);  // Align với dropRect
                        if (GUILayout.Button("Delete Prefab", GUILayout.Width(100), GUILayout.Height(20)))
                        {
                            // Clear path và xóa instance/lines
                            dot.prefabPath = "";
                            dot.prefabColor = Color.white;
                            ClearPrefabAtPosition(dot.gridPos);
                            if (createdBoard != null)
                            {
                                createdBoard.RemovePrefabAndLines(dot.gridPos);  // Xóa lines liên quan (nếu hàm này có trong BoardGrid3D)
                            }
                            Debug.Log($"<color=orange>🗑️ Xóa prefab tại {dot.gridPos}</color>");
                            Repaint();  // Refresh UI ngay
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndHorizontal();


                }
            }
        }

        // 🆕 THÊM: Section hiển thị tất cả lines (scrollable list)
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Tất cả Lines", EditorStyles.boldLabel);
        if (createdBoard != null && createdBoard.lineRenderers.Count > 0)
        {
            // Scroll view cho list lines
            Vector2 linesScroll = EditorGUILayout.BeginScrollView(scrollPosition);  // Scroll height 150px
            foreach (var kvp in createdBoard.lineRenderers)
            {
                string key = kvp.Key;
                LineRenderer lr = kvp.Value;
                Vector3 start = lr.GetPosition(0);
                Vector3 end = lr.GetPosition(1);
                Color color = lr.material.color;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Key: {key}", GUILayout.Width(150));
                EditorGUILayout.LabelField($"Start: {start:F1} - End: {end:F1}", GUILayout.Width(280));
                EditorGUILayout.ColorField("", color, GUILayout.Width(60));  // Hiển thị màu
                if (GUILayout.Button("Xóa Line", GUILayout.Width(80)))
                {
                    if (createdBoard != null)
                    {
                        createdBoard.RemoveLineByKey(key);  // Xóa line cụ thể (thêm hàm này trong BoardGrid3D)
                        Debug.Log($"<color=orange>🗑️ Xóa line {key}</color>");
                    }
                    Repaint();  // Refresh UI
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label($"Tổng lines: {createdBoard.lineRenderers.Count}", EditorStyles.miniBoldLabel);
        }
        else
        {
            EditorGUILayout.HelpBox("No board or no lines yet. Create board and spawn prefabs to see lines.", MessageType.Info);
        }

        GUILayout.Space(10);
        // === NÚT TẠO BOARD ===
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("CREATE BOARD", GUILayout.Height(40)))
            CreateBoard();
        GUI.backgroundColor = createdBoard != null ? Color.red : Color.gray;
        if (GUILayout.Button("Delete Board", GUILayout.Height(40)))
            DeleteBoard();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        // === SAVE/LOAD ===
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Save & Load Level", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Level", GUILayout.Height(30), GUILayout.Width(120)))
        {
            SaveLevel();
        }
        if (GUILayout.Button("Load Level", GUILayout.Height(30), GUILayout.Width(120)))
        {
            LoadLevel();
        }
        EditorGUILayout.EndHorizontal();
        if (createdBoard != null)
            EditorGUILayout.HelpBox($"Board đã tạo! {specialDots.Count(p => !string.IsNullOrEmpty(p.prefabPath))} prefab được gán.", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    // 🆕 THÊM: Update màu spawned prefab instance
    //private void UpdatePrefabColor(Vector3Int gridPos, Color newColor)
    //{
    //    if (createdBoard == null) return;
    //    string targetName = $"_at_{gridPos}";
    //    foreach (Transform child in createdBoard.transform)
    //    {
    //        if (child.name.Contains(targetName))
    //        {
    //            Renderer rend = child.GetComponent<Renderer>();
    //            if (rend != null)
    //            {
    //                rend.material.color = newColor;  // Update material
    //                Debug.Log($"<color=green>Updated color cho {child.name} thành {newColor}</color>");
    //            }
    //            break;
    //        }
    //    }
    //}
    // 🆕 THÊM: Lấy info tất cả lines connected với pos (gọi từ BoardGrid3D)
    private string GetLinesInfo(Vector3Int pos)
    {
        if (createdBoard == null) return "No board";
        List<string> lineInfos = new List<string>();
        foreach (var kvp in createdBoard.lineRenderers)
        {
            Vector3 p0 = kvp.Value.GetPosition(0);
            Vector3 p1 = kvp.Value.GetPosition(1);
            Vector3Int p0Int = Vector3Int.FloorToInt(p0 / cellSize + new Vector3(sizeX / 2f, sizeY / 2f, sizeZ / 2f) - Vector3.one * 0.5f);  // Convert world to grid
            Vector3Int p1Int = Vector3Int.FloorToInt(p1 / cellSize + new Vector3(sizeX / 2f, sizeY / 2f, sizeZ / 2f) - Vector3.one * 0.5f);
            if (p0Int == pos || p1Int == pos)
            {
                lineInfos.Add($"({p0Int.x},{p0Int.y},{p0Int.z})-({p1Int.x},{p1Int.y},{p1Int.z})");
            }
        }
        return lineInfos.Count > 0 ? $"Lines: {string.Join(", ", lineInfos)}" : "No lines";
    }

    // XÓA PREFAB CŨ TẠI VỊ TRÍ TRƯỚC KHI SPAWN MỚI
    private void ClearPrefabAtPosition(Vector3Int gridPos)
    {
        if (createdBoard == null) return;
        string targetName = $"_at_{gridPos}";
        for (int i = createdBoard.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = createdBoard.transform.GetChild(i);
            if (child.name.Contains(targetName))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    // HÀM MỚI: TỰ ĐỘNG TẠO BOARD NẾU CHƯA CÓ
    private void AutoCreateBoardIfNeeded()
    {
        if (createdBoard != null) return;
        Debug.Log("<color=cyan>Auto creating board for live preview...</color>");
        GameObject boardObj = new GameObject("AutoBoard_Preview");
        createdBoard = boardObj.AddComponent<BoardGrid3D>();
        createdBoard.sizeX = sizeX;
        createdBoard.sizeY = 1;
        createdBoard.sizeZ = sizeZ;
        createdBoard.cellSize = cellSize;
        createdBoard.GenerateBoard(); // Tạo cube xám
    }
    // 🆕 HÀM CHÍNH: TẠO BOARDGRID3D
    // 🆕 ÁP DỤNG SPECIAL DOTS LÊN BOARD
    private void ApplySpecialDotsToBoard()
    {
        // XÓA TẤT CẢ COLORS TRƯỚC
        createdBoard.mainColors = new Color[] { redColor, blueColor };
        // TẠO DANH SÁCH INDEX CHO SPECIAL DOTS
        List<int> specialIndices = new List<int>();
        foreach (var dot in specialDots)
        {
            if (createdBoard.IsValidPosition(dot.gridPos))
            {
                int index = dot.gridPos.z * sizeX + dot.gridPos.x;
                specialIndices.Add(index);
            }
        }
        // GÁN MÀU CHO CÁC CUBE ĐẶC BIỆT (sau khi GenerateBoard)
        // LƯU Ý: Phải gọi sau GenerateBoard vì sphereList được tạo trong đó
    }
    private void CreateBoard()
    {
        DeleteBoard(); // Xóa board cũ nếu có
        GameObject boardObj = new GameObject("LevelBoard_" + levelName);
        createdBoard = boardObj.AddComponent<BoardGrid3D>();
        createdBoard.sizeX = sizeX;
        createdBoard.sizeY = 1;
        createdBoard.sizeZ = sizeZ;
        createdBoard.cellSize = cellSize;
        createdBoard.mainColors = new Color[] { redColor, blueColor };
        // TẠO BOARD NHƯNG KHÔNG VẼ LINE
        createdBoard.GenerateBoard();
        //createdBoard.CreateGridLines(); // Có thể comment nếu không muốn line
        // Spawn prefab + tô màu special dots
        foreach (var dot in specialDots)
        {
            if (!string.IsNullOrEmpty(dot.prefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dot.prefabPath);
                if (prefab != null)
                    createdBoard.SpawnPrefabAt(dot.gridPos, prefab);
            }
        }
        createdBoard.ApplySpecialDots(specialDots);
        UpdateAllPrefabColors();
    }
    private void UpdateAllPrefabColors()
    {
        if (createdBoard == null || specialDots == null) return;
        int count = 0;
        foreach (var dot in specialDots)
        {
            if (!string.IsNullOrEmpty(dot.prefabPath))
            {
                UpdatePrefabColor(dot.gridPos, dot.prefabColor);
                count++;
            }
        }
        Debug.Log($"<color=green>Updated colors for {count} prefabs</color>");
    }
    // 🆕 XÓA BOARD
    private void DeleteBoard()
    {
        if (createdBoard != null)
        {
            DestroyImmediate(createdBoard.gameObject);
            createdBoard = null;
            Debug.Log("<color=orange>🗑️ Đã xóa board</color>");
        }
    }
    // 🆕 GỌI LẠI KHI SIZE THAY ĐỔI
    private void OnValidate()
    {
        if (createdBoard != null)
        {
            createdBoard.sizeX = sizeX;
            createdBoard.sizeZ = sizeZ;
            createdBoard.cellSize = cellSize;
            createdBoard.GenerateBoard();
            ApplySpecialDotsToBoard();
        }
    }
    // === SCENE VIEW ===
    private void OnSceneGUI(SceneView sceneView)
    {
        DrawGridDots();
        //DrawSpecialDots();
        HandleMouseInput();
        sceneView.Repaint();
    }
    // Các hàm còn lại giữ nguyên...
    private string GetPrefabName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "No Prefab";
        string name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrEmpty(name) ? "Unknown" : name;
    }
    private void DrawGridDots()
    {
        // Vẽ điểm góc (corners) – giữ nguyên
        Handles.color = new Color(0.7f, 0.7f, 0.7f);  // Màu xám nhạt cho góc
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector3 worldPos = GridToWorld(new Vector3Int(x, 0, z));
                Handles.DrawSolidDisc(worldPos, Vector3.up, 0.08f);  // Điểm góc lớn hơn
            }
        }

        // 🆕 THÊM: Vẽ center points ở giữa mỗi ô (giữa 4 góc)
        Handles.color = Color.cyan;  // Màu xanh dương nổi bật để phân biệt center
        for (int x = 0; x < sizeX - 1; x++)  // -1 vì giữa ô (sizeX-1 ô)
        {
            for (int z = 0; z < sizeZ - 1; z++)  // -1 tương tự
            {
                // Tính grid trung tâm (float)
                Vector3 centerGrid = new Vector3(x + 0.5f, 0, z + 0.5f);
                Vector3 worldPos = GridToWorld(Vector3Int.FloorToInt(centerGrid));  // Chuyển sang world (dùng FloorToInt để khớp GridToWorld)
                                                                                    // Offset để chính giữa: + (cellSize * 0.5f) cho x/z
                worldPos += new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);

                Handles.DrawSolidDisc(worldPos, Vector3.up, 0.05f);  // Điểm center nhỏ hơn
                                                                     // Optional: Label tên để debug
                                                                     // Handles.Label(worldPos + Vector3.up * 0.1f, $"C{x}_{z}", EditorStyles.miniLabel);
            }
        }

        // ĐƯỜNG NỐI ngang/dọc – giữ nguyên
        Handles.color = new Color(0.3f, 0.3f, 0.3f);  // Màu xám đen
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector3 p = GridToWorld(new Vector3Int(x, 0, z));
                if (x < sizeX - 1) Handles.DrawLine(p, GridToWorld(new Vector3Int(x + 1, 0, z)));  // Ngang
                if (z < sizeZ - 1) Handles.DrawLine(p, GridToWorld(new Vector3Int(x, 0, z + 1)));  // Dọc
            }
        }

        // Optional: Thêm line chéo nếu muốn (hình X đầy đủ)
        // Handles.color = new Color(0.2f, 0.2f, 0.5f);  // Màu xanh đậm cho chéo
        // for (int x = 0; x < sizeX - 1; x++)
        // {
        //     for (int z = 0; z < sizeZ - 1; z++)
        //     {
        //         Vector3 p1 = GridToWorld(new Vector3Int(x, 0, z));
        //         Vector3 p2 = GridToWorld(new Vector3Int(x + 1, 0, z + 1));  // Chéo phải-xuống
        //         Handles.DrawLine(p1, p2);
        //         Vector3 p3 = GridToWorld(new Vector3Int(x + 1, 0, z));
        //         Vector3 p4 = GridToWorld(new Vector3Int(x, 0, z + 1));  // Chéo trái-xuống
        //         Handles.DrawLine(p3, p4);
        //     }
        // }
    }
    private void DrawSpecialDots()
    {
        foreach (var dot in specialDots)
        {
            if (!IsValidPos(dot.gridPos)) continue;
            Vector3 pos = GridToWorld(dot.gridPos);
            Handles.color = Color.white;  // 🆕 SỬA: Không dùng màu red/blue nữa
            Handles.DrawSolidDisc(pos, Vector3.up, 0.15f);
            if (!string.IsNullOrEmpty(dot.prefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dot.prefabPath);
                if (prefab != null)
                {
                    Handles.color = Color.cyan * 0.8f;
                    var bounds = GetPrefabBounds(prefab);
                    Handles.DrawWireCube(pos + bounds.center, bounds.size);
                }
            }
            Handles.color = Color.white;
            string label = "Prefab";  // 🆕 SỬA: Bỏ Red/Blue label
            if (!string.IsNullOrEmpty(dot.prefabPath)) label += " +Spawned";
            Handles.Label(pos + Vector3.up * 0.4f, label, EditorStyles.whiteMiniLabel);
        }
    }
    private Bounds GetPrefabBounds(GameObject prefab)
    {
        var renderer = prefab.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f);
    }
    private void HandleMouseInput()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && isPicking)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3Int gridPos = WorldToGrid(hit.point);
                if (IsValidPos(gridPos))
                {
                    ToggleSpecialDot(gridPos, false);  // 🆕 SỬA: isRed = false (không special)
                    e.Use();
                }
            }
        }
        else if (e.type == EventType.MouseDown && e.button == 1)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3Int gridPos = WorldToGrid(hit.point);
                RemoveSpecialDot(gridPos);
                e.Use();
            }
        }
    }
    // Các hàm hỗ trợ còn lại giữ nguyên...
    private Vector3 GridToWorld(Vector3Int gridPos) => new Vector3(
        (gridPos.x - sizeX * 0.5f + 0.5f) * cellSize, 0, (gridPos.z - sizeZ * 0.5f + 0.5f) * cellSize);
    private Vector3Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x / cellSize) + sizeX * 0.5f - 0.5f);
        int z = Mathf.RoundToInt((worldPos.z / cellSize) + sizeZ * 0.5f - 0.5f);
        return new Vector3Int(x, 0, z);
    }
    private bool IsValidPos(Vector3Int pos) => pos.x >= 0 && pos.x < sizeX && pos.z >= 0 && pos.z < sizeZ;
    private void ToggleSpecialDot(Vector3Int pos, bool isRed)
    {
        var existing = specialDots.Find(d => d.gridPos == pos);
        if (existing != null) specialDots.Remove(existing);
        else specialDots.Add(new SpecialDot { gridPos = pos });  // 🆕 SỬA: Không set isRed
    }
    private void RemoveSpecialDot(Vector3Int pos) => specialDots.RemoveAll(d => d.gridPos == pos);
    private void ValidateSpecialDots() => specialDots.RemoveAll(d => !IsValidPos(d.gridPos));

    private void SaveLevel()
    {
        // Validate trước khi save (xóa invalid dots)
        ValidateSpecialDots();
        var data = new LevelData
        {
            levelName = levelName,
            sizeX = sizeX,
            sizeZ = sizeZ,
            cellSize = cellSize,
            dots = specialDots.Where(d => !string.IsNullOrEmpty(d.prefabPath)).Select(d => new DotData { x = d.gridPos.x, z = d.gridPos.z, prefabPath = d.prefabPath, prefabColor = d.prefabColor })
            .ToList(), // ← FIX: Bỏ isRed
            lines = new List<LineData>()
        };
        if (createdBoard != null)
        {
            // Gọi hàm GetAllLines từ BoardGrid3D để lấy list lines (bạn cần thêm hàm này)
            data.lines = createdBoard.GetAllLines();  // Return List<LineData> từ lineRenderers
        }
        string json = JsonUtility.ToJson(data, true);
        string path = EditorUtility.SaveFilePanel("Save Level", "Assets/Levels", levelName + ".json", "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>Saved: {path} ({data.dots.Count} prefabs)</color>");
        }
    }
    private void LoadLevel()
    {
        string path = EditorUtility.OpenFilePanel("Load Level", "Assets/Levels", "json");
        if (string.IsNullOrEmpty(path)) return;
        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<LevelData>(json);
        if (data == null)
        {
            Debug.LogError("<color=red>JSON invalid!</color>");
            return;
        }
        levelName = data.levelName;
        sizeX = data.sizeX;
        sizeZ = data.sizeZ;
        cellSize = data.cellSize;
        LineCount = data.lines.Count;
        // Clear và regenerate để sync size
        specialDots.Clear();
        RegenerateAllSpecialDots(); // Tạo full list
                                    // Restore chỉ special dots
        foreach (var d in data.dots)
        {
            Vector3Int pos = new Vector3Int(d.x, d.y, d.z);  // 🆕 SỬA: Thêm y
            if (IsValidPos(pos))
            {
                int idx = d.x * (sizeY * sizeZ) + d.y * sizeZ + d.z;  // 🆕 SỬA: Index X-major đúng
                if (idx < specialDots.Count)
                {
                    specialDots[idx].prefabPath = d.prefabPath;
                    specialDots[idx].prefabColor = d.prefabColor;  // 🆕 THÊM: Load màu
                    Debug.Log($"Prefabcolor :  {d.prefabColor}");
                }
            }
        }
        // Auto recreate board preview nếu có
        if (createdBoard != null)
        {
            DeleteBoard();
            CreateBoard(); // Re-apply special dots và spawn prefab
        }

        Debug.Log($"<color=green>Loaded {data.dots.Count} prefabs from {path}</color>");
        Repaint(); // Refresh UI
    }

    private void RegenerateAllSpecialDots()
    {
        specialDots.Clear();
        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                {
                    specialDots.Add(new SpecialDot { gridPos = new Vector3Int(x, 0, z) });
                }
        Debug.Log($"<color=cyan>Regenerated theo Z outer X inner</color>");
    }

    // 🆕 THÊM: Update màu cho 1 prefab instance
    private void UpdatePrefabColor(Vector3Int gridPos, Color newColor)
    {
        if (createdBoard == null) return;
        string targetName = $"_at_{gridPos}";
        foreach (Transform child in createdBoard.transform)
        {
            if (child.name.Contains(targetName))
            {
                Renderer rend = child.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = newColor;
                    Debug.Log($"<color=green>Updated color cho {child.name} thành {newColor}</color>");
                }
                break;
            }
        }
    }

    private void LoadDefaultLevel()
    {
        sizeX = 2; sizeZ = 3;
        // 🆕 SỬA: Regenerate full list trước
        RegenerateAllSpecialDots();
    }
    private void AutoGenerateLevelName()
    {
        string levelsFolder = "Assets/Levels";
        if (!Directory.Exists(levelsFolder))
            Directory.CreateDirectory(levelsFolder);
        // Lấy danh sách tất cả file .json trong thư mục Levels
        var files = Directory.GetFiles(levelsFolder, "Level *.json");
        int maxIndex = 0;
        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            // Tìm pattern "Level X"
            if (fileName.StartsWith("Level "))
            {
                string numStr = fileName.Substring(6);
                if (int.TryParse(numStr, out int n))
                    maxIndex = Mathf.Max(maxIndex, n);
            }
        }
        // Tự động đặt tên kế tiếp
        levelName = $"Level {maxIndex + 1}";
    }
    // 🆕 HÀM MỚI: Log vị trí Point khi kéo/spawn prefab (tên + world pos)
    private void LogPointPositionAtSpawn(Vector3Int gridPos, string prefabName)
    {
        if (createdBoard == null)
        {
            Debug.LogError("<color=red>Board chưa tạo! Không thể log Point.</color>");
            return;
        }
        // Lấy point từ intersections của board
        if (createdBoard.intersections.TryGetValue(gridPos, out Transform pointTransform))
        {
            string pointName = pointTransform.name; // e.g., "Point_0_0_0"
            Vector3 worldPos = pointTransform.position; // World pos của point
            Debug.Log($"<color=green>🧊 Kéo prefab '{prefabName}' → Spawn tại POINT: {pointName} | Grid: {gridPos} | World Pos: {worldPos:F2}</color>");
        }
        else
        {
            Debug.LogError($"<color=red>Khi kéo '{prefabName}', KHÔNG tìm thấy Point tại {gridPos}! (Chạy GenerateBoard trước?)</color>");
        }
    }
}
