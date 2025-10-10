using System;
using UnityEngine;
namespace Match3
{
    public class Readme : ScriptableObject
    {
        public Texture2D icon;
        public string title;
        public Section[] sections;
        public bool loadedLayout;

        [Serializable]
        public class Section
        {
            public Texture2D bannerImage;
            public string heading, text, linkText, url;

        }
    }
}
