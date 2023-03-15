using System;
using UnityEngine;

namespace Unity.Services.Samples.Editor
{
    /// <remarks>
    ///     Custom readme class based on the readme done in Boss Room sample. For more context, see:
    ///     https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/tree/main/Assets/BossRoom/Scripts/Editor/Readme
    /// </remarks>
    [CreateAssetMenu]
    public class Readme : ScriptableObject
    {
        public Header header;
        public Section[] sections;

        [Serializable]
        public class Header
        {
            public string title;
            public Texture2D icon;
        }

        [Serializable]
        public class Section
        {
            public string subHeader1;
            public string subHeader2;
            public string subHeader3;
            public string body;
            public FontFormat bodyFormat;
            public string boxCallout;
            public BulletItemLevel1[] bulletList;
            public LinkListItem[] linkList;
        }

        [Serializable]
        public class BulletItemLevel1
        {
            public string body;
            public FontFormat bodyFormat;
            public BulletItemLevel2[] bulletList;
            public LinkListItem[] linkList;
        }

        [Serializable]
        public class BulletItemLevel2
        {
            public string body;
            public FontFormat bodyFormat;
            public BulletItemLevel3[] bulletList;
        }

        [Serializable]
        public class BulletItemLevel3
        {
            public string body;
            public FontFormat bodyFormat;
            public string[] bulletList;
        }

        [Serializable]
        public class LinkListItem
        {
            public string linkText;
            public string url;
        }

        [Serializable]
        public enum FontFormat
        {
            Regular,
            Bold,
            Italic,
        }
    }
}
