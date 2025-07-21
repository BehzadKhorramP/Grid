using BEH;
using UnityEngine;

namespace MadApper.GridSystem
{
    public class PoolGridWallObject : Pool<GridWallObject> { }

    public class GridWallObject : MonoBehaviour, IPoolable
    {
        [SerializeField] string id;


        #region IPoolable
        public string i_PoolID { get => id; set => id = value; }
        public bool i_InPool { get; set; }
        public void i_OnSpawned(bool instantiated) { } 

        #endregion


        public void Despawn() => PoolGridWallObject.Despawn(this);
    }
}
