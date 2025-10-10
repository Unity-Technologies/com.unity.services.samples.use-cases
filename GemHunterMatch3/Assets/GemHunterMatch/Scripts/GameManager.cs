using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

namespace Match3
{
    /// <summary>
    /// The GameManager is the interface between all the system in the game. It is either instantiated by the Loading scene
    /// which is the first at the start of the game, or Loaded from the Resource Folder dynamically at first access in editor
    /// so we can press play from any point of the game without having to add it to every scene and test if it already exist
    /// </summary>
    [DefaultExecutionOrder(-9999)]
    public class GameManager : MonoBehaviour
    {
        //This is set to true when the manager is deleted. This is useful as the manager can be deleted before other
        //objects 
        private static bool s_IsShuttingDown = false;
        public event Action ReturnToHubWonResult;
        public event Action ReturnToHubLostResult;
        public event Action ReplayLevelLost;
        
        private bool m_HasWonLevel;
        
        public static GameManager Instance
        {
            get
            {
                
                // In Editor, the instance can be crated on the fly so we can play any scene without setup to do.
                // In a build, the first scene will Init all that so we are sure there will already be an instance.
#if UNITY_EDITOR
                if (s_Instance == null && !s_IsShuttingDown)
                {
                    var newInstance = Instantiate(Resources.Load<GameManager>("GameManager"));
                    newInstance.Awake();
                }
#endif
                return s_Instance;
            }

            private set => s_Instance = value;
        }

        public static bool IsShuttingDown()
        {
            return s_IsShuttingDown;
        }

        [Serializable]
        public class SoundData
        {
            public float MainVolume = 1.0f;
            public float MusicVolume = 1.0f;
            public float SFXVolume = 1.0f;
        }

        [System.Serializable]
        public class BonusItemEntry
        {
            public int Amount;
            public BonusItem Item;
        }
    
        private static GameManager s_Instance;

        public Board Board;
        public InputAction ClickAction;
        public InputAction ClickPosition;
        public GameSettings Settings;

        public int Coins { get; private set; } = 0;
        public int Stars { get; private set; }
        public int Lives { get; private set; } = 5;

        public SoundData Volumes => m_SoundData;

        public List<BonusItemEntry> BonusItems = new();

        public VFXPoolSystem PoolSystem { get; private set; } = new();

        //we use two sources so we can crossfade
        private AudioSource MusicSourceActive;
        private AudioSource MusicSourceBackground;
        private Queue<AudioSource> m_SFXSourceQueue = new();

        private GameObject m_BonusModePrefab;
    
        private VisualEffect m_WinEffect;
        private VisualEffect m_LoseEffect;
        
        private SoundData m_SoundData = new();

        private void Awake()
        {
            if (s_Instance == this)
            {
                return;
            }

            if (s_Instance == null)
            {
                s_Instance = this;
                DontDestroyOnLoad(gameObject);
                
                Application.targetFrameRate = 60;
            
                ClickAction.Enable();
                ClickPosition.Enable();

                MusicSourceActive = Instantiate(Settings.SoundSettings.MusicSourcePrefab, transform);
                MusicSourceBackground = Instantiate(Settings.SoundSettings.MusicSourcePrefab, transform);

                MusicSourceActive.volume = 1.0f;
                MusicSourceBackground.volume = 0.0f;

                for (int i = 0; i < 16; ++i)
                {
                    var sourceInst = Instantiate(Settings.SoundSettings.SFXSourcePrefab, transform);
                    m_SFXSourceQueue.Enqueue(sourceInst);
                }

                if (Settings.VisualSettings.BonusModePrefab != null)
                {
                    m_BonusModePrefab = Instantiate(Settings.VisualSettings.BonusModePrefab);
                    m_BonusModePrefab.SetActive(false);
                }

                m_WinEffect = Instantiate(Settings.VisualSettings.WinEffect, transform);
                m_LoseEffect = Instantiate(Settings.VisualSettings.LoseEffect, transform);

                LoadSoundData();
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this) s_IsShuttingDown = true;
        }

        void GetReferences()
        {
            Board = FindFirstObjectByType<Board>();
        }

        /// <summary>
        /// Called by the LevelData when it awake, notify the GameManager we started a new level.
        /// </summary>
        public void StartLevel()
        {
            GetReferences();
            UIHandler.Instance.Display(true);
            
            m_WinEffect.gameObject.SetActive(false);
            m_LoseEffect.gameObject.SetActive(false);
            
            LevelData.Instance.OnAllGoalFinished += () =>
            {
                Instance.Board.ToggleInput(false);
                Instance.Board.TriggerFinalStretch();
            };

            LevelData.Instance.OnNoMoveLeft += () =>
            {
                Instance.Board.ToggleInput(false);
                Instance.Board.TriggerFinalStretch();
            };

            if (LevelData.Instance.Music != null)
            {
                SwitchMusic(LevelData.Instance.Music);
            }

            PoolSystem.AddNewInstance(Settings.VisualSettings.CoinVFX, 12);

            //we delay the board init to leave enough time for all the tile to init
            StartCoroutine(DelayedInit());
        }

        IEnumerator DelayedInit()
        {
            yield return null;

            Board.Init();
            ComputeCamera();
        }
        
