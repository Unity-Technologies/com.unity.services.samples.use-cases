// NOTE:
// You can disable all editor analytics in Unity > Preferences.
// The EditorAnalytics API is for Unity to collect usage data for this samples project in
// order to improve our products. The EditorAnalytics API won't be useful in your own project
// because it only works in the Editor and the data is only sent to Unity. To see how you
// could implement analytics in your own project, have a look at
// the AB Test Level Difficulty sample or the Seasonal Events sample.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples
{
    [RequireComponent(typeof(Button))]
    public class ButtonEditorAnalytics : MonoBehaviour
    {
#if UNITY_EDITOR
        public string buttonName;

        void Awake()
        {
            var button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonPressed);
        }

        void OnButtonPressed()
        {
            SamplesEditorAnalytics.SendButtonPressedInPlayModeEvent(buttonName);
        }
#endif
    }
}
