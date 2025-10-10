using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Match3
{
    public class MainMenu : MonoBehaviour
    {
        public LevelList LevelList;
        public VisualTreeAsset LevelEntry;

        private UIDocument m_Document;
        private VisualElement m_Cover;

        private int m_TargetLevel = -1;

        private void Start()
        {
            GameManager.Instance.MainMenuOpened();
            
            m_Document = GetComponent<UIDocument>();
            UIHandler.ApplySafeArea(m_Document.rootVisualElement);
            
            var container = m_Document.rootVisualElement.Q<VisualElement>("LevelSelectionContainer");
        
            for(var i = 0; i < LevelList.SceneCount; ++i)
            {
#if UNITY_EDITOR
                //in editor we check if the level is not null. This shouldn't happen in a build as the build script will
                //check
                if (LevelList.Scenes[i] == null)
                {
                    Debug.LogWarning("LevelList contains a null scene! Fix or remove the scene from the LevelList");
                    continue;
                }
#endif
            
                var newEntry = LevelEntry.Instantiate();
                var label = newEntry.Q<Label>("LevelNumber");
                label.text = (i+1).ToString();
            
                container.Add(newEntry);

                //the container is stretched, which would lead to be able to click UNDER the entry, so we grab the actual
                //level entry inside the container 
                var subEntry = newEntry.Q<VisualElement>("LevelEntry");
                var i1 = i;
                subEntry.AddManipulator(new Clickable(() =>
                {
                    m_TargetLevel = i1;
                    FadeOut();
                }));
            }
            
            m_Cover = m_Document.rootVisualElement.Q<VisualElement>("Cover");
            m_Cover.style.opacity = 1.0f;
            m_Cover.RegisterCallback<TransitionEndEvent>(evt =>
            {
                //we're fading out
                if (m_Cover.style.opacity.value > 0.9f && m_TargetLevel != -1)
                {
                    LevelList.LoadLevel(m_TargetLevel);
                }
            });
            
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            yield return null;
            yield return null;

            m_Cover.style.opacity = 0.0f;
        }

        void FadeOut()
        {
            m_Cover.style.opacity = 1.0f;
        }
    }
}