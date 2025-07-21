using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MadApper.GridSystem
{
    public static class GridUtils
    {

        #region Hex

        /// <summary>
        /// clockwise from topleft
        /// </summary>
        public static readonly Vector2Int[] s_HexAdjacentsEven = new Vector2Int[]
        {
            // topleft to topright
            new Vector2Int(-1,0), new Vector2Int(0,-1), new Vector2Int(1,0),
            // bottomright to bottomleft
            new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1)
        };

        /// <summary>
        /// clockwise from topleft
        /// </summary
        public static readonly Vector2Int[] s_HexAdjacentsOdd = new Vector2Int[]
        {
            // topleft to topright
            new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1),
             // bottomright to bottomleft
            new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(-1,0)
        };

        public static IEnumerable<Vector2Int> GetHexNeighborsKeys(this Vector2Int key)
        {
            var directions = (key.x & 1) == 0 ? s_HexAdjacentsEven : s_HexAdjacentsOdd;
            foreach (var dir in directions) yield return key + dir;
        }
        public static IEnumerable<Vector3> GetHexNeighborsWorldDirections(this Vector2Int key, int height, Tilemap tilemap)
        {
            var keys = GetHexNeighborsKeys(key);
            Vector3 fromWorld = GetWorldPos(key, height, tilemap);

            foreach (var toKey in keys)
            {
                Vector3 toWorld = GetWorldPos(toKey, height, tilemap);
                Vector3 direction = (toWorld - fromWorld).normalized;
                yield return direction;
            }
        }


        public static readonly Vector2Int[] s_HexAdjacentsBottomEven = new Vector2Int[]
        {            
             // bottomright to bottomleft
            new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1)
        };

        public static readonly Vector2Int[] s_HexAdjacentsBottomOdd = new Vector2Int[]
        {            
             // bottomright to bottomleft
            new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(-1,0)
        };



        public static readonly Vector2Int[] s_HexAdjacentsTopEven = new Vector2Int[]
        {            
            // topleft to topright
            new Vector2Int(-1,0), new Vector2Int(0,-1), new Vector2Int(1,0),
        };

        public static readonly Vector2Int[] s_HexAdjacentsTopOdd = new Vector2Int[]
        {            
            // topleft to topright
            new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1,-1),
        };

        public static IEnumerable<Vector2Int> GetHexTopNeighbors(this Vector2Int key)
        {
            var directions = (key.x & 1) == 0 ? s_HexAdjacentsTopEven : s_HexAdjacentsTopOdd;
            foreach (var dir in directions) yield return key + dir;
        }
        public static HashSet<Vector2Int> GetHexNeighborsWithinDepth(this Vector2Int startKey, int depth)
        {
            var visited = new HashSet<Vector2Int> { startKey };
            var frontier = new Queue<(Vector2Int key, int depth)>();
            frontier.Enqueue((startKey, 0));

            while (frontier.Count > 0)
            {
                var (current, currentDepth) = frontier.Dequeue();
                if (currentDepth >= depth) continue;

                foreach (var neighbor in current.GetHexNeighborsKeys())
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    frontier.Enqueue((neighbor, currentDepth + 1));
                }
            }

            visited.Remove(startKey); // if you don't want to include the startKey itself
            return visited;
        }

        public static Vector2Int GetClosestHexDirection(Vector2Int origin, Vector3 worldDir)
        {
            var directions = (origin.x & 1) == 0 ? s_HexAdjacentsEven : s_HexAdjacentsOdd;

            Vector2Int best = directions[0];
            float bestDot = float.MinValue;

            foreach (var dir in directions)
            {
                // Approximate world-space direction
                Vector3Int cellFrom = origin.GetCellPosByKey(10); // any height is fine here
                Vector3Int cellTo = (origin + dir).GetCellPosByKey(10);
                Vector3 dirWorld = ((Vector3)(cellTo - cellFrom)).normalized;

                float dot = Vector3.Dot(worldDir, dirWorld);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = dir;
                }
            }

            return best;
        }

        public static float HexFlatTopWorldY(Vector2Int key)
        {
            float baseY = key.y * 0.866f; // hexHeight
            if ((key.x & 1) != 0) baseY -= 0.866f / 2f;
            return baseY;
        }

        #endregion

        #region Rect

        /// <summary>
        /// clockwise from top
        /// </summary>
        public static readonly Vector2Int[] s_RectCrossAdjacents = new Vector2Int[]
        {
            new Vector2Int(0,-1),
            new Vector2Int(1,0),
            new Vector2Int(0,1),
            new Vector2Int(-1,0)
        };

        public static readonly Vector2Int[] s_RectAllAdjacents = new Vector2Int[]
       {
            new Vector2Int(0,-1),
            new Vector2Int(1,-1),
            new Vector2Int(1,0),
            new Vector2Int(1,1),
            new Vector2Int(0,1),
            new Vector2Int(-1,1),
            new Vector2Int(-1,0),
            new Vector2Int(-1,-1),

       };
        public static IEnumerable<Vector2Int> GetRectCrossNeighbors(this Vector2Int key)
        {
            foreach (var dir in s_RectCrossAdjacents) yield return key + dir;
        }
        public static IEnumerable<Vector2Int> GetRectAllNeighbors(this Vector2Int key)
        {
            foreach (var dir in s_RectAllAdjacents) yield return key + dir;
        }

        #endregion

        public static Vector3 GetGridCenterOffset(this Tilemap tilemap, int width, int height)
        {
            Grid grid = tilemap.layoutGrid;
            float tileWidth = grid.cellSize.y;
            float tileHeight = grid.cellSize.x;

            Vector3 anchorOffset = tilemap.tileAnchor;

            if (grid.cellLayout == GridLayout.CellLayout.Hexagon)
            {
                // Hex columns are offset horizontally by 0.75 * width (flat top)
                float columnSpacing = tileWidth * 0.75f;
                float rowSpacing = tileHeight;

                // Horizontal offset to center columns around 0
                float totalWidth = (width - 1) * columnSpacing;
                float offsetX = -totalWidth / 2f;

                // Vertical offset so that bottom row (y=0) is aligned
                // Bottom row tile should be placed at y = 0, but grid places (0,0) at center of tile
                float offsetY = tileHeight / 2f;

                return new Vector3(offsetX, offsetY, 0f) - anchorOffset;
            }
            else if (grid.cellLayout == GridLayout.CellLayout.Rectangle)
            {
                float offsetX = -((width - 1) / 2f) * tileWidth;

                // The Y offset aligns the bottom row (y = 0) with world origin, adjusting for cell center
                float offsetY = tileHeight / 2f;

                return new Vector3(offsetX, offsetY, 0f) - anchorOffset;
            }

            return Vector3.zero;
        }

        public static NodeValueSO GetNodeValueSOByTile(this IEnumerable<NodeValueSO> all, TileBase tileBase)
        {
            return all.FirstOrDefault(x => x.GetTile() == tileBase);
        }

        public static IEnumerable<Vector2Int> GetOccupiedKeys(this Vector2Int key, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    yield return new Vector2Int(key.x + dx, key.y + dy);
        }

        public static IEnumerable<Vector3Int> GetOccupiedCellPoses(Vector3Int cellPos, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    yield return new Vector3Int(cellPos.x - dy, cellPos.y + dx, 0);
        }

        public static Vector3 GetWorldPosWithSize(this Vector2Int originKey, Vector2Int size, int height, Tilemap tilemap)
        {
            var keys = GetOccupiedKeys(originKey, size);
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var key in keys)
            {
                sum += GetWorldPos(key, height, tilemap);
                count++;
            }

            return count > 0 ? sum / count : GetWorldPos(originKey, height, tilemap);
        }
        public static Vector3 GetWorldPosWithSize(this Vector3Int originCellPos, Vector2Int size, Tilemap tilemap)
        {
            var cells = GetOccupiedCellPoses(originCellPos, size);
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var cell in cells)
            {
                sum += GetWorldPos(cell, tilemap);
                count++;
            }

            return count > 0 ? sum / count : GetWorldPos(originCellPos, tilemap);
        }

        static Vector3 GetWorldPos(this Vector2Int key, int height, Tilemap tilemap)
        {
            Vector3Int cellPos = key.GetCellPosByKey(height);
            Vector3 worldPos = tilemap.layoutGrid.CellToWorld(cellPos);
            Vector3 anchorOffset = tilemap.tileAnchor;
            Vector3 alignedPos = worldPos + anchorOffset;

            return alignedPos;
        }
        static Vector3 GetWorldPos(this Vector3Int cellPos, Tilemap tilemap)
        {
            Vector3 worldPos = tilemap.layoutGrid.CellToWorld(cellPos);
            Vector3 anchorOffset = tilemap.tileAnchor;
            Vector3 alignedPos = worldPos + anchorOffset;

            return alignedPos;
        }

        public static Vector2Int GetKeyByWorldPosition(this Vector3 worldPos, int height, Tilemap tilemap)
        {           
            Vector3Int cellPos = tilemap.layoutGrid.WorldToCell(worldPos);
            return cellPos.GetKeyByCellPos(height);
        }

        public static Vector3Int GetCellPosByKey(this Vector2Int key, int height)
        {
            // unity grid works vice versa
            // x moves in Y direction
            // y moves in X direction
            int x = height - key.y;
            int y = key.x;

            return new Vector3Int(x, y, 0);
        }


        public static Vector2Int GetKeyByCellPos(this Vector3Int pos, int height)
        {
            int keyX = pos.y;
            int keyY = height - pos.x;

            return new Vector2Int(keyX, keyY);
        }



    }



}
