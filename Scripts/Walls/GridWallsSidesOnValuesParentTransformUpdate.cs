using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Tilemaps;

using static MadApper.GridSystem.GridData;

namespace MadApper.GridSystem
{
    public interface IGridFillerWithSideWalls
    {
        public static Action<ValueParentsTransformUpdateArgs> s_ValuesParentUpdates;

        public bool IsBoardCleared();
        public List<BoardEntity> GetBoardEntitiesWithKey(Vector2Int key);
        public HashSet<BoardEntity> GetBoardEntities();

        public struct ValueParentsTransformUpdateArgs
        {
            public IGridFillerWithSideWalls Filler;
            public Vector3 TargetPos;
            public float Duration;
        }
    }
    public class GridWallsSidesOnValuesParentTransformUpdate : MonoBehaviour
    {
        const string k_Tag = "Grid Walls";
        const string k_ValuesParentId = "Runtime - Grid Walls Parent";

        [AutoFind][SerializeField] protected Tilemap tileMap;
        [SerializeField] protected GridWallObject sideWallLeft, sideWallRight, topWall;
        [SerializeField] protected Vector3 sideOffset;
        [SerializeField] protected int topExtra = 2;
        [SerializeField] protected int bottomExtra = 2;

        [SerializeField] bool despawnExcess = true;

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

        GridData gridData;
        [ShowInInspector] Dictionary<Vector2Int, GridWallObject> sideWalls;
        List<GridWallObject> wallsFlatten;
        CancellationToken injetctedCToken;
        int foot;


        private void OnEnable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions += InitializeGridWalls;
            IGridFillerWithSideWalls.s_ValuesParentUpdates += UpdateWalls;
        }
        private void OnDisable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions -= InitializeGridWalls;
            IGridFillerWithSideWalls.s_ValuesParentUpdates -= UpdateWalls;
        }


        private void InitializeGridWalls(GridDataInitializationQueue actions)
        {
            gridData = actions.GetSender();
            var action = new ActionAsync.Builder(injetctedCToken)
                .SetTask((cToken) => Initialize(gridData, cToken))
                .Priority(1)
                .Tag(k_Tag)
                .Build();

            actions.Append(action);
        }


        void Cleanup()
        {
            valuesParent.transform.position = Vector3.zero;

            if (sideWalls == null) sideWalls = new();
            if (wallsFlatten == null) wallsFlatten = new();

            foreach (var item in wallsFlatten) if (item != null) item.Despawn();
            foreach (Transform item in valuesParent.transform) Destroy(item.gameObject);

            sideWalls.Clear();
            wallsFlatten.Clear();
        }

        private async UniTask Initialize(GridData gridData, CancellationToken cToken)
        {
            Cleanup();

            var width = gridData.Width;
            var height = gridData.Height;

            var grid = tileMap.layoutGrid;
            grid.transform.position = tileMap.GetGridCenterOffset(width, height);

            foot = height;

            InitSides(gridData);
            InitTopRow(gridData);
            InitExtra(gridData);

            await UniTask.Delay(1, cancellationToken: cToken);
        }

        protected GridWallObject CreateWalls(Vector2Int key, Vector3 offset, GridWallObject wallPrefab, bool isSideWall = false)
        {
            var size = Vector2Int.one;
            Vector3 worldPos = key.GetWorldPosWithSize(size, gridData.Height, tileMap);

            var wall = PoolGridWallObject.Get(wallPrefab.i_PoolID, wallPrefab, valuesParent.transform);
            wall.transform.position = worldPos + offset;

            wallsFlatten.Add(wall);

            if (isSideWall) sideWalls[key] = wall;

            return wall;
        }


        protected virtual void InitSides(GridData gridData)
        {
            var width = gridData.Width;
            var height = gridData.Height;

            for (int i = -topExtra; i < height + bottomExtra; i++)
            {
                Vector2Int leftkey = new Vector2Int(-1, i);
                Vector2Int rightkey = new Vector2Int(width, i);

                CreateWalls(leftkey, -sideOffset, sideWallLeft, true);
                CreateWalls(rightkey, sideOffset, sideWallRight, true);
            }
        }
        protected virtual void InitTopRow(GridData gridData)
        {
            var width = gridData.Width;
            var height = gridData.Height;

            for (int h = 0; h < topExtra; h++)
            {
                for (int i = 0; i < width; i++)
                {
                    Vector2Int key = new Vector2Int(i, -1 - h);
                    CreateWalls(key, Vector3.zero, topWall);
                }

                Vector2Int topLeftkey = new Vector2Int(-1, 0 - h);
                CreateWalls(topLeftkey, Vector3.zero, topWall);

                var xRight = (width & 1) == 0 ? -1 : 0;
                Vector2Int topRighttkey = new Vector2Int(width, xRight - h);
                CreateWalls(topRighttkey, Vector3.zero, topWall);
            }
        }
        protected virtual void InitExtra(GridData gridData) { }

        private void UpdateWalls(IGridFillerWithSideWalls.ValueParentsTransformUpdateArgs args)
        {
            valuesParent.transform.DOKill();
            valuesParent.transform.DOMove(args.TargetPos, args.Duration);

            if (!despawnExcess) return;

            var newFoot = args.Filler.GetBoardEntities().Max(x => x.OriginKey.y) + 1;
            var diff = foot - newFoot;
            var exFoot = foot;
            foot = newFoot;

            var width = gridData.Width;

            for (int i = 0; i < diff; i++)
            {
                Vector2Int leftkey = new Vector2Int(-1, exFoot - i);
                Vector2Int rightkey = new Vector2Int(width, exFoot - i);

                var left = sideWalls[leftkey];
                var right = sideWalls[rightkey];

                left.Despawn();
                right.Despawn();

                wallsFlatten.Remove(left);
                wallsFlatten.Remove(right);

                sideWalls.Remove(leftkey);
                sideWalls.Remove(rightkey);
            }
        }


        public void z_Cleanup() => Cleanup();

    }
}
