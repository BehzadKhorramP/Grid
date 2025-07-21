using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MadApper.GridSystem
{
    public class NodeValueCollider : MonoBehaviour
    {
        [SerializeField][AutoGetOrAdd] Collider _collider;
        [SerializeField] NodeValue _nodeValue;

        int? interactableLayer;


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (_nodeValue != null) return;

            _nodeValue = GetComponentInParent<NodeValue>(true);

            this.TrySetDirty();
        }
#endif

        public NodeValue GetNodeValue() => _nodeValue;
        public Collider GetCollider() => _collider;
        public void SetInteractibilityViaEnabled(bool interactable) => _collider.enabled = interactable;
        public void SetInteractibilityViaLayer(bool interactable)
        {
            if (!interactableLayer.HasValue) interactableLayer = _collider.gameObject.layer;
            var newLayer = interactable ? interactableLayer.Value : 0;
            _collider.gameObject.layer = newLayer;
        }

        public void z_SetInteractibilityViaEnabled(bool interactable) => SetInteractibilityViaEnabled(interactable);


    }
}
