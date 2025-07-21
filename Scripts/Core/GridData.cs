using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MadApper.GridSystem
{
    [Serializable]
    public class GridData
    {
        public int Width;
        public int Height;

        public List<NodeData> NodesData;

        [SerializeReference][InlineProperty, HideLabel] public GridValueProvider ValueProvider;

        [NonSerialized] GridDataInitializationQueue initQueue;

        public GridData()
        {
            NodesData = new List<NodeData>();
            Width = 5;
            Height = 10;
        }
        public GridData(int width, int height)
        {
            NodesData = new List<NodeData>();
            Width = width;
            Height = height;
        }
        public class GridDataInitializationQueue : QueuedActionsAsync<GridData, GridDataInitializationQueue>
        {
            public GridDataInitializationQueue(GridData sender) : base(sender) { }
            public override GridDataInitializationQueue GetSelf() => this;
        }


        public void InitializeGridData()
        {
            Stop();

            initQueue = new GridDataInitializationQueue(this);
            initQueue.CollectActions();
            initQueue.Execute();
        }


        public void Stop()
        {
            if (initQueue != null) initQueue.Stop();
            initQueue = null;
        }



        public void AddNodeData(NodeData data, bool sort = true)
        {
            NodesData.Add(data);
            if (sort) Sort();
        }
        public void RemoveNodeData(NodeData data, bool sort = true)
        {
            NodesData.Remove(data);
            if (!NodesData.Any()) return;
            if (sort) Sort();
        }

        void Sort()
        {
            NodesData.Sort((a, b) =>
            {
                int cmp = a.Key.x.CompareTo(b.Key.x);
                return cmp != 0 ? cmp : a.Key.y.CompareTo(b.Key.y);
            });
        }

        public NodeData GetNodeData(Vector2Int key) => NodesData.Find(x => x.Key == key);

       
        public void EnsureNoUndefined()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var key = new Vector2Int(x, y);

                    if (GetNodeData(key) == null)
                    {
                        var nodeData = new NodeData(key);
                        AddNodeData(nodeData);
                    }
                }
            }

            Sort();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var key = new Vector2Int(x, y);
                    var nodeData = GetNodeData(key);
                    nodeData.SetIsGap(GetIsGap(nodeData));
                }
            }
        }
        bool GetIsGap(NodeData nodeData)
        {
            if (nodeData.Values.Count > 0)
                return false;

            Vector2Int targetKey = nodeData.Key;

            foreach (var other in NodesData)
            {
                if (other == nodeData || other.Values.Count == 0) continue;

                foreach (var value in other.Values)
                {
                    var size = value.Size;
                    if (size == Vector2Int.one) continue;

                    var occupiedKeys = other.Key.GetOccupiedKeys(size);
                    if (occupiedKeys.Contains(targetKey))
                        return false; // This key is covered by a larger-sized value
                }
            }

            return true; // No value overlaps this empty tile
        }


        public bool IsWithinBounds(Vector2Int key)
        {
            return key.x >= 0 && key.x < Width && key.y >= 0 && key.y < Height;
        }

#if UNITY_EDITOR
        public static IEnumerable<ValueDropdownItem<Type>> GetAvailableProviderTypes()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && typeof(GridValueProvider).IsAssignableFrom(t))
                .Select(t => new ValueDropdownItem<Type>(t.Name, t));
        }
#endif
    }

    [Serializable]
    public class NodeData
    {
        public Vector2Int Key;
        public List<ValueData> Values;
        public bool IsGap;
        public NodeData(Vector2Int key, bool isGap = false)
        {
            Key = key;
            Values = new List<ValueData>();
            IsGap = isGap;
        }

        public void SetIsGap(bool isGap) => IsGap = isGap;
    }

    [Serializable]
    public class ValueData
    {
        public NodeValueSO SO;
        public Vector2Int Size;
        public string Options;

        public ValueData(NodeValueSO so, string options)
        {
            SO = so;
            Size = so.GetSize();
            Options = options;
        }
        public ValueData(NodeValueSO so, string options, Vector2Int size)
        {
            SO = so;
            Size = size;
            Options = options;
        }

        public NodeValue Create(Transform parent)
        {
            var instance = SO.CreateNodeValue(parent);
            instance.OnCreated(this);
            return instance;
        }

        public override string ToString()
        {
            return $"{SO.GetPrefab()}-{Options}";
        }

    }



    //public struct GridCenter
    //{
    //    public enum Type { Center, CenterBottom }
    //    public int X { get; private set; }
    //    public int Y { get; private set; }

    //    public GridCenter(int width, int height, Type type)
    //    {
    //        switch (type)
    //        {
    //            case Type.Center:
    //                X = width / 2;
    //                Y = height / 2;
    //                break;
    //            case Type.CenterBottom:
    //                X = width / 2 ; 
    //                Y = height;
    //                break;
    //            default:
    //                X = width / 2;
    //                Y = height / 2;
    //                break;
    //        }
    //    }

    //}

}
