#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.Samples.Editor
{
    [CustomEditor(typeof(Readme))]
    [InitializeOnLoad]
    public class ReadmeEditor : UnityEditor.Editor
    {
        Readme m_Readme;
        const float k_SectionSpacer = 16f;
        const float k_HeaderSpacer = 8f;
        const float k_BulletItemSpacer = 2f;
        const float k_BulletLevel1LabelWidth = 25;
        const float k_BulletLevel2LabelWidth = 40;
        const float k_BulletLevel3LabelWidth = 55;
        const float k_BulletLevel4LabelWidth = 70;
        bool m_StylesInitialized;

        const string k_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";
        const string k_DefaultReadmeLabel = "StartHereReadme";

        [SerializeField]
        GUIStyle bodyStyle;

        [SerializeField]
        GUIStyle headerTitleStyle;

        [SerializeField]
        GUIStyle subHeader1Style;

        [SerializeField]
        GUIStyle subHeader2Style;

        [SerializeField]
        GUIStyle subHeader3Style;

        [SerializeField]
        GUIStyle boldStyle;

        [SerializeField]
        GUIStyle italicsStyle;

        [SerializeField]
        GUIStyle linkStyle;

        void OnEnable()
        {
            m_Readme = target as Readme;
        }

        static ReadmeEditor()
        {
            EditorApplication.delayCall += SelectReadmeAutomatically;
        }

        static void SelectReadmeAutomatically()
        {
            if (!SessionState.GetBool(k_ShowedReadmeSessionStateName, false))
            {
                SelectReadme();
                SessionState.SetBool(k_ShowedReadmeSessionStateName, true);
            }
        }

        static void SelectReadme()
        {
            var ids = AssetDatabase.FindAssets($"l:{k_DefaultReadmeLabel}");

            if (ids.Length > 0)
            {
                var readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));
                Selection.objects = new[] { readmeObject };
            }
            else
            {
                Debug.Log($"Couldn't find a default README asset with the \"{k_DefaultReadmeLabel}\" label.");
            }
        }

        void InitStyles()
        {
            if (m_StylesInitialized)
            {
                return;
            }

            bodyStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 14
            };

            headerTitleStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 26
            };

            subHeader1Style = new GUIStyle(bodyStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };

            subHeader2Style = new GUIStyle(bodyStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };

            subHeader3Style = new GUIStyle(bodyStyle)
            {
                fontSize = 16,
                fontStyle = FontStyle.Italic
            };

            boldStyle = new GUIStyle(bodyStyle)
            {
                fontStyle = FontStyle.Bold
            };

            italicsStyle = new GUIStyle(bodyStyle)
            {
                fontStyle = FontStyle.Italic
            };

            linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                fontSize = 14
            };

            m_StylesInitialized = true;
        }

        protected override void OnHeaderGUI()
        {
            InitStyles();

            Texture2D icon = null;
            var title = "";
            var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

            if (m_Readme != null && m_Readme.header != null)
            {
                icon = m_Readme.header.icon;
                title = m_Readme.header.title;
            }

            using (new EditorGUILayout.HorizontalScope("In BigTitle"))
            {
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
                }

                GUILayout.Label(title, headerTitleStyle);
            }
        }

        public override void OnInspectorGUI()
        {
            InitStyles();

            if (m_Readme == null || m_Readme.sections == null)
            {
                return;
            }

            foreach (var section in m_Readme.sections)
            {
                if (!string.IsNullOrEmpty(section.subHeader1))
                {
                    DisplayFormattedHeaderText(section.subHeader1, subHeader1Style);
                }

                if (!string.IsNullOrEmpty(section.subHeader2))
                {
                    DisplayFormattedHeaderText(section.subHeader2, subHeader2Style);
                }

                if (!string.IsNullOrEmpty(section.subHeader3))
                {
                    DisplayFormattedHeaderText(section.subHeader3, subHeader3Style);
                }

                if (!string.IsNullOrEmpty(section.body))
                {
                    DisplayFormattedBodyText(section.body, section.bodyFormat);
                }

                if (!string.IsNullOrEmpty(section.boxCallout))
                {
                    DisplayFormattedBoxCallout(section.boxCallout);
                }

                if (section.bulletList != null && section.bulletList.Length > 0)
                {
                    DisplayFormattedBulletItemLevel1List(section.bulletList);
                }

                if (section.linkList != null && section.linkList.Length > 0)
                {
                    DisplayFormattedLinkList(section.linkList);
                }

                GUILayout.Space(k_SectionSpacer);
            }
        }

        static void DisplayFormattedHeaderText(string headerText, GUIStyle style)
        {
            GUILayout.Label(headerText, style);
            GUILayout.Space(k_HeaderSpacer);
        }

        void DisplayFormattedBodyText(string text, Readme.FontFormat format)
        {
            GUILayout.Label(text, GetStyle(format));
        }

        static void DisplayFormattedBoxCallout(string text)
        {
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }

        void DisplayFormattedBulletItemLevel1List(Readme.BulletItemLevel1[] list)
        {
            var previousLabelWidth = StartListFormatting(k_BulletLevel1LabelWidth);

            foreach (var item in list)
            {
                DisplayFormattedBulletListItem(item.body, item.bodyFormat);

                if (item.bulletList != null && item.bulletList.Length > 0)
                {
                    DisplayFormattedBulletItemLevel2List(item.bulletList);
                }

                if (item.linkList != null && item.linkList.Length > 0)
                {
                    DisplayFormattedLinkList(item.linkList);
                }
            }

            EndListFormatting(previousLabelWidth);
        }

        void DisplayFormattedBulletItemLevel2List(Readme.BulletItemLevel2[] list)
        {
            var previousLabelWidth = StartListFormatting(k_BulletLevel2LabelWidth);

            foreach (var item in list)
            {
                DisplayFormattedBulletListItem(item.body, item.bodyFormat);

                if (item.bulletList != null && item.bulletList.Length > 0)
                {
                    DisplayFormattedBulletItemLevel3List(item.bulletList);
                }
            }

            EndListFormatting(previousLabelWidth);
        }

        void DisplayFormattedBulletItemLevel3List(Readme.BulletItemLevel3[] list)
        {
            var previousLabelWidth = StartListFormatting(k_BulletLevel3LabelWidth);

            foreach (var item in list)
            {
                DisplayFormattedBulletListItem(item.body, item.bodyFormat);

                if (item.bulletList != null && item.bulletList.Length > 0)
                {
                    DisplayFormattedBulletItemLevel4List(item.bulletList);
                }
            }

            EndListFormatting(previousLabelWidth);
        }

        void DisplayFormattedBulletItemLevel4List(string[] list)
        {
            var previousLabelWidth = StartListFormatting(k_BulletLevel4LabelWidth);

            foreach (var item in list)
            {
                DisplayFormattedBulletListItem(item, Readme.FontFormat.Regular);
            }

            EndListFormatting(previousLabelWidth);
        }

        void DisplayFormattedBulletListItem(string text, Readme.FontFormat format)
        {
            EditorGUILayout.LabelField("*", text, GetStyle(format));

            GUILayout.Space(k_BulletItemSpacer);
        }

        void DisplayFormattedLinkList(Readme.LinkListItem[] list)
        {
            var previousLabelWidth = StartListFormatting(k_BulletLevel1LabelWidth);

            foreach (var item in list)
            {
                DisplayFormattedLinkListItem(item.linkText, item.url);
            }

            EndListFormatting(previousLabelWidth);
        }

        void DisplayFormattedLinkListItem(string text, string url)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("*");

                if (GUILayout.Button(text, linkStyle))
                {
                    Application.OpenURL(url);
                }
            }

            GUILayout.Space(k_BulletItemSpacer);
        }

        static float StartListFormatting(float labelWidth)
        {
            EditorGUI.indentLevel++;
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            return previousLabelWidth;
        }

        static void EndListFormatting(float previousLabelWidth)
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUI.indentLevel--;
        }

        GUIStyle GetStyle(Readme.FontFormat format)
        {
            return format switch
            {
                Readme.FontFormat.Bold => boldStyle,
                Readme.FontFormat.Italic => italicsStyle,
                _ => bodyStyle
            };
        }
    }
}

#endif
