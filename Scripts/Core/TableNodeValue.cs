using Sirenix.OdinInspector;
using System.Linq;
using UnityEngine;

namespace MadApper.GridSystem
{
    public class TableNodeValue<TValue> : RefValueTable<NodeValueSO, TValue> where TValue :  class, new()
    {
        [PropertyOrder(-1)]
        [SerializeField] GridLookupTable lookupTable;


#if UNITY_EDITOR

        //[MenuItem("Context/Bubble Pong/NodeValues Table", false, 100)]
        //static void EditSettings()
        //{
        //    Selection.activeObject = MADUtility.GetOrCreateSOAtEssentialsFolder<TableNodeValue>();
        //}

        [PropertyOrder(-1)]
        [Button(ButtonSizes.Large)]
        public void Refresh()
        {
            var all = lookupTable.AllSOs;

            foreach (var item in all)
            {
                var find = items.Find(x => x.Ref == item);
                if (find == null)
                {
                    var i = new Item() { Ref = item, Value = new TValue() };
                    items.Add(i);
                }
            }

            var removes = items.Where(x => !all.Any(so => so == x.Ref)).ToList();

            foreach (var item in removes)
            {
                items.Remove(item);
            }


            this.TrySetDirty();
        }


        public GridLookupTable GetGridLookupTable() => lookupTable;
#endif

    }
}
