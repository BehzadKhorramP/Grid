using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
using MadApperEditor.Common;
using MadApper.Bridge;
#endif

namespace MadApper.GridSystem
{

    [Serializable]
    public abstract class GridValueProviderShuffler : GridValueProvider
    {
        [Serializable]
        public class ValueSOProvider : ProviderShuffled<NodeValueSO>
        {
            public ValueSOProvider() : base() { }
            public ValueSOProvider(List<NodeValueSO> allowables) : base(allowables) { }
        }
        [Serializable]
        public class StringProvider : ProviderShuffled<string>
        {
            public StringProvider() : base() { }
            public StringProvider(List<string> allowables) : base(allowables) { }
        }



        [HideIf(nameof(hideRefs))][SerializeField] protected ValueSOProvider valueSOProvider;

        [HideIf(nameof(hideRefs))][SerializeField] protected StringProvider optionProvider;


        // necessary for GridEditor instance creation
        public GridValueProviderShuffler()
        {
            valueSOProvider = new ValueSOProvider();
            optionProvider = new StringProvider();
        }

        public GridValueProviderShuffler(List<NodeValueSO> nodeValues)
        {
            valueSOProvider = new ValueSOProvider(nodeValues);
            optionProvider = new StringProvider();
        }
        public GridValueProviderShuffler(List<NodeValueSO> nodeValues, List<string> options)
        {
            valueSOProvider = new ValueSOProvider(nodeValues);
            optionProvider = new StringProvider(options);
        }


        public override void Initialize(params object[] deletes)
        {
            valueSOProvider.Initialize(deletes);
            optionProvider.Initialize(deletes);
        }
        public override ValueData Provide()
        {
            var so = valueSOProvider.Provide();
            var options = optionProvider.Provide();

            return new ValueData(so, options);
        }

        public override void EnsureValid()
        {
            var currentNodeValues = valueSOProvider.GetAllowables();
            if (currentNodeValues.Count != 0) return;

#if UNITY_EDITOR
            var allNodeValueSOs = GetAllNodeValueSOs();
            // no value found, it's a human error
            if (allNodeValueSOs == null || !allNodeValueSOs.Any()) return;
            valueSOProvider.AddAllowable(allNodeValueSOs[0]);
#endif

            InitializeEditor();
        }

        public override bool IsValid()
        {         
            return valueSOProvider.GetAllowables().Count != 0;
        }

        public ValueSOProvider GetValueSOProvider() => valueSOProvider;
        public StringProvider GetOptionStringProvider() => optionProvider;


        #region Editor

#if UNITY_EDITOR


        public abstract List<NodeValueSO> GetAllNodeValueSOs();

        [Title("Allowable Values", bold: true, horizontalLine: true, titleAlignment: TitleAlignments.Centered)]
        [HideReferenceObjectPicker][SerializeField][InlineProperty][HideLabel] protected EditorNodeValueMatrix nodeValueMatrix;

        [Title("Allowable Options", bold: true, horizontalLine: true, titleAlignment: TitleAlignments.Centered)]
        [Space(10)][HideReferenceObjectPicker][SerializeField][InlineProperty][HideLabel] protected StringMatrix optionsMatrix;

        protected override void InitializeEditorInternal()
        {
            base.InitializeEditorInternal();

            var commonAssets = EditorCommonAssets.GetSO();

            var currentNodeValues = valueSOProvider.GetAllowables();
            var nodeValueCells = currentNodeValues.Select(value => new NodeValueEditorCell(assets: commonAssets, value));
            var allNodeValueSOs = GetAllNodeValueSOs();
            var allNodeValueOptions = allNodeValueSOs.Select(value => new NodeValueEditorCell(assets: commonAssets, value));

            Action<NodeValueEditorCell> onAddedValue = (cell) =>
            {
                var value = cell.Value;
                if (value == null) return;
                valueSOProvider.AddAllowable(value);
                InitializeEditor();
            };
            Action<NodeValueEditorCell> onRemovedValue = (cell) =>
            {
                var value = cell.Value;
                if (value == null) return;
                valueSOProvider.RemoveAllowable(value);
                InitializeEditor();
            };

            nodeValueMatrix = new EditorNodeValueMatrix(assets: commonAssets, cells: nodeValueCells, allOptions: allNodeValueOptions, onAdded: onAddedValue, onRemoved: onRemovedValue);




            var currentOptions = optionProvider.GetAllowables();
            var optionCells = currentOptions.Select(value => new StringEditorCell(assets: commonAssets, value));

            Action<string> onAddedOption = (value) =>
            {
                optionProvider.AddAllowable(value);
                InitializeEditor();
            };
            Action<StringEditorCell> onRemovedOption = (cell) =>
            {
                var value = cell.Value;
                optionProvider.RemoveAllowable(value);
                InitializeEditor();
            };

            optionsMatrix = new StringMatrix(assets: commonAssets, cells: optionCells, onAdded: onAddedOption, onRemoved: onRemovedOption);
        }

#endif

        #endregion


    }


}
