using System;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;


namespace MadApper.GridSystem.Editor
{
    [CustomGridBrush(hideAssetInstances: false, hideDefaultInstance: false, defaultBrush: true, defaultName: "MAD Brush")]
    public class WatchingBrush : GridBrush
    {
        public static Action<Args> s_OnPainted;
        public static Action<Args> s_OnErased;
        public static Action<Args> s_OnSelected;
        public static Action s_OnBrushFinished;

        public override void BoxFill(GridLayout gridLayout, GameObject brushTarget, BoundsInt bounds)
        {
            base.BoxFill(gridLayout, brushTarget, bounds);

            if (brushTarget == null) return;
            Tilemap tilemap = brushTarget.GetComponent<Tilemap>();
            if (tilemap == null) return;

            //var isErasing = Event.current != null && Event.current.button == 1;

            //this.Log(isErasing);

            foreach (var pos in bounds.allPositionsWithin)
            {
                TileBase paintedTile = tilemap.GetTile(pos);

                var args = new Args() { Grid = gridLayout, Tile = paintedTile, CellPos = pos };

                //if (paintedTile == null || isErasing)
                //{
                //    s_OnErased?.Invoke(args);
                //}
                //else
                //{
                //    s_OnPainted?.Invoke(args);
                //}

                if (paintedTile != null)
                {
                    s_OnPainted?.Invoke(args);
                }
                else
                {
                    s_OnErased?.Invoke(args);
                }
            }

            s_OnBrushFinished?.Invoke();
        }


        public override void Erase(GridLayout grid, GameObject brushTarget, Vector3Int position)
        {
            base.Erase(grid, brushTarget, position);

            var args = new Args() { Grid = grid, Tile = null, CellPos = position };

            s_OnErased?.Invoke(args);

            s_OnBrushFinished?.Invoke();
        }

        public override void Select(GridLayout gridLayout, GameObject brushTarget, BoundsInt bounds)
        {
            base.Select(gridLayout, brushTarget, bounds);

            if (brushTarget == null) return;
            Tilemap tilemap = brushTarget.GetComponent<Tilemap>();
            if (tilemap == null) return;

            foreach (var pos in bounds.allPositionsWithin)
            {
                TileBase paintedTile = tilemap.GetTile(pos);
                var args = new Args() { Grid = gridLayout, Tile = paintedTile, CellPos = pos };
                s_OnSelected?.Invoke(args);

                if (paintedTile != null)
                    return;
            }
        }

        public struct Args
        {
            public GridLayout Grid;
            public TileBase Tile;
            public Vector3Int CellPos;
        }
    }


#if UNITY_EDITOR

    [CustomEditor(typeof(WatchingBrush))]
    public class WatchingBrushEditor : GridBrushEditor
    {
        public override void OnPaintInspectorGUI()
        {
            base.OnPaintInspectorGUI();
            EditorGUILayout.HelpBox("Left-click to paint. Right-click to erase.", MessageType.Info);
        }
    }
#endif
}
