using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MadApper.GridSystem
{
    [CreateAssetMenu(fileName = "Grid Lookup Table", menuName = "Grid/Grid Lookup Table")]
    public class GridLookupTable : SerializedScriptableObject
    {     
        [PropertySpace(10, 10)]
        public GameObject Palette;

        [PropertySpace(10, 10)]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetAvailableProviderTypes))] 
#endif
        public Type ValueProviderType;

        [PropertySpace(10, 10)]
        [AutoGetSOInDirectory(useAssetDirectory: true)] public List<NodeValueSO> AllSOs;

#if UNITY_EDITOR
        public IEnumerable<ValueDropdownItem<Type>> GetAvailableProviderTypes()
        {
            return GridData.GetAvailableProviderTypes();
        } 
#endif
    }
}