        public void ComputeCamera()
        {
            //setup the camera so it look at the center of the play area, and change its ortho setting so it perfectly frame
            var bounds = Board.Bounds;
            Vector3 center = Board.Grid.CellToLocalInterpolated(bounds.center) + new Vector3(0.5f, 0.5f, 0.0f);
            center = Board.transform.TransformPoint(center);
            
            //we offset of 1 up as the top bar is thicker, so this center it better between the top & bottom bar
            Camera.main.transform.position = center + Vector3.back * 10.0f + Vector3.up * 0.75f;

            float halfSize = 0.0f;
            
            if (Screen.height > Screen.width)
            {
                float screenRatio = Screen.height / (float)Screen.width;
                halfSize = ((bounds.size.x + 1) * 0.5f + LevelData.Instance.BorderMargin) * screenRatio;
            }
            else
            {
                //On Wide screen, we fit vertically
                halfSize = (bounds.size.y + 3) * 0.5f + LevelData.Instance.BorderMargin;
            }

            halfSize += LevelData.Instance.BorderMargin;
        
            Camera.main.orthographicSize = halfSize;
        }

        /// <summary>
        /// Called by the Main Menu when it open, notify the GameManager we are back in the menu so need to hide the Game UI.
        /// </summary>
        public void MainMenuOpened()
        {
            PoolSystem.Clean();
            m_WinEffect.gameObject.SetActive(false);
            m_LoseEffect.gameObject.SetActive(false);
            
            SwitchMusic(Instance.Settings.SoundSettings.MenuSound);
            UIHandler.Instance.Display(false);
        }

        private void Update()
        {
            //In the editor or a development build, F12 can open a debug menu to add any gem anywhere, but in final build
            //we do not need to test for that.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                UIHandler.Instance.ToggleDebugMenu();
            }
#endif

            if (MusicSourceActive.volume < 1.0f)
            {
                MusicSourceActive.volume = Mathf.MoveTowards(MusicSourceActive.volume, 1.0f, Time.deltaTime * 0.5f);
                MusicSourceBackground.volume = Mathf.MoveTowards(MusicSourceBackground.volume, 0.0f, Time.deltaTime * 0.5f);
            }
        }
        
        public void ChangeCoins(int amount)
        {
            Coins += amount;
            if (Coins < 0)
                Coins = 0;
            
            UIHandler.Instance.UpdateTopBarData();
        }
        
        public void WinStar()
        {
            Stars += 1;
        }

        public void AddLive(int amount)
        {
            Lives += amount;
        }

        public void LoseLife()
        {
            Lives -= 1;
        }

        public void UpdateVolumes()
        {
            Settings.SoundSettings.Mixer.SetFloat("MainVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.MainVolume)) * 30.0f);
            Settings.SoundSettings.Mixer.SetFloat("SFXVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.SFXVolume)) * 30.0f);
            Settings.SoundSettings.Mixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(0.0001f, m_SoundData.MusicVolume)) * 30.0f);
        }

        public void SaveSoundData()
        {
            System.IO.File.WriteAllText(Application.persistentDataPath + "/sounds.json", JsonUtility.ToJson(m_SoundData));
        }

        void LoadSoundData()
        {
            if (System.IO.File.Exists(Application.persistentDataPath + "/sounds.json"))
            {
                JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(Application.persistentDataPath+"/sounds.json"), m_SoundData);
            }
            
            UpdateVolumes();
        }

        public void AddBonusItem(BonusItem item)
        {
            var existingItem = BonusItems.Find(entry => entry.Item == item);

            if (existingItem != null)
            {
                existingItem.Amount += 1;
            }
            else
            {
                BonusItems.Add(new BonusItemEntry()
                {
                    Amount = 1,
                    Item = item
                });
            }
            
            UIHandler.Instance.UpdateBottomBar();
        }

        public void ActivateBonusItem(BonusItem item)
        {
            LevelData.Instance.DarkenBackground(item != null);
            m_BonusModePrefab?.SetActive(item != null);
            Board.ActivateBonusItem(item);
        }

        public void UseBonusItem(BonusItem item, Vector3Int cell)
        {
            var existingItem = BonusItems.Find(entry => entry.Item == item);
            if(existingItem == null) return;
        
            existingItem.Item.Use(cell);
            existingItem.Amount -= 1;
            
            m_BonusModePrefab?.SetActive(false);
            UIHandler.Instance.UpdateBottomBar();
            UIHandler.Instance.DeselectBonusItem();
        }

        public AudioSource PlaySFX(AudioClip clip)
        {
            var source = m_SFXSourceQueue.Dequeue();
            m_SFXSourceQueue.Enqueue(source);

            source.clip = clip;
            source.Play();

            return source;
        }

        public void WinTriggered()
        {
            PlaySFX(Settings.SoundSettings.WinVoice);
            m_WinEffect.gameObject.SetActive(true);
            m_HasWonLevel = true;
        }

        public void LoseTriggered()
        {
            PlaySFX(Settings.SoundSettings.LooseVoice);
            m_LoseEffect.gameObject.SetActive(true);
            m_HasWonLevel = false;
        }
        
        public void EndLevelContinueReturnToHub()
        {
            if(m_HasWonLevel)
                ReturnToHubWonResult?.Invoke();
            else
                ReturnToHubLostResult?.Invoke();
        }

        public void TriggerReplayLevelLostEvent()
        {
            ReplayLevelLost?.Invoke();
        }

        void SwitchMusic(AudioClip music)
        {
            MusicSourceBackground.clip = music;
            MusicSourceBackground.Play();
            (MusicSourceActive, MusicSourceBackground) = (MusicSourceBackground, MusicSourceActive);
        }
    }
}