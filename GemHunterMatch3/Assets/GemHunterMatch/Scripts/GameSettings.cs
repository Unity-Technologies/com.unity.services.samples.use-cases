using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.VFX;

namespace Match3
{
    /// <summary>
    /// Contains all the settings for the game so they can be found in a single place. This is stored on the GameManager
    /// in the Resource folder if you need to edit it.
    /// </summary>
    [System.Serializable]
    public class GameSettings
    {
        public float InactivityBeforeHint = 2.0f;
    
        public VisualSetting VisualSettings;
        public BonusSetting BonusSettings;
        public ShopSetting ShopSettings;
        public SoundSetting SoundSettings;
    }

    /// <summary>
    /// Visual Settings are all the parameters used in the Visual effect of the game like fall speed, bounce curve and vfx
    /// </summary>
    [System.Serializable]
    public class VisualSetting
    {
        public float FallSpeed = 10.0f;
        public AnimationCurve FallAccelerationCurve;
        public AnimationCurve BounceCurve;
        public AnimationCurve SquishCurve;

        public AnimationCurve MatchFlyCurve;
        public AnimationCurve CoinFlyCurve;

        public GameObject BonusModePrefab;
        
        public GameObject HintPrefab;

        public VisualEffect CoinVFX;

        public VisualEffect WinEffect;
        public VisualEffect LoseEffect;
    }

    /// <summary>
    /// Setting related to bonus gem, list all the existing bonus gems. 
    /// </summary>
    [System.Serializable]
    public class BonusSetting
    {
        public BonusGem[] Bonuses;
    }

    /// <summary>
    /// Settings related to the Shop, list all ShopItems.
    /// </summary>
    [System.Serializable]
    public class ShopSetting
    {
        public abstract class ShopItem : ScriptableObject
        { 
            public Sprite ItemSprite;
            public string ItemName;
            public int Price;

            public virtual bool CanBeBought()
            {
                return GameManager.Instance.Coins >= Price; 
            }
        
            public abstract void Buy();
        }

        public ShopItem[] Items;
    }

    [System.Serializable]
    public class SoundSetting
    {
        public AudioMixer Mixer;
        
        public AudioSource SFXSourcePrefab;
        public AudioSource MusicSourcePrefab;

        public AudioClip MenuSound;
        
        public AudioClip SwipSound;
        public AudioClip FallSound;
        public AudioClip CoinSound;

        public AudioClip WinVoice;
        public AudioClip LooseVoice;

        public AudioClip WinSound;
        public AudioClip LooseSound;
    }
}