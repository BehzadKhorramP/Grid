using MadApperEditor.Common;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

using Object = UnityEngine.Object;
using Tile = UnityEngine.Tilemaps.Tile;

namespace MadApper.GridSystem.Editor
{
    public abstract class GridEditor : OdinEditorWindow
    {
        #region Editor Assets

        [SerializeField] protected GridLookupTable gridLookup;

        [NonSerialized] EditorCommonAssets _commonAssets;
        protected EditorCommonAssets commonAssets => _commonAssets ??= EditorCommonAssets.Get();
        #endregion

        [BoxGroup("Provider", order: 0, centerLabel: true)]
        [SerializeField]
        [OnValueChanged(nameof(OnValueProviderTypeChanged))][ValueDropdown(nameof(GetAvailableProviderTypes))] Type valueProviderType;
        [BoxGroup("Provider")]
        [HideReferenceObjectPicker][ShowInInspector][InlineProperty, HideLabel] GridValueProvider valueProvider;


        [BoxGroup("Dimensions", order: 1, centerLabel: true)][OnValueChanged(nameof(OnGridDimensionsChanged))][PropertyRange(1, 100)] public int Width = 5;

        [BoxGroup("Dimensions")][OnValueChanged(nameof(OnGridDimensionsChanged))][PropertyRange(1, 100)] public int Height = 10;


        #region References

        [NonSerialized] Tilemap _tileMap;
        Tilemap tileMap => _tileMap ??= FindObjectOfType<Tilemap>();

        [NonSerialized] GridBrush _gridBrush;
        GridBrush gridBrush => _gridBrush ??= GetActiveGridBrush();

        #endregion

        #region NodeValues

        const string k_ValuesParentId = "Editor - Values Parent";

        [HideInInspector][SerializeField] List<BoardEntity> nodeValueInstancesCache = new();

        [HideInInspector][SerializeField] GameObject _valuesParent;
        GameObject valuesParent
        {
            get
            {
                if (_valuesParent == null)
                {
                    _valuesParent = GameObject.Find(k_ValuesParentId);
                    if (_valuesParent == null) _valuesParent = new GameObject(k_ValuesParentId);
                }
                return _valuesParent;
            }
        }

        [HideInInspector][NonSerialized] BoardEntity selectedValueInstance;

        bool modificationMode;
        bool isEarasing;

        #endregion


        [HideInInspector][SerializeField] GridData gridData;

        Action<GridData> onSaveCallback;

        protected override void OnEnable()
        {
            base.OnEnable();

            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            SceneView.duringSceneGui += OnSceneGUI;

            WatchingBrush.s_OnPainted += OnPaintedByBrush;
            WatchingBrush.s_OnErased += OnErasedByBrush;
            WatchingBrush.s_OnSelected += OnSelectedByBrush;
            WatchingBrush.s_OnBrushFinished += OnBrushFinished;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            SceneView.duringSceneGui -= OnSceneGUI;

            WatchingBrush.s_OnPainted -= OnPaintedByBrush;
            WatchingBrush.s_OnErased -= OnErasedByBrush;
            WatchingBrush.s_OnSelected -= OnSelectedByBrush;
            WatchingBrush.s_OnBrushFinished -= OnBrushFinished;
        }


        public static TWindow OpenEditor<TWindow>(GridLookupTable gridLookup, GridData gridData, Action<GridData> onSaveCallback) where TWindow : GridEditor
        {
            var window = GetWindow<TWindow>();

            window.titleContent = new GUIContent("Grid Editor");
            window.InitializeEditor(gridLookup);
            window.Show();
            window.SetOnSaveCallback(onSaveCallback);
            window.InitializeGrid(gridData);

            return window;
        }


        public void CleanupEditor()
        {
            gridData = null;
            onSaveCallback = null;
            isEarasing = false;

            tileMap.ClearAllTiles();

            _valuesParent = GameObject.Find(k_ValuesParentId);
            if (_valuesParent != null) DestroyImmediate(_valuesParent);

            modificationMode = false;
            selectedValueInstance = null;
            valueProvider = null;

            Repaint();
        }


        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                CleanupEditor();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {

            }
        }

