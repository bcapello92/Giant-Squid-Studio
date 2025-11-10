// Assets/Editor/AsciiRoomImporterWindow.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class AsciiRoomImporterWindow : EditorWindow
{
    // ---- Tiles & Prefabs ----
    [Header("Tiles & Prefabs")]
    public TileBase floorTile;
    public TileBase[] floorTiles;
    public int floorRandomSeed = 12345;

    public TileBase wallTile;         // middle/default
    public TileBase wallLeftTile;     // '['
    public TileBase wallRightTile;    // ']'
    public TileBase wallCapTile;      // top cap
    public TileBase cornerCapTile;    // '^' manual corner
    public TileBase cornerForegroundTile; // 'v'
    public TileBase foregroundWallTileLeft; // 'F'
    public TileBase foregroundWallTileRight;//'G'
    public TileBase lavaTile;           // 'L'

    public GameObject doorInPrefab;   // 'E'
    public GameObject doorOutPrefab;  // 'O'

    // ---- Options ----
    [Header("Room Options")]
    public string roomName = "Room_9x9";
    public bool zeroAtBottomLeft = true;
    public bool centerPivot = false;
    public bool makePrefab = false;
    public string prefabFolder = "Assets/Rooms";
    public bool placeFloorUnderLava = false;

    [Header("Grid")]
    public GridLayout.CellLayout cellLayout = GridLayout.CellLayout.Isometric;
    public Vector3 cellSize = new Vector3(4f, 2f, 1f);

    [Header("Sorting Layers (names)")]
    public string worldSortingLayer = "World"; // Floor/Walls/WallsFront/WallsTop
    public string lavaSortingLayer = "Lava";  // Lava

    [TextArea(5, 20)]
    public string asciiSource = SampleRooms.ArenaBasic;

    [MenuItem("Tools/ASCII/Tilemap Importer")]
    public static void Open() => GetWindow<AsciiRoomImporterWindow>("ASCII Importer");

    void OnGUI()
    {
        EditorGUILayout.LabelField("ASCII Source", EditorStyles.boldLabel);
        asciiSource = EditorGUILayout.TextArea(asciiSource, GUILayout.MinHeight(120));

        if (GUILayout.Button("Load Sample: Arena (9x9)")) asciiSource = SampleRooms.ArenaBasic;
        if (GUILayout.Button("Load Sample: Arena + Lava (9x9)")) asciiSource = SampleRooms.ArenaLava;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tiles & Prefabs", EditorStyles.boldLabel);

        floorTile = (TileBase)EditorGUILayout.ObjectField("Floor (.)", floorTile, typeof(TileBase), false);
        var so = new SerializedObject(this);
        var floorsProp = so.FindProperty("floorTiles");
        EditorGUILayout.PropertyField(floorsProp, new GUIContent("Floor Tiles (multiple)"), true);
        so.ApplyModifiedProperties();
        floorRandomSeed = EditorGUILayout.IntField("Floor Random Seed", floorRandomSeed);

        wallTile = (TileBase)EditorGUILayout.ObjectField("Wall (#) front", wallTile, typeof(TileBase), false);
        wallLeftTile = (TileBase)EditorGUILayout.ObjectField("Wall Left ([)", wallLeftTile, typeof(TileBase), false);
        wallRightTile = (TileBase)EditorGUILayout.ObjectField("Wall Right (])", wallRightTile, typeof(TileBase), false);
        wallCapTile = (TileBase)EditorGUILayout.ObjectField("Wall cap (top)", wallCapTile, typeof(TileBase), false);
        cornerCapTile = (TileBase)EditorGUILayout.ObjectField("Corner cap (^)", cornerCapTile, typeof(TileBase), false);
        cornerForegroundTile = (TileBase)EditorGUILayout.ObjectField("Corner foreground (v)", cornerForegroundTile, typeof(TileBase), false);
        foregroundWallTileLeft = (TileBase)EditorGUILayout.ObjectField("Foreground wall (F)", foregroundWallTileLeft, typeof(TileBase), false);
        foregroundWallTileRight = (TileBase)EditorGUILayout.ObjectField("Foreground wall (G)", foregroundWallTileRight, typeof(TileBase), false);
        lavaTile = (TileBase)EditorGUILayout.ObjectField("Lava (L)", lavaTile, typeof(TileBase), false);

        doorInPrefab = (GameObject)EditorGUILayout.ObjectField("Door In (E)", doorInPrefab, typeof(GameObject), false);
        doorOutPrefab = (GameObject)EditorGUILayout.ObjectField("Door Out (O)", doorOutPrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        zeroAtBottomLeft = EditorGUILayout.Toggle("ASCII origin at bottom-left", zeroAtBottomLeft);
        centerPivot = EditorGUILayout.Toggle("Center room at origin", centerPivot);
        makePrefab = EditorGUILayout.Toggle("Create Prefab", makePrefab);
        if (makePrefab) prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
        placeFloorUnderLava = EditorGUILayout.Toggle("Place floor under lava", placeFloorUnderLava);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
        cellLayout = (GridLayout.CellLayout)EditorGUILayout.EnumPopup("Cell Layout", cellLayout);
        cellSize = EditorGUILayout.Vector3Field("Cell Size (x,y,z)", cellSize);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sorting Layers", EditorStyles.boldLabel);
        worldSortingLayer = EditorGUILayout.TextField("World layer name", worldSortingLayer);
        lavaSortingLayer = EditorGUILayout.TextField("Lava layer name", lavaSortingLayer);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Room From ASCII", GUILayout.Height(34)))
        {
            BuildFromAscii();
        }
    }

    // ---------------- Build ----------------
    void BuildFromAscii()
    {
        if (string.IsNullOrWhiteSpace(asciiSource))
        {
            Debug.LogWarning("[ASCII] No source text.");
            return;
        }

        // Root & Grid
        GameObject root = new GameObject(roomName);
        var grid = root.AddComponent<Grid>();
        grid.cellLayout = cellLayout;
        grid.cellSize = cellSize;

        // Tilemaps
        var floorTM = CreateTilemap(root.transform, "Floor", collider: false, sortingLayer: worldSortingLayer, orderInLayer: 1);
        var wallsFrontTM = CreateTilemap(root.transform, "WallsFront", collider: true, sortingLayer: worldSortingLayer, orderInLayer: 3);
        var wallsTM = CreateTilemap(root.transform, "Walls", collider: true, sortingLayer: worldSortingLayer, orderInLayer: 3);
        var wallsTopTM = CreateTilemap(root.transform, "WallsTop", collider: true, sortingLayer: worldSortingLayer, orderInLayer: 3);
        var lavaTM = CreateTilemap(root.transform, "Lava", collider: true, sortingLayer: lavaSortingLayer, orderInLayer: 0);

        SetupComposite(wallsFrontTM.gameObject);
        SetupComposite(wallsTM.gameObject);
        SetupComposite(lavaTM.gameObject, isTrigger: true);

        var doors = new GameObject("Doors");
        doors.transform.SetParent(root.transform, false);

        // Parse ASCII
        var lines = ReadLines(asciiSource);
        int h = lines.Count;
        for (int r = 0; r < h; r++)
        {
            string ln = lines[r];
            for (int c = 0; c < ln.Length; c++)
            {
                char ch = ln[c];
                int y = zeroAtBottomLeft ? (h - 1 - r) : r;
                var cell = new Vector3Int(c, y, 0);

                switch (ch)
                {
                    case '^': // manual corner cap
                        if (cornerCapTile) wallsTopTM.SetTile(cell, cornerCapTile);
                        break;

                    case 'v': // manual corner cap for foreground/front walls
                        if (cornerForegroundTile) wallsFrontTM.SetTile(cell, cornerForegroundTile);
                        break;

                    case '[': // force left wall
                        if (wallLeftTile) wallsTM.SetTile(cell, wallLeftTile);
                        else if (wallTile) wallsTM.SetTile(cell, wallTile);
                        break;

                    case ']': // force right wall
                        if (wallRightTile) wallsTM.SetTile(cell, wallRightTile);
                        else if (wallTile) wallsTM.SetTile(cell, wallTile);
                        break;

                    case '#': // auto run: edges + middle, plus top cap auto
                        {
                            bool hasLeft = (c > 0 && lines[r][c - 1] == '#');
                            bool hasRight = (c + 1 < ln.Length && lines[r][c + 1] == '#');
                            TileBase use = wallTile;
                            if (!hasLeft && hasRight && wallLeftTile) use = wallLeftTile;
                            else if (hasLeft && !hasRight && wallRightTile) use = wallRightTile;
                            if (use) wallsTM.SetTile(cell, use);

                            bool hasAbove = (r > 0 && c < lines[r - 1].Length && lines[r - 1][c] == '#');
                            if (!hasAbove && wallCapTile) wallsTopTM.SetTile(cell, wallCapTile);
                        }
                        break;

                    case 'F': // foreground rim (blocking)
                        if (foregroundWallTileLeft) wallsFrontTM.SetTile(cell, foregroundWallTileLeft);
                        else if (wallTile) wallsFrontTM.SetTile(cell, wallTile);
                        break;

                    case 'G'://forgeround front wall right
                        if (foregroundWallTileRight) wallsFrontTM.SetTile(cell, foregroundWallTileRight);
                        break;

                    case '.': // floor
                        {
                            var t = PickFloor(c, y);
                            if (t) floorTM.SetTile(cell, t);
                        }
                        break;

                    case 'L': // lava
                        {
                            if (placeFloorUnderLava)
                            {
                                var t = PickFloor(c, y);
                                if (t) floorTM.SetTile(cell, t);
                            }
                            else floorTM.SetTile(cell, null);

                            if (lavaTile) lavaTM.SetTile(cell, lavaTile);
                        }
                        break;

                    case 'E': // door in
                        {
                            var t = PickFloor(c, y); if (t) floorTM.SetTile(cell, t);
                            PlaceDoor(doorInPrefab, doors.transform, cell, grid);
                        }
                        break;

                    case 'O': // door out
                        {
                            var t = PickFloor(c, y); if (t) floorTM.SetTile(cell, t);
                            PlaceDoor(doorOutPrefab, doors.transform, cell, grid);
                        }
                        break;
                }
            }
        }

        if (centerPivot)
        {
            var b = ComputeChildrenBounds(root);
            root.transform.position = -b.center;
        }

        if (makePrefab)
        {
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                AssetDatabase.CreateFolder("Assets", "Rooms");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{prefabFolder}/{roomName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        else
        {
            Selection.activeObject = root;
        }
    }

    // ---------------- Helpers (all inside class) ----------------

    static List<string> ReadLines(string src)
    {
        var list = new List<string>();
        using (var r = new System.IO.StringReader(src))
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                list.Add(line);
            }
        }
        return list;
    }

    TileBase PickFloor(int x, int y)
    {
        if (floorTiles != null && floorTiles.Length > 0)
        {
            int idx = Mathf.Abs(Hash2D(x, y, floorRandomSeed)) % floorTiles.Length;
            var tb = floorTiles[idx];
            return tb ? tb : floorTile;
        }
        return floorTile;
    }

    static int Hash2D(int x, int y, int seed)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + x;
            h = h * 31 + y;
            h = h * 31 + seed;
            h ^= (h << 13); h ^= (h >> 17); h ^= (h << 5);
            return h;
        }
    }

    static Tilemap CreateTilemap(Transform parent, string name, bool collider, string sortingLayer, int orderInLayer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.mode = TilemapRenderer.Mode.Individual;
        r.sortOrder = TilemapRenderer.SortOrder.TopRight;
        r.sortingLayerName = sortingLayer;
        r.sortingOrder = orderInLayer;

        if (collider)
            go.AddComponent<TilemapCollider2D>(); // composite set in SetupComposite

        return tm;
    }

    static void SetupComposite(GameObject go, bool isTrigger = false)
    {
        var tmCol = go.GetComponent<TilemapCollider2D>();
        if (!tmCol) return;


        tmCol.isTrigger = isTrigger;

        var rb = go.GetComponent<Rigidbody2D>();
        if (!rb) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        var comp = go.GetComponent<CompositeCollider2D>();
        if (!comp) comp = go.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        comp.generationType = CompositeCollider2D.GenerationType.Synchronous;
    }

    static void PlaceDoor(GameObject prefab, Transform parent, Vector3Int cell, Grid grid)
    {
        if (!prefab) return;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (!inst) inst = GameObject.Instantiate(prefab);
        inst.name = prefab.name;
        inst.transform.SetParent(parent, false);
        inst.transform.position = grid.CellToWorld(cell);
    }

    static Bounds ComputeChildrenBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // ---- Sample ASCII (nested class) ----
    static class SampleRooms
    {
        public static string ArenaBasic =>
@"[[[[[[O]
F.......]
F.......]
F.......]
F.......]
F.......]
F.......]
F.......]
FFFFEFFF";

        public static string ArenaLava =>
@"[[[[[[O]
F.......]
F..LL...]
F...L...]
F..LL...]
F.......]
F.......]
F.......]
FFFFEFFF";
    }
}
