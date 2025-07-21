using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Tilemaps;

using static MadApper.GridSystem.GridData;
using static UnityEngine.EventSystems.EventTrigger;


namespace MadApper.GridSystem
{
    public abstract class GridFiller<TFiller> : MonoBehaviour where TFiller : GridFiller<TFiller>
    {
        const string k_Tag = "Grid Filler";
        const string k_ValuesParentId = "Runtime - Grid Values Parent";

        [AutoFind][SerializeField] protected Tilemap tileMap;


        [HideInInspector][SerializeField] GameObject _valuesParent;
        protected GameObject valuesParent
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
        protected GridData gridData;

        [ShowInInspector][FoldoutGroup("Refs")] protected Dictionary<Vector2Int, Node> nodes;
        [ShowInInspector][FoldoutGroup("Refs")] protected HashSet<BoardEntity> boardEntities;
        [ShowInInspector][FoldoutGroup("Refs")] protected HashSet<BoardEntity> removedFromBoard;

        [FoldoutGroup("Random SO")][SerializeField] protected NodeValueSO randomSO;

        [FoldoutGroup("Falling")][SerializeField] protected bool wouldEntitiesFall = false;
        [FoldoutGroup("Falling")][SerializeField] protected float stepDuration = 0.1f;
        [FoldoutGroup("Falling")][SerializeField] protected int maxScanHeightForBlockingDiagonalFall = 4;

        protected readonly List<BoardEntity> fallingEntitiesStep = new();
        protected readonly HashSet<BoardEntity> fallingEntitiesTotal = new();
        protected readonly List<BoardEntity> toRemoveMovingEntities = new();

        protected CancellationToken injetctedCToken;
        protected CancellationTokenSource fallingCts;

        public static TFiller s_Instance;
        protected static List<Action<TFiller>> s_GridCreatedSubscribers = new();

        public static Action<TFiller> s_OnGridReady;
        public static Func<Vector2Int, List<BoardEntity>> s_GetBoardEntityWithKey;
        public static Func<HashSet<BoardEntity>> s_GetBoardEntities;
        public static Action<BoardEntity> s_RemoveNodeValueFromBoard;
        public static Action<BoardEntity> s_PlaceNodeValueInBoard;


        protected virtual void OnEnable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions += InitializeGridFiller;

            s_RemoveNodeValueFromBoard += OnTryRemoveBoardEntityFromBoard;
            s_PlaceNodeValueInBoard += OnPlaceNodeValueInBoard;
            s_GetBoardEntityWithKey += GetBoardEntitiesWithKey;
            s_GetBoardEntities += GetBoardEntities;
        }
        protected virtual void OnDisable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions -= InitializeGridFiller;