        private void OnSceneGUI(SceneView scene)
        {
            HandMouseEvents(scene);
            HandleModification(scene);
        }



        #region Initialization

        void InitializeEditor(GridLookupTable gridLookup)
        {
            this.gridLookup = gridLookup;

            CleanupEditor();
            RefreshTilePalette();
            OpenTilePalette();
            this.TrySetDirty();
        }
        public void SetOnSaveCallback(Action<GridData> onSaveCallback) => this.onSaveCallback = onSaveCallback;

        #endregion

        #region Palette
        public void RefreshTilePalette()
        {
            if (gridLookup == null) return;
            if (gridLookup.Palette == null)
            {
                EditorUtility.DisplayDialog("Error", "Palette is null in GridEditorAssets", "OK");
                return;
            }

            // 2. Instantiate it temporarily to modify
            GameObject instance = PrefabUtility.InstantiatePrefab(gridLookup.Palette) as GameObject;
            Grid grid = instance.GetComponent<Grid>();
            Tilemap tilemap = instance.GetComponentInChildren<Tilemap>();

            if (tilemap == null)
            {
                Debug.LogError("No Tilemap found in palette prefab.");
                Object.DestroyImmediate(instance);
                return;
            }

            tilemap.ClearAllTiles();

            var allSOs = gridLookup.AllSOs;
            int columns = 4;

            for (int i = 0; i < allSOs.Count; i++)
            {
                var tile = allSOs[i].GetTile();
                if (tile == null) continue;

                int x = (i / columns);   // Column
                int y = -(i % columns);    // Row (negative to go downward in Unity's Y axis)

                Vector3Int cellPos = new Vector3Int(x, y, 0);
                tilemap.SetTile(cellPos, tile);
            }

            var path = AssetDatabase.GetAssetPath(gridLookup.Palette);

            // 4. Apply changes back to the prefab
            PrefabUtility.SaveAsPrefabAsset(instance, path);

            // 5. Cleanup
            Object.DestroyImmediate(instance);
        }
        public static GridBrush GetActiveGridBrush()
        {
            // Get the type of the internal Tile Palette window
            var tilePaletteWindowType = GetPaintPaletteWindowType();
            var window = EditorWindow.GetWindow(tilePaletteWindowType);
            if (window == null) return null;

            // Get the internal brush field (non-public)
            var brushField = tilePaletteWindowType.GetField("m_GridBrush", BindingFlags.NonPublic | BindingFlags.Instance);
            if (brushField == null) return null;

            var brush = brushField.GetValue(window) as GridBrush;
            return brush;
        }
        //   public GridPaintPaletteWindow
        public static void OpenTilePalette()
        {
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
        }
        public static Type GetPaintPaletteWindowType()
        {
            return GetTypeFromAllAssemblies("UnityEditor.Tilemaps.GridPaintPaletteWindow");

            Type GetTypeFromAllAssemblies(string typeName)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip dynamic assemblies or assemblies that we don't want to search
                    if (assembly.IsDynamic || assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("Microsoft"))
                        continue;

                    try
                    {
                        // Search for the type in the current assembly
                        var type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            return type;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle any errors that occur while inspecting the assembly
                        Console.WriteLine($"Error inspecting assembly {assembly.FullName}: {ex.Message}");
                    }
                }

