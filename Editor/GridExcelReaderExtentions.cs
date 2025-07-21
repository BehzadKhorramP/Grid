using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MadApper.GridSystem.Editor
{
    public static class GridExcelReaderExtentions
    {      
        public const string k_SizeTag = "Size:";

        public static GridData GetGridData(DataTable table, int fromRowIndex, int toRowIndex, LookUpSO lookup)
        {
            int width = 0;
            int height = 0;
            int rowsCount = table.Rows.Count;
            var columsCount = table.Columns.Count;
            int maxCol = 100;
            var colClamped = Mathf.Clamp(columsCount, 0, maxCol);

            for (int r = fromRowIndex; r < toRowIndex; r++)
            {
                for (int c = 1; c < colClamped; c++)
                {
                    var cell = table.Rows[r].TryGetStringAtColumn(c);
                    if (string.IsNullOrEmpty(cell)) continue;
                    if (c >= width) width = c;
                }
                height++;
            }

            if (width == 0 || height == 0) return null;

            var gridData = new GridData(width, height);         

            height = 0;

            for (int r = fromRowIndex; r < toRowIndex; r++)
            {
                for (int c = 1; c < colClamped; c++)
                {
                    var cell = table.Rows[r].TryGetStringAtColumn(c).ToUpper();
                    if (string.IsNullOrEmpty(cell)) continue;

                    var key = new Vector2Int(c - 1, height);
                    var nodeData = GetNodeData(cell, key, lookup);

                    if (nodeData == null) continue;
                    // (sort: false) cause it iterates in a sorted way already
                    gridData.AddNodeData(nodeData, sort: false);
                }

                height++;
            }

            gridData.EnsureNoUndefined();


            return gridData;
        }
        public static NodeData GetNodeData(string cell, Vector2Int key, LookUpSO lookup)
        {
            var values = new List<ValueData>(); 
            var splitstar = cell.Split('_');

            foreach (var str in splitstar)
            {
                MADUtility.GetExcelValueStrAndOptions(str, out string valueStr, out string options);

                var nodeValueSO = lookup.GetValue<NodeValueSO>(valueStr);

                if (nodeValueSO != null)
                {
                    Vector2Int size = nodeValueSO.GetSize();

                    var excelSize = GetValueExcelSize(options);
                    if (excelSize.HasValue) size = excelSize.Value;

                    values.Add(new ValueData(so: nodeValueSO, options: options, size: size));
                }
            }

            if (values.Count == 0) return null;

            var nodeData = new NodeData(key);
            nodeData.Values = values;

            return nodeData;
        }

        static Vector2Int? GetValueExcelSize(string options)
        {
            if (string.IsNullOrEmpty(options)) return null;

            var optionsSplit = options.Split('&');
            foreach (var opSplit in optionsSplit)
            {
                if (opSplit.IndexOf(k_SizeTag, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var s = Regex.Replace(opSplit, k_SizeTag, "", RegexOptions.IgnoreCase);
                var sizeSplit = s.Split('*');

                if (sizeSplit.Length != 2) continue;

                var isValid = 0;
                var newSize = new Vector2Int();
                if (int.TryParse(sizeSplit[0], out int x))
                {
                    isValid++;
                    newSize.x = x;
                }
                if (int.TryParse(sizeSplit[1], out int y))
                {
                    isValid++;
                    newSize.y = y;
                }

                if (isValid >= 2) return newSize;
            }

            return null;
        }


        public static GridData GetDefaultGridData(int Width, int Height, NodeValueSO randomSO)
        {
            var gridData = new GridData();

            gridData.Width = Width;
            gridData.Height = Height;

            for (int h = 0; h < Height; h++)
            {
                for (int w = 0; w < Width; w++)
                {
                    var key = new Vector2Int(w, h);
                    var valueData = new ValueData(randomSO, null);
                    var nodeData = new NodeData(key);
                    nodeData.Values.Add(valueData);
                    gridData.AddNodeData(nodeData);
                }
            }

            return gridData;
        }


    }
}