            s_RemoveNodeValueFromBoard -= OnTryRemoveBoardEntityFromBoard;
            s_PlaceNodeValueInBoard -= OnPlaceNodeValueInBoard;
            s_GetBoardEntityWithKey -= GetBoardEntitiesWithKey;
            s_GetBoardEntities -= GetBoardEntities;
        }
        protected virtual void Update()
        {
            TryMoveFallingEntities();
        }



        public abstract TFiller GetSelfFiller();


        protected virtual void Cleanup()
        {
            StopFalling();

            if (nodes == null) nodes = new();
            if (boardEntities == null) boardEntities = new();
            if (removedFromBoard == null) removedFromBoard = new();

            foreach (var pair in nodes)
                foreach (var entity in pair.Value.Entities)
                    if (entity != null)
                        entity.NodeValue.Despawn();

            foreach (var item in boardEntities)
                if (item.NodeValue != null) item.NodeValue.Despawn();

            foreach (var item in removedFromBoard)
                if (item.NodeValue != null) item.NodeValue.Despawn();

            foreach (Transform item in valuesParent.transform)
                Destroy(item.gameObject);

            nodes.Clear();
            boardEntities.Clear();
            removedFromBoard.Clear();
            fallingEntitiesTotal.Clear();

            tileMap.ClearAllTiles();
        }
        protected void StopFalling()
        {
            if (fallingCts != null)
            {
                fallingCts.Cancel();
                fallingCts.Dispose();
            }
            fallingCts = null;
        }



        private void InitializeGridFiller(GridDataInitializationQueue actions)
        {
            s_Instance = null;

            injetctedCToken = actions.GetCToken();
            var gridData = actions.GetSender();
            var action = new ActionAsync.Builder(injetctedCToken)
                .SetTask((cToken) => InitializeGrid(gridData, cToken))
                .Priority(5)
                .Tag(k_Tag)
                .Build();

            actions.Append(action);
        }



        async UniTask InitializeGrid(GridData gridData, CancellationToken cToken)
        {
            Cleanup();

            this.gridData = gridData;

            try
            {
                var alteredGridData = await PreCreate(gridData).AttachExternalCancellation(cToken);
                await Create(alteredGridData, cToken).AttachExternalCancellation(cToken);
                await PostCreate(cToken).AttachExternalCancellation(cToken);
            }
            catch (Exception) { }
        }

        protected virtual async UniTask<GridData> PreCreate(GridData gridData)
        {
            gridData.ValueProvider.Initialize(randomSO, IMutableNodeValue.k_RandomTag);

            var newGridData = new GridData(gridData.Width, gridData.Height);

            foreach (var n in gridData.NodesData)
            {
                var newNodeData = new NodeData(n.Key, n.IsGap);

                foreach (var v in n.Values)
                {
                    var value = v;
                    var altered = TryAlterValueData(v);
                    if (altered != null) value = altered;
                    newNodeData.Values.Add(value);
                }
                newGridData.NodesData.Add(newNodeData);
            }

            return newGridData;
        }

        protected virtual async UniTask Create(GridData gridData, CancellationToken token)
        {
            gridData.CreateNodes(tileMap, onNodeCreated: OnNodeCreated);
            gridData.Create(tileMap, parent: valuesParent.transform, onEntityCreated: (e) => AddEntity(e));
            OnCreated();
        }
        protected virtual async UniTask PostCreate(CancellationToken token)
        {
            s_OnGridReady?.Invoke(GetSelfFiller());
        }


        protected virtual ValueData TryAlterValueData(ValueData valueData)
        {
            return IMutableNodeValue.s_TryAlteringValueData?.Invoke(valueData);
        }
        protected virtual void OnNodeCreated(Node node)
        {
            nodes[node.Key] = node;
        }

        protected void OnCreated()
        {
            var filler = GetSelfFiller();
            s_Instance = filler;
            foreach (var item in s_GridCreatedSubscribers) item?.Invoke(filler);
            s_GridCreatedSubscribers.Clear();
        }



        #region Falling

        public virtual async UniTask FallEntities()
        {
            StopFalling();
            fallingCts = new CancellationTokenSource();

            try
            {
                await FallEntitiesInternal()
                   .AttachExternalCancellation(fallingCts.Token);

            }
            catch (OperationCanceledException) { }
#if UNITY_EDITOR
            catch (Exception)
            {
                throw;
            }
#endif
        }

        long GetGCs(long before, out long delta)
        {
            long after = Profiler.GetMonoUsedSizeLong();
            delta = after - before;
            return after;
        }
        async UniTask FallEntitiesInternal()
        {
            if (boardEntities.Count == 0) return;

            int width = gridData.Width;
            int height = gridData.Height;
            var minY = nodes.Min(k => k.Key.y);

            var l = GetGCs(0, out var delta);

            while (!fallingCts.IsCancellationRequested)
            {
                fallingEntitiesStep.Clear();

                for (int y = height - 2; y >= minY; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var fromKey = new Vector2Int(x, y);

                        if (!nodes.TryGetValue(fromKey, out var node)) continue;

                        var entities = node.Entities;

                        for (int i = entities.Count - 1; i >= 0; i--)
                        {
                            var entity = entities[i];

                            if (!entity.HasValue || !entity.NodeValue.CanFall) continue;
                            if (entity.OriginKey != fromKey) continue;

                            var nextNonGapNode = TryGetNextBottomNonGapNode(fromKey);
                            var checkDiagonals = false;

                            if (nextNonGapNode != null)
                            {
                                if (CanFallTo(nextNonGapNode, entity))
                                    MoveEntityTo(entity, nextNonGapNode);
                                else
                                    checkDiagonals = true;
                            }
                            else
                                checkDiagonals = true;

                            if (checkDiagonals)
                            {
                                var nextDiagonalNode = TryGetNextDiagonalNodeToFallTo(entity);

                                if (nextDiagonalNode != null)
                                    MoveEntityTo(entity, nextDiagonalNode);
                            }
                        }
                    }
                }

                if (fallingEntitiesStep.Count == 0) break;

                await UniTask.WaitForSeconds(stepDuration, cancellationToken: fallingCts.Token);

                foreach (var item in fallingEntitiesStep)
                    item.NodeValue.OnFallingStepFinished();

                TryClearFallingTotal();
            }

            TryClearFallingTotal();

            void TryClearFallingTotal()
            {
                toRemoveMovingEntities.Clear();

                foreach (var item in fallingEntitiesTotal)
                    if (!fallingEntitiesStep.Contains(item))
                        toRemoveMovingEntities.Add(item);

                foreach (var item in toRemoveMovingEntities)
                {
                    var nodeValue = item.NodeValue;

                    if (nodeValue != null)
                    {
                        nodeValue.transform.position = item.TargetPos;                       
                        item.NodeValue?.OnFallingAllFinished();
                    }
                  
                    fallingEntitiesTotal.Remove(item);
                }
            }
        }

        Node TryGetNextBottomNonGapNode(Vector2Int fromKey)
        {
            Vector2Int checkKey = fromKey + Vector2Int.up;

            while (checkKey.y < gridData.Height)
            {
                // it may been started off-grid
                if (!nodes.TryGetValue(checkKey, out var node))
                {
                    return null;
                }
                if (node.IsGap)
                {
                    checkKey.y++;
                    continue;
                }

                return node;
            }

            return null;
        }

        public bool CanFallTo(Node targetNode, BoardEntity entity)
        {
            if (!entity.NodeValue.CanFall) return false;

            var newOriginKey = targetNode.Key;
            var offset = newOriginKey - entity.OriginKey;

            foreach (var oldKey in entity.OccupiedKeys)
            {
                var neighbourKey = oldKey + offset;
                if (!IsInsideGrid(neighbourKey)) return false;
                if (!nodes.TryGetValue(neighbourKey, out var neighbourNode)) return false;
                if (!neighbourNode.CanAccept(entity)) return false;
            }
            return true;
        }
        bool IsInsideGrid(Vector2Int key)
        {
            return key.x >= 0 && key.x < gridData.Width &&/* key.y >= 0 &&*/ key.y < gridData.Height;
        }

        Node TryGetNextDiagonalNodeToFallTo(BoardEntity entity)
        {
            var origin = entity.OriginKey;
            var leftDiag = new Vector2Int(origin.x - 1, origin.y + 1);
            var rightDiag = new Vector2Int(origin.x + 1, origin.y + 1);
            bool tryLeftFirst = UnityEngine.Random.value < 0.5f;

            if (tryLeftFirst)
            {
                var node = TryGetDiagonalNode(entity, leftDiag);
                if (node != null) return node;
                return TryGetDiagonalNode(entity, rightDiag);
            }
            else
            {
                var node = TryGetDiagonalNode(entity, rightDiag);
                if (node != null) return node;
                return TryGetDiagonalNode(entity, leftDiag);
            }
        }

        Node TryGetDiagonalNode(BoardEntity entity, Vector2Int targetKey)
        {
            if (!IsInsideGrid(targetKey)) return null;
            if (!nodes.TryGetValue(targetKey, out var node)) return null;
            if (!CanFallTo(node, entity)) return null;
            if (!IsOriginColumnBlockedBelow(entity)) return null;
            if (HasPendingVerticalFallFromAbove(entity, targetKey)) return null;

            return node;
        }
        bool IsOriginColumnBlockedBelow(BoardEntity entity)
        {
            var origin = entity.OriginKey;
            var size = entity.Size;

            for (int dx = 0; dx < size.x; dx++)
            {
                int x = origin.x + dx;

                for (int y = origin.y + size.y; y < gridData.Height; y++)
                {
                    var key = new Vector2Int(x, y);

                    if (!nodes.TryGetValue(key, out var node)) continue;

                    foreach (var other in node.Entities)
                    {
                        if (other == entity || other.NodeValue == null) continue;

                        var downKey = other.OriginKey + Vector2Int.up;

                        if (IsInsideGrid(downKey) && nodes.TryGetValue(downKey, out var downNode))
                        {
                            if (CanFallTo(downNode, other))
                                return false; // something below could fall next frame                           
                        }

                        return true; // blocked
                    }
                }
            }

            return true; // nothing below
        }
        bool HasPendingVerticalFallFromAbove(BoardEntity self, Vector2Int newOriginKey)
        {
            var size = self.Size;

            for (int dx = 0; dx < size.x; dx++)
            {
                int x = newOriginKey.x + dx;

                for (int y = newOriginKey.y - 1; y >= newOriginKey.y - maxScanHeightForBlockingDiagonalFall - 1; y--)
                {
                    var key = new Vector2Int(x, y);

                    if (!IsInsideGrid(key)) continue;
                    if (!nodes.TryGetValue(key, out var node)) continue;

                    foreach (var other in node.Entities)
                    {
                        if (other == self || !other.HasValue)
                            continue;

                        if (!other.NodeValue.CanFall) return false;

                        Vector2Int downKey = other.OriginKey + Vector2Int.up;

                        if (!IsInsideGrid(downKey)) continue;
                        if (!nodes.TryGetValue(downKey, out var downNode)) continue;

                        if (CanFallTo(downNode, other))
                        {
                            return true; // ✅ Something above could fall soon
                        }
                    }
                }
            }

            return false;
        }



        protected virtual void MoveEntityTo(BoardEntity entity, Node node)
        {
            var targetKey = node.Key;

            ReassignEntity(entity, targetKey);

            var pos = GetPositionWithSize(entity, targetKey);

            entity.SetTargetPos(pos, stepDuration);
            entity.NodeValue.OnFallingStepStarted();

            fallingEntitiesStep.Add(entity);

            if (!fallingEntitiesTotal.Contains(entity))
                entity.NodeValue.OnFallingAllStarted();

            fallingEntitiesTotal.Add(entity);
        }


        protected virtual void TryMoveFallingEntities()
        {
            if (!wouldEntitiesFall) return;

          //  toRemoveMovingEntities.Clear();

            foreach (var entity in fallingEntitiesTotal)
            {
                //if (!entity.HasValue)
                //{
                //    toRemoveMovingEntities.Add(entity);
                //    continue;
                //}

                var pos = entity.NodeValue.transform.position;
                var targetpos = entity.TargetPos;
                var speed = entity.FallSpeed;

                //if (pos == targetpos)
                //{
                //    entity.NodeValue.transform.position = targetpos;
                //    toRemoveMovingEntities.Add(entity);
                //    continue;
                //}

                float fallSpeed = 1 / stepDuration;

                entity.NodeValue.transform.position = Vector3.MoveTowards(pos, targetpos, fallSpeed * Time.deltaTime);
            }

            //foreach (var item in toRemoveMovingEntities)
            //{                
            //    fallingEntitiesTotal.Remove(item);
            //}

        }



        #endregion



        #region Add Remove


        public NodeValue CreateNodeValue(ValueData valueData, Vector2Int key)
        {
            if (gridData == null) return null;
            if (valueData == null || valueData.SO == null) return null;

            Transform parent = valuesParent.transform;
            NodeValue value = valueData.Create(parent);
            Vector3 worldPos = key.GetWorldPosWithSize(valueData.Size, gridData.Height, tileMap);
            value.Place(worldPos, parent);

            return value;
        }
        public BoardEntity CreateEntity(ValueData valueData, Vector2Int key)
        {
            var value = CreateNodeValue(valueData, key);
            if (value == null) return null;

            return new BoardEntity(value, key);
        }

        public BoardEntity CreateEntityAndAdd(ValueData valueData, Vector2Int key)
        {
            var entity = CreateEntity(valueData, key);
            if (entity == null) return null;

            AddEntity(entity);

            return entity;
        }



        public void AddEntity(BoardEntity entity, bool sanityCheck = false)
        {
            foreach (var key in entity.OccupiedKeys)
            {
                if (sanityCheck)
                {
                    if (nodes.TryGetValue(key, out var node))
                        node.Add(entity);
                }
                else
                {
                    nodes[key].Add(entity);
                }
            }

            boardEntities.Add(entity);
        }
        public void RemoveEntity(BoardEntity entity, bool sanityCheck = false)
        {
            foreach (var key in entity.OccupiedKeys)
            {
                if (sanityCheck)
                {
                    if (nodes.TryGetValue(key, out var node))
                        node.Remove(entity);
                }
                else
                {
                    nodes[key].Remove(entity);
                }
            }

            removedFromBoard.Add(entity);

            if (sanityCheck)
            {
                if (boardEntities.Contains(entity))
                    boardEntities.Remove(entity);
            }
            else
                boardEntities.Remove(entity);
        }
        public void ReassignEntity(BoardEntity entity, Vector2Int newKey)
        {
            foreach (var key in entity.OccupiedKeys)
                if (nodes.TryGetValue(key, out var node))
                    node.Remove(entity);

            entity.SetKey(newKey);

            foreach (var key in entity.OccupiedKeys)
                if (nodes.TryGetValue(key, out var node))
                    node.Add(entity);
        }



        public void OnTryRemoveBoardEntityFromBoard(BoardEntity instance)
        {
            if (instance == null) return;
            RemoveEntity(instance);
        }
        public void OnPlaceNodeValueInBoard(BoardEntity instance)
        {
            if (instance == null || gridData == null || tileMap == null) return;

            var key = instance.OriginKey;
            var size = instance.NodeValue.ValueData.Size;
            Vector3 worldPos = key.GetWorldPosWithSize(size, gridData.Height, tileMap);
            instance.Place(worldPos, valuesParent.transform);
            AddEntity(instance);
        }


        #endregion

        #region Getters
        public Tilemap GetTilemap() => tileMap;
        public GridData GetGridData() => gridData;
        public int GetWidth() => gridData.Width;
        public int GetHeight() => gridData.Height;
        public Transform GetValuesParent() => valuesParent.transform;
        public List<BoardEntity> GetBoardEntitiesWithKey(Vector2Int key)
        {
            if (!nodes.TryGetValue(key, out var node)) return null;
            return node.Entities;
        }
        public bool IsBoardCleared() => boardEntities.Count == 0;
        public Dictionary<Vector2Int, Node> GetNodes() => nodes;
        public HashSet<BoardEntity> GetBoardEntities() => boardEntities;

        public bool IsOnBoard(NodeValue value)
        {
            if (value.BoardEntity == null) return false;
            return boardEntities.Contains(value.BoardEntity);
        }

        public Vector3 GetPositionWithSize(Vector2Int key, Vector2Int size)
        {
            return key.GetWorldPosWithSize(size, gridData.Height, tileMap);
        }
        public Vector3 GetPositionWithSize(BoardEntity entity, Vector2Int key)
        {
            return GetPositionWithSize(key, entity.Size);
        }
        public bool HasReachedBottom(BoardEntity entity)
        {
            if (entity == null || !entity.HasValue || gridData == null)
                return false;

            int bottomY = gridData.Height - 1;

            foreach (var key in entity.OccupiedKeys)
            {
                if (key.y == bottomY)
                    return true;
            }

            return false;
        }

        #endregion

        #region Subscribers

        public static void OnSubscribeToCreated(Action<TFiller> onCreated)
        {
            if (s_Instance != null)
            {
                onCreated?.Invoke(s_Instance);
                return;
            }

            s_GridCreatedSubscribers.Add(onCreated);
        }

        #endregion

        #region Inspector
        public void z_Cleanup() => Cleanup();

        #endregion

    }

    [Serializable]
    public class Node
    {
        [ShowInInspector] public Vector2Int Key { get; private set; }
        [ShowInInspector] public Vector3 WorldPos { get; private set; }
        [ShowInInspector] public bool IsGap { get; private set; }
        [ShowInInspector] public List<BoardEntity> Entities { get; private set; }
        public bool HasEntities => Entities.Count > 0;

        public Node(Vector2Int key, Vector3 worldPos, bool isGap)
        {
            Key = key;
            WorldPos = worldPos;
            IsGap = isGap;
            Entities = new();
        }

        public void Add(BoardEntity entity)
        {
            Entities.Add(entity);
        }
        public void Remove(BoardEntity entity)
        {
            if (!Entities.Contains(entity)) return;
            Entities.Remove(entity);
        }
        public bool CanAccept(BoardEntity entity)
        {
            if (IsGap) return false;

            // TODO : add for multiple acceptance based on heirarchy
            foreach (var other in Entities)
                if (other.NodeValue != entity.NodeValue)
                    return false;

            return true;
        }

        public override string ToString()
        {
            return $"{Key}";
        }

    }


    [Serializable]
    public class BoardEntity
    {
        /// <summary>
        /// Top-Left key based on the NodeValue's size in the grid
        /// </summary>
        [ShowInInspector] public Vector2Int OriginKey { get; private set; }
        [ShowInInspector] public NodeValue NodeValue { get; private set; }
        [ShowInInspector] public Vector3 TargetPos { get; private set; }
        [ShowInInspector] public float FallDuration { get; private set; }
        [ShowInInspector] public float FallSpeed { get; private set; }

        public bool HasValue => NodeValue != null;
        public Vector2Int Size => NodeValue.ValueData.Size;

        private bool resetCachedOccupiedKeys = true;

        private List<Vector2Int> _cachedOccupiedKeys = new();

        // <summary>
        /// The keys this entity occupies in the grid.
        /// Automatically updates when the OriginKey is changed.
        /// </summary>
        public IReadOnlyList<Vector2Int> OccupiedKeys
        {
            get
            {

                if (resetCachedOccupiedKeys)
                {
                    _cachedOccupiedKeys.Clear();
                    foreach (var key in OriginKey.GetOccupiedKeys(Size)) _cachedOccupiedKeys.Add(key);
                    resetCachedOccupiedKeys = false;
                }
                return _cachedOccupiedKeys;
            }
        }

        public BoardEntity(NodeValue nodeValue, Vector2Int key)
        {
            SetNodeValue(nodeValue);
            SetKey(key);
        }

        void SetNodeValue(NodeValue nodeValue)
        {
            NodeValue = nodeValue;
            NodeValue.LinkBoardEntity(this);
        }
        public void SetKey(Vector2Int key)
        {
            OriginKey = key;
            FlagResetOccupiedKeys();
            NodeValue.OnKeySet(key);
        }

        public void ReplaceNodeValue(NodeValue nodeValue)
        {
            if (NodeValue != null)
            {
                NodeValue.UnlinkBoardEntity();
                NodeValue.Despawn();
            }

            SetNodeValue(nodeValue);
            FlagResetOccupiedKeys();
        }

        void FlagResetOccupiedKeys()
        {
            resetCachedOccupiedKeys = true;
        }

        public void Place(Vector3 pos, Transform parent)
        {
            NodeValue.Place(pos, parent);
            SetTargetPos(pos, 0);
        }

        public void SetTargetPos(Vector3 pos, float duration)
        {
            FallDuration = duration;

            float distance = Vector3.Distance(pos, TargetPos);
            if (duration == 0) FallSpeed = int.MaxValue;
            else FallSpeed = distance / duration;

            TargetPos = pos;
        }


        public void OnDespawned()
        {

        }
    }

    public static partial class GridFillerExtentions
    {
        public static void CreateNodes(this GridData gridData, Tilemap tileMap, Action<Node> onNodeCreated)
        {
            var grid = tileMap.layoutGrid;
            grid.transform.position = tileMap.GetGridCenterOffset(gridData.Width, gridData.Height);

            foreach (var nData in gridData.NodesData)
            {
                var node = nData.Key.CreateNode(gridData.Height, tileMap, nData.IsGap);
                onNodeCreated(node);
            }
        }

        public static Node CreateNode(this Vector2Int key, int height, Tilemap tileMap, bool isGap)
        {
            Vector3 nodeWorldPos = key.GetWorldPosWithSize(Vector2Int.one, height, tileMap);
            var node = new Node(key, nodeWorldPos, isGap);
            return node;
        }

        public static void Create(this GridData gridData, Tilemap tileMap, Transform parent, Action<BoardEntity> onEntityCreated)
        {
            var grid = tileMap.layoutGrid;
            grid.transform.position = tileMap.GetGridCenterOffset(gridData.Width, gridData.Height);

            foreach (var nData in gridData.NodesData)
            {
                if (nData.Values == null) continue;

                Vector2Int key = nData.Key;

                foreach (var valData in nData.Values)
                {
                    Vector3 worldPos = key.GetWorldPosWithSize(valData.Size, gridData.Height, tileMap);
                    var entity = valData.Create(key: key, worldPos: worldPos, parent: parent);
                    if (entity == null) continue;
                    onEntityCreated?.Invoke(entity);
                }
            }
        }


        public static BoardEntity Create(this ValueData valueData, Vector2Int key, Vector3 worldPos, Transform parent)
        {
            if (valueData == null || valueData.SO == null) return null;

            var value = valueData.Create(parent);
            var entity = new BoardEntity(value, key);

            entity.Place(worldPos, parent);

            return entity;
        }




    }

}
