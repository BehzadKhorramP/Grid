using Sirenix.OdinInspector;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;


namespace MadApper.GridSystem
{
    public interface INodeValue
    {
        NodeValue i_NodeValue { get; }
    }

    public class PoolNodeValue : BEH.Pool<NodeValue> { }

    /// <summary>
    /// Order:
    /// <code>OnSpawned</code>  
    /// <code>OnCreated</code> 
    /// </summary>

    public abstract class NodeValue : MonoBehaviour, INodeValue, BEH.IPoolable
    {        
        #region Events

        [FoldoutGroup("Events")] public UnityEventDelayList<NodeValue> onSpawned;
        [FoldoutGroup("Events")] public UnityEventDelayList<ValueData> onCreated;

        #endregion


        #region Refs
        [FoldoutGroup("Refs")][ShowInInspector][ReadOnly] public ValueData ValueData { get; protected set; }
        [FoldoutGroup("Refs")][ShowInInspector][ReadOnly] public BoardEntity BoardEntity { get; protected set; }

        [FoldoutGroup("Refs")][ReadOnly] public INodeValueShared[] Extras;
        [FoldoutGroup("Refs")][AutoGetInChildren(mandatory:false)][ReadOnly] public NodeValueCollider Collider;
        [FoldoutGroup("Refs")] public Transform LocalTransform;
        bool extrasSorted = false;
        #endregion


        #region Props     

        #endregion




        #region Validate

#if UNITY_EDITOR
        [Button]
        protected virtual void OnValidate()
        {
            if (Application.isPlaying) return;

            var setDirty = false;

            var extras = transform.GetComponentsInChildren<INodeValueShared>();
            bool areDifferent = !extras.AreEqual(Extras);
            if (areDifferent)
            {
                Extras = extras;
                setDirty = true;
            }

            if (setDirty) this.TrySetDirty();
        }
#endif
        #endregion


        #region Abstracts
        public abstract string GetPoolID();

        #endregion


        #region INodeValue
        public NodeValue i_NodeValue => this;

        #endregion


        #region IPoolable
        public bool i_InPool { get; set; }
        public string i_PoolID { get; set; }

        public virtual void i_OnSpawned(bool instantiated)
        {
            if (extrasSorted == false && Extras != null && Extras.Length != 0)
            {
                extrasSorted = true;
                Extras = Extras.OrderBy(x => x.GetPriority()).ToArray();
            }
            UnlinkBoardEntity();

            if (Extras != null && Extras.Length != 0)
                foreach (var item in Extras)
                    item.OnSpawned();

            onSpawned?.Invoke(this);
        }
        public virtual void Despawn()
        {
            Stop();

            if (Extras != null && Extras.Length != 0)
                foreach (var item in Extras)
                    item.OnDespawned();

            if (BoardEntity != null)
                BoardEntity.OnDespawned();

            PoolNodeValue.Despawn(this);
        }
        #endregion



        #region Virtuals

        public virtual bool CanFall => true;
        protected virtual void Stop() { }
        public virtual void OnCreated(ValueData valueData)
        {
            ValueData = valueData;

            if (Extras != null && Extras.Length != 0)
                foreach (var item in Extras)
                    item.OnCreated(valueData);

            Collider?.SetInteractibilityViaEnabled(true);

            onCreated?.Invoke(valueData);
        }
        public virtual void Place(Vector3 pos, Transform parent)
        {
            transform.ResetTransformToParent(parent);
            transform.localPosition = pos;
        }
        public virtual void LinkBoardEntity(BoardEntity entity) => BoardEntity = entity;
        public virtual void UnlinkBoardEntity() => BoardEntity = null;
        public virtual void OnKeySet(Vector2Int key)
        {
            if (Extras != null && Extras.Length != 0)
                foreach (var item in Extras)
                    item.OnKeySet(key);
        }
        public virtual void OnFallingStepStarted() { }
        public virtual void OnFallingStepFinished() { }
        public virtual void OnFallingAllStarted() { }
        public virtual void OnFallingAllFinished() { }


        #endregion



    }






    public abstract class INodeValueShared : MonoBehaviour
    {
        [SerializeField] int priority = 10;
        [SerializeField] bool IsActive = true;

        public int GetPriority() => priority;

        public void OnSpawned()
        {
            if (!IsActive) return;
            OnSpawnedInternal();
        }
        protected virtual void OnSpawnedInternal() { }
        public void OnDespawned()
        {
            if (!IsActive) return;
            OnDespawnedInternal();
        }
        protected virtual void OnDespawnedInternal() { }


        public void OnCreated(ValueData valueData)
        {
            if (!IsActive) return;
            OnCreatedInternal(valueData);
        }
        protected virtual void OnCreatedInternal(ValueData valueData) { }


        public void OnKeySet(Vector2Int key)
        {
            if (!IsActive) return;
            OnKeySetInternal(key);
        }
        protected virtual void OnKeySetInternal(Vector2Int key) { }
    }

}
