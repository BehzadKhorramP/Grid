#if UNITY_EDITOR

using MadApper.Bridge;
using System.Collections.Generic;
using System;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
using MadApperEditor.Common;

namespace MadApper.GridSystem
{
    [Serializable]
    public class EditorNodeValueMatrix : EditorMatrix<NodeValueEditorCell>
    {
        [SerializeField][HideInInspector] List<NodeValueEditorCell> allOptions;
        [SerializeField][HideInInspector] Action<NodeValueEditorCell> onAdded;
        [SerializeField][HideInInspector] Action<NodeValueEditorCell> onRemoved;

        public EditorNodeValueMatrix(EditorCommonAssets assets, IEnumerable<NodeValueEditorCell> cells, IEnumerable<NodeValueEditorCell> allOptions,
           Action<NodeValueEditorCell> onAdded, Action<NodeValueEditorCell> onRemoved, float width = 64, float height = 64, bool drawAddButton = true)
            : base(assets, cells, width, height, drawAddButton)
        {
            this.onAdded = onAdded;
            this.onRemoved = onRemoved;
            this.allOptions = allOptions.ToList();
        }

        public override void OnDrawOptions(NodeValueEditorCell cell, float width, float heigth)
        {
            commonAssets.DrawRemoveButton(width, height: 24, () => onRemoved?.Invoke(cell));
        }
        public override void OnAddPressed()
        {
            NodeValueSelectionPopup.ShowWindow<NodeValueSelectionPopup>(allOptions, onAdded);
        }
    }

    public class NodeValueEditorCell : EditorCellBase
    {
        [HideInInspector] public NodeValueSO Value;

        public NodeValueEditorCell(EditorCommonAssets assets, NodeValueSO value) : base(assets) { this.Value = value; }
        public override string Name => Value.name;
        public override Object Icon => Value.GetTile();
    }


    public class NodeValueSelectionPopup : EditorSelectionPopup<NodeValueEditorCell> { }

}

#endif