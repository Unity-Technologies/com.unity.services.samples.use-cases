using Unity.Services.Economy.Model;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    public class InventoryHudView : MonoBehaviour
    {
        public GameObject inventoryItemPrefab;
        public Transform itemListParentTransform;


        public void Refresh(GetInventoryResult inventory)
        {
            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (inventoryItemPrefab == null || itemListParentTransform == null) return;

            RemoveAll();

            if (inventory?.PlayersInventoryItems is null) return;

            foreach (var item in inventory.PlayersInventoryItems)
            {
                var newInventoryItemGameObject = Instantiate(inventoryItemPrefab, itemListParentTransform);
                var inventoryItemView = newInventoryItemGameObject.GetComponent<InventoryItemView>();
                inventoryItemView.SetKey(item.InventoryItemId);
            }

            Debug.Log("Inventory items retrieved and updated. Total inventory item count: " +
                $"{inventory.PlayersInventoryItems.Count}");
        }

        void RemoveAll()
        {
            while (itemListParentTransform.childCount > 0)
            {
                DestroyImmediate(itemListParentTransform.GetChild(0).gameObject);
            }
        }
    }
}
