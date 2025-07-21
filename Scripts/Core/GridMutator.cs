using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using static MadApper.GridSystem.GridData;

namespace MadApper.GridSystem
{
    public abstract class GridMutator : MonoBehaviour
    {
        const string k_Tag = "Grid Mutator";
        public const string k_ForcedOptionTag = "Forced:";
     

        [SerializeField] protected NodeValueSO randomSO;

        protected GridData gridData;

        protected virtual void OnEnable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions += InitializeGridMutator;

            IMutableNodeValue.s_TryMutating += TryMutating;
            IMutableNodeValue.s_TryAlteringValueData += TryAlteringValueData;
        }
        protected virtual void OnDisable()
        {
            GridDataInitializationQueue.s_OnAsyncQueuedActions -= InitializeGridMutator;

            IMutableNodeValue.s_TryMutating -= TryMutating;
            IMutableNodeValue.s_TryAlteringValueData -= TryAlteringValueData;

            gridData = null;
        }

        private void InitializeGridMutator(GridDataInitializationQueue actions)
        {
            var cToken = actions.GetCToken();
            var gridData = actions.GetSender();
            var action = new ActionAsync.Builder(cToken)
                .SetTask((cToken) => InitializeMutator(gridData, cToken))
                .Priority(0)
                .Tag(k_Tag)
                .Build();

            actions.Append(action);
        }

        protected virtual async UniTask InitializeMutator(GridData gridData, CancellationToken cToken)
        {
            this.gridData = gridData;

            gridData.ValueProvider.Initialize(randomSO, IMutableNodeValue.k_RandomTag);

            await UniTask.Delay(1, cancellationToken: cToken);
        }

        protected abstract NodeValue TryMutating(IMutableNodeValue arg);


        // mainly for replacing random values and options with proper values
        protected virtual ValueData TryAlteringValueData(ValueData valueData)
        {
            if (gridData == null) return valueData;

            NodeValueSO newValueSO = valueData.SO;
            string newOptions = valueData.Options;
            NodeValue prefab = valueData.SO.GetPrefab();

            bool isNew = false;

            if (randomSO != null)
            {
                if (valueData.SO == randomSO)
                {
                    isNew = true;
                    newValueSO = gridData.ValueProvider.Provide().SO;
                }
            }
            if (prefab is IMutableNodeValue mutable && valueData.Options == IMutableNodeValue.k_RandomTag)
            {
                isNew = true;
                newOptions = gridData.ValueProvider.Provide().Options;
            }


            if (isNew) return new ValueData(newValueSO, newOptions) ;
            else return valueData;
        }
    }

}
