using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public abstract class PanelViewBase : MonoBehaviour
    {
        [SerializeField]
        List<Selectable> allSelectables;

        protected bool isInteractable { get; private set; }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        public virtual void SetInteractable(bool isInteractable)
        {
            foreach (var selectable in allSelectables)
            {
                selectable.interactable = isInteractable;
            }

            this.isInteractable = isInteractable;
        }

        protected void AddSelectable(Selectable selectable)
        {
            allSelectables.Add(selectable);
        }

        protected void RemoveSelectable(Selectable selectableToRemove)
        {
            allSelectables.RemoveAll(selectable => selectable == selectableToRemove);
        }
    }
}