                return null; // Return null if the type is not found
            }
        }

        public static void CloseTilePalette()
        {
            var type = GetPaintPaletteWindowType();

            if (type != null)
            {
                // Get the non-public instance method "Close"
                var closeMethod = type.GetMethod("Close", BindingFlags.Instance | BindingFlags.Public);

                if (closeMethod != null)
                {
                    // Get the window instance
                    var window = EditorWindow.GetWindow(type);
                    if (window != null)
                    {
                        closeMethod.Invoke(window, null);
                    }
                }
            }

        }

        #endregion


        #region Creation

        [PropertyOrder(-10)]
        [HorizontalGroup]
        [Button(ButtonSizes.Large), GUIColor(EditorCommonAssets.k_YellowColor)]
        public void CreateNew()
        {
            InitializeGrid(null);
        }

        NodeData AddRandomAllowedNodeData(GridData data, Vector2Int key)
        {
            if (valueProvider == null) return null;
            var provided = valueProvider.Provide();
            if (provided == null) return null;

            var values = new List<ValueData>() { provided };
            var nodeData = new NodeData(key) { Values = values };
            data.AddNodeData(nodeData);
            return nodeData;
        }
        public void InitializeGrid(GridData gridData)
        {
            EnsureValueProviderIsValid();

            if (gridData == null)
            {
                gridData = new GridData();
                gridData.Width = Width;
                gridData.Height = Height;

                for (int height = 0; height < Height; height++)
                {
                    for (int width = 0; width < Width; width++)
                    {
                        var key = new Vector2Int(width, height);
                        AddRandomAllowedNodeData(gridData, key);
                    }
                }
            }
            else
            {
                Width = gridData.Width;
                Height = gridData.Height;

                if (gridData.ValueProvider != null)
                {
                    valueProviderType = gridData.ValueProvider.GetType();
                    var valueProvider = gridData.ValueProvider.DeepCopy();

                    SetValueProvider(valueProvider);

                    EnsureValueProviderIsValid();
                }
            }


            this.gridData = new GridData();
            this.gridData = gridData.DeepCopy();

            tileMap.ClearAllTiles();

            foreach (var item in gridData.NodesData)
            {
                if (item.Values == null || !item.Values.Any()) continue;

                var tile = item.Values[0].SO.GetTile();
                if (tile == null) continue;
                var cellPos = item.Key.GetCellPosByKey(gridData.Height);

                tileMap.SetTile(cellPos, tile);
            }

            RecenterGridItself();

            RefreshNodeValues();

            OnModificationModeOff();
        }

        void EnsureValueProviderIsValid()
        {
            if (valueProviderType == null)
            {
                valueProviderType = gridLookup.ValueProviderType;

                if (valueProviderType == null)
                {
                    $"no GridValueProvider type found in lookup!".LogWarning();
                    return;
                }
            }

            if (valueProvider == null)
            {
                OnValueProviderTypeChanged();
            }

            valueProvider.EnsureValid();
        }

        void OnValueProviderTypeChanged()
        {
            SetValueProvider((GridValueProvider)Activator.CreateInstance(valueProviderType));
        }

        void SetValueProvider(GridValueProvider valueProvider)
        {
            this.valueProvider = valueProvider;
            this.valueProvider.InitializeEditor();
        }

        void OnGridDimensionsChanged()
        {
            UpdateGridData();
        }

        void UpdateGridData()
        {
            UpdataGridDimensions();
            UpdateGridDimensionsParameters();
            InitializeGrid(gridData);
        }

        void UpdataGridDimensions()
        {
            if (gridData == null) return;
            if (tileMap == null) return;

            HashSet<Vector2Int> requiredCellPoses = new();
            HashSet<Vector2Int> excessCellPoses = new();

            int previousWidth = gridData.Width;
            int previousHeight = gridData.Height;
            int newWidth = Width;
            int newHeight = Height;

            if (newWidth > previousWidth)
                for (int y = 0; y < Math.Min(previousHeight, newHeight); y++)
                    for (int x = previousWidth; x < newWidth; x++)
                        requiredCellPoses.Add(new Vector2Int(x, y));

            if (newHeight > previousHeight)
                for (int y = previousHeight; y < newHeight; y++)
                    for (int x = 0; x < Math.Min(previousWidth, newWidth); x++)
                        requiredCellPoses.Add(new Vector2Int(x, y));

            if (newWidth < previousWidth)
                for (int y = 0; y < previousHeight; y++)
                    for (int x = newWidth; x < previousWidth; x++)
                        excessCellPoses.Add(new Vector2Int(x, y));

            if (newHeight < previousHeight)
                for (int y = newHeight; y < previousHeight; y++)
                    for (int x = 0; x < previousWidth; x++)
                        excessCellPoses.Add(new Vector2Int(x, y));


            foreach (var key in requiredCellPoses)
                if (!gridData.NodesData.Any(x => x.Key == key))
                {
                    var nodeData = AddRandomAllowedNodeData(gridData, key);
                }

            foreach (var key in excessCellPoses)
            {
                var item = gridData.NodesData.Find(x => x.Key == key);

                if (item != null)
                {
                    gridData.RemoveNodeData(item);
                }
            }
        }

        (bool hasChanged, int heightKeyChange, int widthKeyChange) UpdateGridDimensionsParameters()
        {
            if (gridData == null) return new(false, 0, 0);
            if (!gridData.NodesData.Any()) return new(false, 0, 0);

            var hasChanged = false;
            var previousHeight = gridData.Height;
            var previousWidth = gridData.Width;


            var allKeys = new HashSet<Vector2Int>();

            foreach (var node in gridData.NodesData)
            {
                foreach (var value in node.Values)
                {
                    var occupied = node.Key.GetOccupiedKeys(value.Size);
                    foreach (var key in occupied) allKeys.Add(key);
                }
            }

            var minX = allKeys.Min(k => k.x);
            var maxX = allKeys.Max(k => k.x);
            var minY = allKeys.Min(k => k.y);
            var maxY = allKeys.Max(k => k.y);

            var height = maxY - minY + 1;
            var width = maxX - minX + 1;

            var widthChange = width - previousWidth;
            var heightChange = height - previousHeight;

            var widthKeyChange = 0 - minX;
            var heightKeyChange = 0 - minY;

            hasChanged = widthChange != 0 || heightChange != 0;

            gridData.Width = Width = width;
            gridData.Height = Height = height;

            return new(hasChanged, heightKeyChange, widthKeyChange);
        }
        void RecenterGridItself()
        {
            if (tileMap == null || tileMap.layoutGrid == null) return;

            var grid = tileMap.layoutGrid;
            grid.transform.position = tileMap.GetGridCenterOffset(gridData.Width, gridData.Height);
        }

        /// <summary>
        /// this is due to inverse vertical movement. e.g. Key(0,1) is placed below Key(0,0)
        /// </summary>
        /// <param name="heightKeyChange"></param>
        /// <param name="widthKeyChange"></param>

        private void ShiftKeys(int heightKeyChange, int widthKeyChange)
        {
            foreach (var nodeData in gridData.NodesData)
            {
                var key = nodeData.Key;
                var newKey = new Vector2Int(key.x + widthKeyChange, key.y + heightKeyChange);
                nodeData.Key = newKey;
            }

            foreach (var instance in nodeValueInstancesCache)
            {
                var key = instance.OriginKey;
                var newKey = new Vector2Int(key.x + widthKeyChange, key.y + heightKeyChange);
                instance.SetKey(newKey);
            }
        }

        void DeleteAllValues()
        {
            var count = valuesParent.transform.childCount;

            for (int i = count - 1; i >= 0; i--)
            {
                var val = valuesParent.transform.GetChild(i);
                if (val != null) DestroyImmediate(val.gameObject);
            }
            nodeValueInstancesCache.Clear();
        }

        void RefreshNodeValues()
        {
            DeleteAllValues();
            gridData.Create(tileMap, parent: valuesParent.transform, onEntityCreated: (instance) => { nodeValueInstancesCache.Add(instance); });
        }

        private void OnPaintedByBrush(WatchingBrush.Args args)
        {
            if (gridData == null) return;
            if (tileMap == null) return;
            if (args.Tile == null) return;

            Vector2Int originKey = args.CellPos.GetKeyByCellPos(gridData.Height);

            var so = gridLookup.AllSOs.GetNodeValueSOByTile(args.Tile);
            var provided = valueProvider.Provide();
            var valueData = new ValueData(so, provided.Options);

            var occupiedKeys = originKey.GetOccupiedKeys(valueData.Size);
            var overlappingInstances = nodeValueInstancesCache
                  .Where(inst => inst.OccupiedKeys.Any(k => occupiedKeys.Contains(k)))
                  .Distinct()
                  .ToList();

            foreach (var inst in overlappingInstances)
                EraseInstance(inst);

            NodeData nodeData = gridData.GetNodeData(originKey);

            if (nodeData == null)
            {
                nodeData = new NodeData(originKey);
                gridData.AddNodeData(nodeData);
            }

            nodeData.Values = new List<ValueData>();
            nodeData.Values.Add(valueData);

            CreateAndInitializeNodeValueInstance(valueData, originKey);
        }
        private void OnErasedByBrush(WatchingBrush.Args args)
        {
            var originKey = args.CellPos.GetKeyByCellPos(gridData.Height);
            var instance = nodeValueInstancesCache.FirstOrDefault(x => x.OccupiedKeys.Contains(originKey));
            if (instance == null) return;
            EraseInstance(instance);
        }

        private void EraseInstance(BoardEntity instance)
        {
            if (instance == null) return;

            if (gridData != null)
            {
                foreach (var key in instance.OccupiedKeys)
                {
                    NodeData nodeData = gridData.GetNodeData(key);
                    if (nodeData != null) gridData.RemoveNodeData(nodeData);
                }
            }
            nodeValueInstancesCache.Remove(instance);
            DestroyImmediate(instance.NodeValue.gameObject);
        }
        private void OnBrushFinished()
        {
            var changes = UpdateGridDimensionsParameters();

            if (changes.hasChanged)
            {
                ShiftKeys(heightKeyChange: changes.heightKeyChange, widthKeyChange: changes.widthKeyChange);
                InitializeGrid(gridData);
            }
        }

        private void OnSelectedByBrush(WatchingBrush.Args args)
        {
            // selectedValue = nodeValueInstancesCache.Find(x => x.CellPos == args.CellPos);
        }


        void CreateAndInitializeNodeValueInstance(ValueData valueData, Vector2Int key)
        {
            var size = valueData.Size;
            var worldPos = key.GetWorldPosWithSize(size, gridData.Height, tileMap);
            var instance = valueData.Create(key: key, worldPos: worldPos, parent: valuesParent.transform);

            nodeValueInstancesCache.Add(instance);
        }


        #endregion


        #region Modificaition

        protected void HandMouseEvents(SceneView sceneView)
        {
            if (EditorApplication.isPlaying) return;

            HandleRightClick();

            if (!modificationMode) return;

            var e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            var controlID = GUIUtility.GetControlID(FocusType.Passive);
            var eventType = e.GetTypeForControl(controlID);

            if (eventType == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                HandleLeftClick();              
            }

            sceneView.Repaint();
            Repaint();
        }

        void HandleRightClick()
        {
            if (tileMap == null) return;

            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                isEarasing = !isEarasing;
                if (isEarasing) TilemapEditorTool.ToggleActiveEditorTool(typeof(EraseTool));
                else TilemapEditorTool.ToggleActiveEditorTool(typeof(PaintTool));
            }
        }

        void HandleLeftClick()
        {
            if (TryGetSceneViewWorldPosUnderMouse(tileMap, out var worldPos))
            {
                var entity = FindClosestBoardInstance(worldPos);

                if (entity == null || entity.NodeValue == null)
                {
                    Nullify();
                    return;
                }

                selectedValueInstance = entity;

                GameObject[] selection = new GameObject[1];
                selection[0] = entity.NodeValue.gameObject;
                Selection.objects = selection;
            }
            else
            {
                Nullify();
            }

            void Nullify()
            {
                Selection.objects = null;
                selectedValueInstance = null;
            }
        }


        public static bool TryGetSceneViewWorldPosUnderMouse(Tilemap tilemap, out Vector3 worldHit)
        {
            worldHit = default;

            if (tilemap == null) return false;
            if (SceneView.lastActiveSceneView == null) return false;

            Camera sceneCam = SceneView.lastActiveSceneView.camera;
            Event e = Event.current;
            if (e == null) return false;

            Vector2 mousePosition = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane;

            switch (tilemap.orientation)
            {
                case Tilemap.Orientation.XY: plane = new Plane(Vector3.forward, tilemap.transform.position); break;
                case Tilemap.Orientation.XZ: plane = new Plane(Vector3.up, tilemap.transform.position); break;
                case Tilemap.Orientation.YX: plane = new Plane(-Vector3.forward, tilemap.transform.position); break;
                case Tilemap.Orientation.ZX: plane = new Plane(-Vector3.up, tilemap.transform.position); break;
                default: plane = new Plane(tilemap.transform.up, tilemap.transform.position); break;
            }

            if (plane.Raycast(ray, out float distance))
            {
                worldHit = ray.GetPoint(distance);
                return true;
            }

            return false;
        }


        BoardEntity FindClosestBoardInstance(Vector3 worldPos)
        {
            if (tileMap == null || gridData == null) return null;
            Vector2Int key = worldPos.GetKeyByWorldPosition(gridData.Height, tileMap);
            return nodeValueInstancesCache.FirstOrDefault(x => x.OccupiedKeys.Contains(key));
        }

        #region Old

        //RaycastHit GetHit3D(Vector3 mousePos)
        //{
        //    Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
        //    Physics.Raycast(ray, out RaycastHit hit, 100.0f);
        //    return hit;
        //}
        //void OnClickHit3D(RaycastHit hit)
        //{
        //    if (hit.collider == null)
        //    {
        //        Selection.objects = null;
        //        return;
        //    }

        //    if (hit.collider.TryGetComponent(out NodeValueCollider nCollider))
        //    {
        //        var nodeValue = nCollider.GetNodeValue();

        //        if (nodeValue != null)
        //        {
        //            selectedValueInstance = nodeValueInstancesCache.Find(x => x.NodeValue == nodeValue);

        //            GameObject[] selection = new GameObject[1];
        //            selection[0] = nCollider.GetNodeValue().gameObject;
        //            Selection.objects = selection;
        //        }
        //    }

        //} 
        #endregion

        public void OnModificationModeOff()
        {
            if (tileMap == null) return;
            modificationMode = false;
            GameObject[] selection = new GameObject[1];
            selection[0] = tileMap.gameObject;
            Selection.objects = selection;
            selectedValueInstance = null;
        }

        private void HandleModification(SceneView sceneView)
        {
            if (gridData == null) return;
            if (selectedValueInstance == null) return;

            Handles.BeginGUI();

            var rect = new Rect(sceneView.position.width - 220, sceneView.position.height - 280, 0, 0);
            var window = GUILayout.Window(0, rect, DrawNodeData, "Node Data");

            Handles.EndGUI();
        }
        private void DrawNodeData(int index)
        {
            Vector2Int key = selectedValueInstance.OriginKey;
            NodeData nodeData = gridData.GetNodeData(key);

            if (nodeData == null) return;

            if (nodeData.Values != null && nodeData.Values.Any())
            {
                // because a value might be deleted by user
                for (int i = nodeData.Values.Count - 1; i >= 0; i--)
                {
                    GUILayout.BeginHorizontal();

                    ValueData valueData = nodeData.Values[i];
                    NodeValueSO so = valueData.SO;
                    TileBase tileBase = so.GetTile();
                    Color? color = null;
                    if (tileBase is Tile tile) color = tile.color;

                    ItemDesc(so.name, color);

                    var optionChanged = OptionsInput(ref valueData.Options);
                    if (optionChanged)
                    {
                        BoardEntity instance = nodeValueInstancesCache.Find(x => x.NodeValue.ValueData == valueData);
                        if (instance != null) instance.NodeValue.OnCreated(valueData);
                    }

                    GUILayout.Space(20);
                    commonAssets.DrawRemoveButton(20, 20, () => { RemoveFromNodeData(nodeData, valueData); });

                    GUILayout.EndHorizontal();

                }
            }


            EditorCommonAssets.MidSpace();

            commonAssets.DrawAddButton(width: 200, height: 20, () => { AddToNodeData(nodeData); });
        }
        void ItemDesc(string name, Color? color)
        {
            if (color.HasValue) EditorCommonAssets.SetBackgroundColor(color.Value);
            EditorGUILayout.LabelField($"", EditorCommonAssets.layoutStyle, GUILayout.Width(20));
            EditorGUILayout.LabelField(name, GUILayout.Width(80));
        }
        bool OptionsInput(ref string options)
        {
            GUILayout.FlexibleSpace();
            string newValue = EditorGUILayout.TextField(options);
            if (EditorGUI.EndChangeCheck())
                if (newValue != options)
                {
                    options = newValue;
                    return true;
                }
            return false;
        }

        void RemoveFromNodeData(NodeData nodeData, ValueData valueData)
        {
            BoardEntity instance = nodeValueInstancesCache.Find(x => x.NodeValue.ValueData == valueData);
            if (nodeData.Values.Contains(valueData)) nodeData.Values.Remove(valueData);
            if (instance != null)
            {
                nodeValueInstancesCache.Remove(instance);
                DestroyImmediate(instance.NodeValue.gameObject);
            }
            if (!nodeData.Values.Any())
            {
                gridData.RemoveNodeData(nodeData);
                UpdateGridData();
            }
        }

        void AddToNodeData(NodeData nodeData)
        {
            Action<NodeValueEditorCell> onAdded = (cell) =>
            {
                var so = cell.Value;
                if (so == null) return;

                var valueData = new ValueData(so, null);
                if (nodeData.Values == null) nodeData.Values = new List<ValueData>();

                nodeData.Values.Add(valueData);

                if (selectedValueInstance != null)
                {
                    //  var cellPos = selectedValueInstance.CellPos;
                    CreateAndInitializeNodeValueInstance(valueData, nodeData.Key/*, cellPos: cellPos*/);
                }
            };

            var allNodeValueSOs = gridLookup.AllSOs;
            var allNodeValueOptions = allNodeValueSOs.Select(value => new NodeValueEditorCell(assets: commonAssets, value)).ToList();

            NodeValueSelectionPopup.ShowWindow<NodeValueSelectionPopup>(allNodeValueOptions, onAdded);
        }

        #endregion


        #region Saving


        [PropertyOrder(-10)]
        [HorizontalGroup]
        [Button(ButtonSizes.Large), GUIColor(EditorCommonAssets.k_GreenColor)]
        public void Save()
        {
            if (gridData == null) return;
            if (valueProvider != null) gridData.ValueProvider = valueProvider.DeepCopy();

            gridData.EnsureNoUndefined();
            onSaveCallback?.Invoke(gridData);
            InitializeGrid(gridData);

            #region Alternative

            //var asset = ScriptableObject.CreateInstance<Level>();
            //asset.HexGridData = data;

            //string path = EditorUtility.SaveFilePanelInProject(
            //    "Save Asset",
            //    "Level",
            //    "asset",
            //    "Select where to save the asset");

            //if (!string.IsNullOrEmpty(path))
            //{
            //    AssetDatabase.CreateAsset(asset, path);
            //    AssetDatabase.SaveAssets();
            //    Debug.Log("Saved ScriptableObject to: " + path);
            //} 
            #endregion
        }

        #endregion


        #region Draw Editor

        protected override void DrawEditor(int index)
        {
            base.DrawEditor(index);

            DrawModificationMode();
        }

        void DrawModificationMode()
        {
            EditorCommonAssets.HorizontalLine();
            EditorCommonAssets.Space();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = modificationMode ? commonAssets.GreenColor : Color.white;
            if (GUILayout.Button("Modification Mode", GUILayout.Height(50)))
            {
                modificationMode = !modificationMode;
                if (modificationMode == false) OnModificationModeOff();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorCommonAssets.Space();
            EditorCommonAssets.HorizontalLine();

        }



        #endregion


        #region Getters

        public Tilemap GetTileMap() => tileMap;


        public IEnumerable<ValueDropdownItem<Type>> GetAvailableProviderTypes()
        {
            return GridData.GetAvailableProviderTypes();
        }

        #endregion


    }







}
