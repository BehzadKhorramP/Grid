using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MadApper.GridSystem
{
    [CreateAssetMenu(fileName = "NodeValueSO", menuName = "Grid/NodeValueSO")]
    public class NodeValueSO : UniqueScriptable
    {
        [Title("Node Value")]

        [SerializeField] NodeValue value;
        [SerializeField, PreviewField] TileBase tile;
        [SerializeField] Color color;
        [SerializeField] Vector2Int size = Vector2Int.one;

        public Color GetColor() => color;
        public TileBase GetTile() => tile;
        public NodeValue GetPrefab() => value;
        public Vector2Int GetSize() => size;
        public NodeValue CreateNodeValue(Transform parent)
        {
#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                var instance = PrefabUtility.InstantiatePrefab(GetPrefab()).GetComponent<NodeValue>();
                instance.transform.SetParent(parent);
                instance.i_OnSpawned(true);
                return instance;
            }
#endif         

            return PoolNodeValue.Get(GetPrefab().GetPoolID(), GetPrefab(), parent);
        }


        public void SetNodeValuePrefab(NodeValue value)
        {
            this.value = value;
            this.TrySetDirty();
        }


    }

}
