using TMPro;
using UnityEngine;

namespace GameOperationsSamples
{
    public class CurrencyHudView : MonoBehaviour
    {
        public string definitionId;

        public TextMeshProUGUI balanceField;

        public void UpdateBalanceField(string definitionId, long balance)
        {
            if (string.Equals(definitionId, this.definitionId))
            {
                SetBalance(balance);
            }
        }

        void SetBalance(long balance)
        {
            balanceField.text = balance.ToString();
        }
    }
}
