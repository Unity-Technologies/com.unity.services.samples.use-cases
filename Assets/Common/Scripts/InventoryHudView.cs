using System.Collections.Generic;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace GameOperationsSamples
{
    public class InventoryHudView : MonoBehaviour
    {
        public GameObject inventoryItemPrefab;
        public Transform itemListParentTransform;

        void RemoveAll()
        {
            while (itemListParentTransform.childCount > 0)
            {
                DestroyImmediate(itemListParentTransform.GetChild(0).gameObject);
            }
        }

        public void Refresh(List<PlayersInventoryItem> playersInventoryItems)
        {
            RemoveAll();

            foreach (var playersInventoryItem in playersInventoryItems)
            {
                var newInventoryItemGameObject = Instantiate(inventoryItemPrefab, itemListParentTransform);
                var inventoryItemView = newInventoryItemGameObject.GetComponent<InventoryItemView>();
                inventoryItemView.SetKey(playersInventoryItem.InventoryItemId);
            }
        }
    }
}
