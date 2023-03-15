using System;
using System.Text;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace Unity.Services.Samples
{
    public class CurrencyHudView : MonoBehaviour
    {
        CurrencyItemView[] m_CurrencyItemViews;

        void Awake()
        {
            m_CurrencyItemViews = GetComponentsInChildren<CurrencyItemView>();
        }

        public void SetBalances(GetBalancesResult getBalancesResult)
        {
            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (this == null) return;

            if (getBalancesResult is null) return;

            var currenciesString = new StringBuilder();

            foreach (var balance in getBalancesResult.Balances)
            {
                if (balance.Balance > 0)
                {
                    currenciesString.Append($", {balance.CurrencyId}:{balance.Balance}");
                }

                foreach (var currencyItemView in m_CurrencyItemViews)
                {
                    if (string.Equals(balance.CurrencyId, currencyItemView.definitionId))
                    {
                        currencyItemView.SetBalance(balance.Balance);
                    }
                }
            }

            if (currenciesString.Length > 0)
            {
                Debug.Log($"Currency balances updated. Value(s): {currenciesString.Remove(0, 2)}");
            }
            else
            {
                Debug.Log("Currency balances updated -- none found.");
            }
        }

        public void SetBalance(string currencyId, long balance)
        {
            foreach (var currencyItemView in m_CurrencyItemViews)
            {
                if (string.Equals(currencyId, currencyItemView.definitionId))
                {
                    currencyItemView.SetBalance(balance);
                }
            }
        }

        public void ClearBalances()
        {
            foreach (var currencyItemView in m_CurrencyItemViews)
            {
                currencyItemView.SetBalance(0);
            }
        }

        public CurrencyItemView GetCurrencyItemView(string currencyDefinitionId)
        {
            foreach (var view in m_CurrencyItemViews)
            {
                if (string.Equals(view.definitionId, currencyDefinitionId))
                {
                    return view;
                }
            }

            return default;
        }
    }
}
