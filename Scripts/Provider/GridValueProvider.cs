using System;
using UnityEngine;

namespace MadApper.GridSystem
{
    [Serializable]
    public abstract class GridValueProvider
    {

        [NonSerialized][HideInInspector] protected bool hideRefs;


        // necessary for GridEditor instance creation
        public GridValueProvider() { }
        public abstract void Initialize(params object[] deletes);       
        public abstract void EnsureValid();
        public abstract bool IsValid();
        public abstract ValueData Provide();

        public void InitializeEditor()
        {
            hideRefs = true;
            InitializeEditorInternal();
        }

        protected virtual void InitializeEditorInternal() { }

    }
}
