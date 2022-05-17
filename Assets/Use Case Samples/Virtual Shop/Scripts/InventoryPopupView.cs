using System.Threading.Tasks;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace VirtualShop
    {
        public class InventoryPopupView : MonoBehaviour
        {
            public async Task Show()
            {
                gameObject.SetActive(true);

                await EconomyManager.instance.RefreshInventory();
            }

            public void OnCloseButtonPressed()
            {
                Close();
            }

            void Close()
            {
                gameObject.SetActive(false);
            }
        }
    }
}
