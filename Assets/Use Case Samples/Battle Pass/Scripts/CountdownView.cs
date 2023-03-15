using System;
using TMPro;
using UnityEngine;

namespace Unity.Services.Samples.BattlePass
{
    public class CountdownView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI countdownText;

        public void SetTotalSeconds(float totalSeconds)
        {
            var minutes = Mathf.FloorToInt(totalSeconds / 60);
            var seconds = totalSeconds % 60;

            countdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
