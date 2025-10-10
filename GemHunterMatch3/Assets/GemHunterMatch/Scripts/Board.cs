using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Match3;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Match3
{
    [DefaultExecutionOrder(-9999)]
    public class Board : MonoBehaviour
    {
        //Board hold a list of BoardAction that get ticked on its Update. Useful for Bonus to add timed effects and the like.
        public interface IBoardAction
        {
            //Return true if should continue, false if the action done 
            bool Tick();
        }

        public class PossibleSwap
        {
            public Vector3Int StartPosition;
            public Vector3Int Direction;
        }

        private static Board s_Instance;

        public List<Vector3Int> SpawnerPosition = new();
        public Dictionary<Vector3Int, BoardCell> CellContent = new();

        public Gem[] ExistingGems;

        public VisualEffect GemHoldPrefab;
        public VisualEffect HoldTrailPrefab;

        public BoundsInt Bounds => m_BoundsInt;
        public Grid Grid => m_Grid;

        private bool m_BoardWasInit = false;
        private bool m_InputEnabled = true;
        private bool m_FinalStretch = false;//set when either reach goal or no move left. When board settle, trigger the end
        
        private Grid m_Grid;
        private BoundsInt m_BoundsInt;

        private Dictionary<int, Gem> m_GemLookup;

        private VisualSetting m_VisualSettingReference;

        private List<Vector3Int> m_TickingCells = new();
        private List<Vector3Int> m_NewTickingCells = new();
        private List<Match> m_TickingMatch = new();
        private List<Vector3Int> m_CellToMatchCheck = new();

        private bool m_BoardChanged = false;
        private List<PossibleSwap> m_PossibleSwaps = new();
        private GameObject m_HintIndicator;
        private int m_PickedSwap;
        private float m_SinceLastHint = 0.0f;

        //this is set by some bonus like the rocket to stop gem moving in/out of the rocket path. Once rocket is done
        //it unfreeze to let everything fall again. Increment at each lock, decrement on unlock so multiple lock are
        //possible
        private int m_FreezeMoveLock = 0;

        private List<Vector3Int> m_EmptyCells = new();

        private Dictionary<Vector3Int, Action> m_CellsCallbacks = new();
        private Dictionary<Vector3Int, Action> m_MatchedCallback = new();

        private List<IBoardAction> m_BoardActions = new();

        private bool m_SwipeQueued;
        private Vector3Int m_StartSwipe;
        private Vector3Int m_EndSwipe;
        //private bool m_IsHoldingTouch;

        private float m_LastClickTime = 0.0f;

        private BonusItem m_ActivatedBonus;

        private VisualEffect m_GemHoldVFXInstance;
        private VisualEffect m_HoldTrailInstance;

        private AudioSource m_FallingSoundSource;

        private enum SwapStage
        {
            None,
            Forward,
            Return
        }

        private SwapStage m_SwapStage = SwapStage.None;
        private (Vector3Int, Vector3Int) m_SwappingCells;

        //---- interaction
        public Vector3 m_StartClickPosition;

        // Start is called before the first frame update
        void Awake()
        {
            s_Instance = this;
            GetReference();
        }

        private void Start()
        {
#if !UNITY_EDITOR
        //In a built player, the tilemap data are not refreshed, and the edit time one are the one used. We need to
        //to refresh it as otherwise the edit time preview sprite would still be used.
        var tilemaps = m_Grid.GetComponentsInChildren<Tilemap>();
        foreach (var tilemap in tilemaps)
        {
            tilemap.RefreshAllTiles();
        }
#endif
        }

        private void OnDestroy()
        {
            //order of deletion when quitting the game can make the manager be destroyed first so we make sure it's not
            //shutting down, otherwise in editor, calling GameManager.Instance would create a new game manager.
            if(!GameManager.IsShuttingDown()) GameManager.Instance.PoolSystem.Clean();
        }

        void GetReference()
        {
            m_Grid = GetComponent<Grid>();
        }

        public void ToggleInput(bool input)
        {
            m_InputEnabled = input;
        }

        public void TriggerFinalStretch()
        {
            m_FinalStretch = true;
        }

        public void LockMovement()
        {
            m_FreezeMoveLock += 1;
        }

        public void UnlockMovement()
        {
            m_FreezeMoveLock -= 1;
        }

        public void Init()
        {
            m_VisualSettingReference = GameManager.Instance.Settings.VisualSettings;
            m_LastClickTime = Time.time;

            UIHandler.Instance.Init();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var bonus in GameManager.Instance.Settings.BonusSettings.Bonuses)
            {
                UIHandler.Instance.RegisterGemToDebug(bonus);
            }
        
            foreach (var gem in GameManager.Instance.Board.ExistingGems)
            {
                UIHandler.Instance.RegisterGemToDebug(gem);
            }
#endif
            
            //fill a lookup of gem type to gem
            m_GemLookup = new Dictionary<int, Gem>();
            foreach (var gem in ExistingGems)
            {
                m_GemLookup.Add(gem.GemType, gem);
            }

            GenerateBoard();
            FindAllPossibleMatch();
        
            m_HintIndicator = Instantiate(GameManager.Instance.Settings.VisualSettings.HintPrefab);
            m_HintIndicator.SetActive(false);
        
            m_BoardWasInit = true;

            if (GemHoldPrefab != null)
            {
                m_GemHoldVFXInstance = Instantiate(GemHoldPrefab);
                m_GemHoldVFXInstance.gameObject.SetActive(false);
            }

            if (HoldTrailPrefab != null)
            {
                m_HoldTrailInstance = Instantiate(HoldTrailPrefab);
                m_HoldTrailInstance.gameObject.SetActive(false);
            }

            ToggleInput(false);
            //we wait couple of frames before fading in, as UI was just init yet so animation would not play
            StartCoroutine(WaitToFadeIn());
        }

        IEnumerator WaitToFadeIn()
        {
            yield return null;
            yield return null;
            UIHandler.Instance.FadeIn(() => { ToggleInput(true); });
        }

        //Called by Gem Placer to create a placement cell
        public static void RegisterCell(Vector3Int cellPosition, Gem startingGem = null)
        {
            //Not super happy with that, but Startup is called before all Awake....
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            if(!s_Instance.CellContent.ContainsKey(cellPosition))
                s_Instance.CellContent.Add(cellPosition, new BoardCell());

            if (startingGem != null)
            {
                s_Instance.NewGemAt(cellPosition, startingGem);
            }
        }

        public static void AddObstacle(Vector3Int cell, Obstacle obstacle)
        {
            RegisterCell(cell);

            obstacle.transform.position = s_Instance.m_Grid.GetCellCenterWorld(cell);
            s_Instance.CellContent[cell].Obstacle = obstacle;
        }

        public static void ChangeLock(Vector3Int cellPosition, bool lockState)
        {
            //Not super happy with that, but Startup is called before all Awake....
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            s_Instance.CellContent[cellPosition].Locked = lockState;
        }

        public static void RegisterDeletedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if (!s_Instance.m_CellsCallbacks.ContainsKey(cellPosition))
            {
                s_Instance.m_CellsCallbacks[cellPosition] = callback;
            }
            else
            {
                s_Instance.m_CellsCallbacks[cellPosition] += callback;
            }
        }

        public static void UnregisterDeletedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if(!s_Instance.m_CellsCallbacks.ContainsKey(cellPosition))
                return;
        
            s_Instance.m_CellsCallbacks[cellPosition] -= callback;
            if (s_Instance.m_CellsCallbacks[cellPosition] == null)
                s_Instance.m_CellsCallbacks.Remove(cellPosition);
        }
        
        public static void RegisterMatchedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if (!s_Instance.m_MatchedCallback.ContainsKey(cellPosition))
            {
                s_Instance.m_MatchedCallback[cellPosition] = callback;
            }
            else
            {
                s_Instance.m_MatchedCallback[cellPosition] += callback;
            }
        }

        public static void UnregisterMatchedCallback(Vector3Int cellPosition, System.Action callback)
        {
            if(!s_Instance.m_MatchedCallback.ContainsKey(cellPosition))
                return;
        
            s_Instance.m_MatchedCallback[cellPosition] -= callback;
            if (s_Instance.m_MatchedCallback[cellPosition] == null)
                s_Instance.m_MatchedCallback.Remove(cellPosition);
        }

        public static void RegisterSpawner(Vector3Int cell)
        {
            //Not super happy with that, but Startup is called before all Awake....
            if (s_Instance == null)
            {
                s_Instance = GameObject.Find("Grid").GetComponent<Board>();
                s_Instance.GetReference();
            }

            s_Instance.SpawnerPosition.Add(cell);
        }

        //generate a gem in every cell, making sure we don't have any match 
        void GenerateBoard()
        {
            m_BoundsInt = new BoundsInt();
            var listOfCells = CellContent.Keys.ToList();

            m_BoundsInt.xMin = listOfCells[0].x;
            m_BoundsInt.xMax = m_BoundsInt.xMin;
        
            m_BoundsInt.yMin = listOfCells[0].y;
            m_BoundsInt.yMax = m_BoundsInt.yMin;

            foreach (var content in listOfCells)
            {
                if (content.x > m_BoundsInt.xMax)
                    m_BoundsInt.xMax = content.x;
                else if (content.x < m_BoundsInt.xMin)
                    m_BoundsInt.xMin = content.x;

                if (content.y > m_BoundsInt.yMax)
                    m_BoundsInt.yMax = content.y;
                else if (content.y < m_BoundsInt.yMin)
                    m_BoundsInt.yMin = content.y;
            }

            for (int y = m_BoundsInt.yMin; y <= m_BoundsInt.yMax; ++y)
            {
                for (int x = m_BoundsInt.xMin; x <= m_BoundsInt.xMax; ++x)
                {
                    var idx = new Vector3Int(x, y, 0);
                
                    if(!CellContent.TryGetValue(idx, out var current) || current.ContainingGem != null)
                        continue;
                
                    var availableGems = m_GemLookup.Keys.ToList();

                    int leftGemType = -1;
                    int bottomGemType = -1;
                    int rightGemType = -1;
                    int topGemType = -1;

                    //check if there is two gem of the same type of the left
                    if (CellContent.TryGetValue(idx + new Vector3Int(-1, 0, 0), out var leftContent) &&
                        leftContent.ContainingGem != null)
                    {
                        leftGemType = leftContent.ContainingGem.GemType;
                    
                        if (CellContent.TryGetValue(idx + new Vector3Int(-2, 0, 0), out var leftLeftContent) &&
                            leftLeftContent.ContainingGem != null && leftGemType == leftLeftContent.ContainingGem.GemType)
                        {
                            //we have two gem of a given type on the left, so we can't ue that type anymore
                            availableGems.Remove(leftGemType);
                        }
                    }
                
                    //check if there is two gem of the same type below
                    if (CellContent.TryGetValue(idx + new Vector3Int(0, -1, 0), out var bottomContent) &&
                        bottomContent.ContainingGem != null)
                    {
                        bottomGemType = bottomContent.ContainingGem.GemType;
                        
                        if (CellContent.TryGetValue(idx + new Vector3Int(0, -2, 0), out var bottomBottomContent) &&
                            bottomBottomContent.ContainingGem != null && bottomGemType == bottomBottomContent.ContainingGem.GemType)
                        {
                            //we have two gem of a given type on the bottom, so we can't ue that type anymore
                            availableGems.Remove(bottomGemType);
                        }

                        if (leftGemType != -1 && leftGemType == bottomGemType)
                        {
                            //if the left and bottom gem are the same type, we need to check if the bottom left gem is ALSO
                            //of the same type, as placing that type here would create a square, which is a valid match
                            if (CellContent.TryGetValue(idx + new Vector3Int(-1, -1, 0), out var bottomLeftContent) &&
                                bottomLeftContent.ContainingGem != null && bottomGemType == leftGemType)
                            {
                                //we already have a corner of gem on left, bottom left and bottom position, so remove that type
                                availableGems.Remove(leftGemType);
                            }
                        }
                    }
                    
                    //as we fill left to right and bottom to top, we could only test left and bottom, but as we can have
                    //manually placed gems, we still need to test in the other 2 direction to make sure
                    
                    //check right
                    if (CellContent.TryGetValue(idx + new Vector3Int(1, 0, 0), out var rightContent) &&
                        rightContent.ContainingGem != null)
                    {
                        rightGemType = rightContent.ContainingGem.GemType;

                        //we have the same type on left and right, so placing that type here would create a 3 line
                        if (rightGemType != -1 && leftGemType == rightGemType)
                        {
                            availableGems.Remove(rightGemType);
                        }
                    
                        if (CellContent.TryGetValue(idx + new Vector3Int(2, 0, 0), out var rightRightContent) &&
                            rightRightContent.ContainingGem != null && rightGemType == rightRightContent.ContainingGem.GemType)
                        {
                            //we have two gem of a given type on the right, so we can't ue that type anymore
                            availableGems.Remove(rightGemType);
                        }

                        //right and bottom gem are the same, check the bottom right to avoid creating a square
                        if (rightGemType != -1 && rightGemType == bottomGemType)
                        {
                            if (CellContent.TryGetValue(idx + new Vector3Int(1, -1, 0), out var bottomRightContent) &&
                                bottomRightContent.ContainingGem != null && bottomRightContent.ContainingGem.GemType == rightGemType)
                            {
                                availableGems.Remove(rightGemType);
                            }
                        }
                    }
                    
                    //check up
                    if (CellContent.TryGetValue(idx + new Vector3Int(0, 1, 0), out var topContent) &&
                        topContent.ContainingGem != null)
                    {
                        topGemType = topContent.ContainingGem.GemType;

                        //we have the same type on top and bottom, so placing that type here would create a 3 line
                        if (topGemType != -1 && topGemType == bottomGemType)
                        {
                            availableGems.Remove(topGemType);
                        }
                    
                        if (CellContent.TryGetValue(idx + new Vector3Int(0, 1, 0), out var topTopContent) &&
                            topTopContent.ContainingGem != null && topGemType == topTopContent.ContainingGem.GemType)
                        {
                            //we have two gem of a given type on the top, so we can't ue that type anymore
                            availableGems.Remove(topGemType);
                        }

                        //right and top gem are the same, check the top right to avoid creating a square
                        if (topGemType != -1 && topGemType == rightGemType)
                        {
                            if (CellContent.TryGetValue(idx + new Vector3Int(1, 1, 0), out var topRightContent) &&
                                topRightContent.ContainingGem != null && topRightContent.ContainingGem.GemType == topGemType)
                            {
                                availableGems.Remove(topGemType);
                            }
                        }
                        
                        //left and top gem are the same, check the top left to avoid creating a square
                        if (topGemType != -1 && topGemType == leftGemType)
                        {
                            if (CellContent.TryGetValue(idx + new Vector3Int(-1, 1, 0), out var topLeftContent) &&
                                topLeftContent.ContainingGem != null && topLeftContent.ContainingGem.GemType == topGemType)
                            {
                                availableGems.Remove(topGemType);
                            }
                        }
                    }
                    

                    var chosenGem = availableGems[Random.Range(0, availableGems.Count)];
                    NewGemAt(idx, m_GemLookup[chosenGem]);
                }
            }
        }

        private void Update()
        {
            if(!m_BoardWasInit)
                return;
            
            GameManager.Instance.PoolSystem.Update();

            for (int i = 0; i < m_BoardActions.Count; ++i)
            {
                if (!m_BoardActions[i].Tick())
                {
                    m_BoardActions.RemoveAt(i);
                    i--;
                }
            }
        
            CheckInput();

            if (m_SwapStage != SwapStage.None)
            {
                TickSwap();
                //return;
            }

            //this will set to false if ANYTHING happen.
            //only increment when the board is still and nothing happens
            //the starting value is if we have a bonus item or not (if we have one, this cannot increment)
            bool incrementHintTimer = m_ActivatedBonus == null;

            if (m_TickingCells.Count > 0)
            {
                MoveGems();
                
                //to avoid sound clash we make sure we only play a falling sound if none are already playing
                if (m_TickingCells.Count == 0 && (m_FallingSoundSource == null || !m_FallingSoundSource.isPlaying))
                {
                    m_FallingSoundSource = GameManager.Instance.PlaySFX(GameManager.Instance.Settings.SoundSettings.FallSound);
                }

                incrementHintTimer = false;
                m_BoardChanged = true;
            }
            
            if (m_CellToMatchCheck.Count > 0)
            {
                DoMatchCheck();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            }
            
            if (m_TickingMatch.Count > 0)
            {
                MatchTicking();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            } 
            
            if (m_EmptyCells.Count > 0)
            {
                EmptyCheck();
                
                incrementHintTimer = false;
                m_BoardChanged = true;
            } 
            
            if (m_SwipeQueued)
            {
                CellContent[m_StartSwipe].IncomingGem = CellContent[m_EndSwipe].ContainingGem;
                CellContent[m_EndSwipe].IncomingGem = CellContent[m_StartSwipe].ContainingGem;

                CellContent[m_StartSwipe].ContainingGem = null;
                CellContent[m_EndSwipe].ContainingGem = null;

                m_SwapStage = SwapStage.Forward;
                m_SwappingCells = (m_StartSwipe, m_EndSwipe);
                
                GameManager.Instance.PlaySFX(GameManager.Instance.Settings.SoundSettings.SwipSound);

                m_SwipeQueued = false;
                incrementHintTimer = false;
            }

            if (m_NewTickingCells.Count > 0)
            {
                m_TickingCells.AddRange(m_NewTickingCells);
                m_NewTickingCells.Clear();
                incrementHintTimer = false;
            }
            
            if (incrementHintTimer)
            {
                //nothing can happen anymore, if we were in the last stretch trigger the end
                if (m_FinalStretch)
                {
                    //this stop the end to be called in a loop. Input is still disabled to user cannot interact with board
                    m_FinalStretch = false;
                    UIHandler.Instance.ShowEnd();
                    return;
                }
                
                //Nothing happened this frame, but the board was changed since last possible match check, so need to refresh
                if (m_BoardChanged)
                {
                    FindAllPossibleMatch();
                    m_BoardChanged = false;
                    
                    // Prevents error if no hints are possible.
                    if (m_PossibleSwaps.Count == 0)
                    {
                        return;
                    }
                }
            
                var match = m_PossibleSwaps[m_PickedSwap];
                if (m_HintIndicator.activeSelf)
                {
                    var startPos = m_Grid.GetCellCenterWorld(match.StartPosition);
                    var endPos = m_Grid.GetCellCenterWorld(match.StartPosition + match.Direction);

                    var current = m_HintIndicator.transform.position;
                    current = Vector3.MoveTowards(current, endPos, 1.0f * Time.deltaTime);

                    m_HintIndicator.transform.position = current == endPos ? startPos : current;
                }
                else
                {
                    m_SinceLastHint += Time.deltaTime;
                    if (m_SinceLastHint >= GameManager.Instance.Settings.InactivityBeforeHint && m_InputEnabled)
                    {
                        m_HintIndicator.transform.position = m_Grid.GetCellCenterWorld(match.StartPosition);
                        m_HintIndicator.SetActive(true);
                    }
                }
            }
            else
            {
                m_HintIndicator.SetActive(false);
                m_SinceLastHint = 0.0f;
            }
        }

        void MoveGems()
        {
            //sort bottom left to top right, so we minimize timing issue (a gem on top try to fall into a cell that is 
            //not yet empty but will be empty once the bottom gem move away)
            m_TickingCells.Sort((a, b) =>
            {
                int yCmp = a.y.CompareTo(b.y);
                if (yCmp == 0)
                {
                    return a.x.CompareTo(b.x);
                }

                return yCmp;
            });

            for (int i = 0; i < m_TickingCells.Count; i++)
            {
                var cellIdx = m_TickingCells[i];

                var currentCell = CellContent[cellIdx];
                var targetPosition = m_Grid.GetCellCenterWorld(cellIdx);
                
                if (currentCell.IncomingGem != null && currentCell.ContainingGem != null)
                {
                    Debug.LogError(
                        $"A ticking cell at {cellIdx} have incoming gems {currentCell.IncomingGem} containing gem {currentCell.ContainingGem}");
                    continue;
                }
                
                //update either position or state.
                if (currentCell.IncomingGem?.CurrentState == Gem.State.Falling)
                {
                    var gem = currentCell.IncomingGem;
                    gem.TickMoveTimer(Time.deltaTime);

                    var maxDistance = m_VisualSettingReference.FallAccelerationCurve.Evaluate(gem.FallTime) *
                                      Time.deltaTime * m_VisualSettingReference.FallSpeed * gem.SpeedMultiplier;
                    
                    gem.transform.position = Vector3.MoveTowards(gem.transform.position, targetPosition,
                        maxDistance);

                    if (gem.transform.position == targetPosition)
                    {
                        m_TickingCells.RemoveAt(i);
                        i--;

                        currentCell.IncomingGem = null;
                        currentCell.ContainingGem = gem;
                        gem.MoveTo(cellIdx);
                        
                        //reached target position, now check if continue falling or finished its fall.
                        if (m_EmptyCells.Contains(cellIdx + Vector3Int.down) && CellContent.TryGetValue(cellIdx + Vector3Int.down, out var belowCell))
                        {
                            //incoming gem goes to the below cell
                            currentCell.ContainingGem = null;
                            belowCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.0f;

                            var target = cellIdx + Vector3Int.down;
                            m_NewTickingCells.Add(target);
                            
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            //if we continue falling, this is now an empty space, if there is a gem above it will fall by itself
                            //but if this is a spawner above, we need to spawn a new gem
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else if ((!CellContent.TryGetValue(cellIdx + Vector3Int.left, out var leftCell) ||
                                  leftCell.BlockFall) && 
                                 m_EmptyCells.Contains(cellIdx + Vector3Int.down + Vector3Int.left) &&
                                 CellContent.TryGetValue(cellIdx + Vector3Int.down + Vector3Int.left, out var belowLeftCell))
                        {
                            //the cell to the left is either non existing or locked, and below that is an empty space, we can fall diagonally
                            currentCell.ContainingGem = null;
                            belowLeftCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.41421356237f;

                            var target = cellIdx + Vector3Int.down + Vector3Int.left;
                            m_NewTickingCells.Add(target);
                            
                            //if the empty cell was part of the empty cell list, we need to remove it it's not empty anymore
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            //if we continue falling, this is now an empty space, if there is a gem above it will fall by itself
                            //but if this is a spawner above, we need to spawn a new gem
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else if ((!CellContent.TryGetValue(cellIdx + Vector3Int.right, out var rightCell) ||
                                  rightCell.BlockFall) &&
                                 m_EmptyCells.Contains(cellIdx + Vector3Int.down + Vector3Int.right) &&
                                 CellContent.TryGetValue(cellIdx + Vector3Int.down + Vector3Int.right, out var belowRightCell))
                        {
                            //we couldn't fall directly below, so we check diagonally
                            //incoming gem goes to the below cell
                            currentCell.ContainingGem = null;
                            belowRightCell.IncomingGem = gem;

                            gem.SpeedMultiplier = 1.41421356237f;

                            var target = cellIdx + Vector3Int.down + Vector3Int.right;
                            m_NewTickingCells.Add(target);
                            
                            //if the empty cell was part of the empty cell list, we need to remove it it's not empty anymore
                            m_EmptyCells.Remove(target);
                            m_EmptyCells.Add(cellIdx);

                            //if we continue falling, this is now an empty space, if there is a gem above it will fall by itself
                            //but if this is a spawner above, we need to spawn a new gem
                            if (SpawnerPosition.Contains(cellIdx + Vector3Int.up))
                            {
                                ActivateSpawnerAt(cellIdx);
                            }
                        }
                        else
                        {
                            //re add but this time we will bounce and not fall.
                            m_NewTickingCells.Add(cellIdx);
                            gem.StopFalling();
                        }
                    }
                }
                else if (currentCell.ContainingGem?.CurrentState == Gem.State.Bouncing)
                {
                    var gem = currentCell.ContainingGem;
                    gem.TickMoveTimer(Time.deltaTime);
                    Vector3 center = m_Grid.GetCellCenterWorld(cellIdx);

                    float maxTime = m_VisualSettingReference.BounceCurve
                        .keys[m_VisualSettingReference.BounceCurve.length - 1].time;
                    
                    if (gem.FallTime >= maxTime)
                    {
                        gem.transform.position = center;
                        gem.transform.localScale = Vector3.one;
                        gem.StopBouncing();

                        m_TickingCells.RemoveAt(i);
                        i--;
                        m_CellToMatchCheck.Add(cellIdx);
                    }
                    else
                    {
                        gem.transform.position =
                            center + Vector3.up * m_VisualSettingReference.BounceCurve.Evaluate(gem.FallTime);
                        gem.transform.localScale =
                            new Vector3(1, m_VisualSettingReference.SquishCurve.Evaluate(gem.FallTime), 1);
                    }
                }
                else if(currentCell.ContainingGem?.CurrentState == Gem.State.Still)
                {
                    //a ticking cells should only be falling or bouncing, if neither of those, remove it 
                    m_TickingCells.RemoveAt(i);
                    i--;
                }

            }
        }

        void MatchTicking()
        {
            for (int i = 0; i < m_TickingMatch.Count; ++i)
            {
                var match = m_TickingMatch[i];

                Debug.Assert(match.MatchingGem.Count == match.MatchingGem.Distinct().Count(),
                    "There is duplicate gems in the matching lists");

                const float deletionSpeed = 1.0f / 0.3f;
                match.DeletionTimer += Time.deltaTime * deletionSpeed;
                
                for(int j = 0; j < match.MatchingGem.Count; j++)
                {
                    var gemIdx = match.MatchingGem[j];
                    var gem = CellContent[gemIdx].ContainingGem;

                    if (gem == null)
                    {
                        match.MatchingGem.RemoveAt(j);
                        j--;
                        continue;
                    }

                    if (gem.CurrentState == Gem.State.Bouncing)
                    {
                        //we stop it bouncing as it is getting destroyed
                        //We check both current and new ticking cells, as it could be the first frame where it started
                        //bouncing so it will be in the new ticking cells NOT in the ticking cell list yet.
                        if(m_TickingCells.Contains(gemIdx)) m_TickingCells.Remove(gemIdx);
                        if(m_NewTickingCells.Contains(gemIdx)) m_NewTickingCells.Remove(gemIdx);
                        
                        gem.transform.position = m_Grid.GetCellCenterWorld(gemIdx);
                        gem.transform.localScale = Vector3.one;
                        gem.StopBouncing();
                    }

                    //forced deletion doesn't wait for end of timer
                    if (match.ForcedDeletion || match.DeletionTimer > 1.0f)
                    {
                        Destroy(CellContent[gemIdx].ContainingGem.gameObject);
                        CellContent[gemIdx].ContainingGem = null;
                    
                        if (match.ForcedDeletion && CellContent[gemIdx].Obstacle != null)
                        {
                            CellContent[gemIdx].Obstacle.Clear();
                        }

                        //callback are only called when this was a match from swipe and not from bonus or other source 
                        if (!match.ForcedDeletion && m_CellsCallbacks.TryGetValue(gemIdx, out var clbk))
                        {
                            clbk.Invoke();
                        }
                    
                        match.MatchingGem.RemoveAt(j);
                        j--;

                        match.DeletedCount += 1;
                        //we only spawn coins for non bonus match
                        if (match.DeletedCount >= 4 && !match.ForcedDeletion)
                        {
                            GameManager.Instance.ChangeCoins(1);
                            GameManager.Instance.PoolSystem.PlayInstanceAt(GameManager.Instance.Settings.VisualSettings.CoinVFX,
                                gem.transform.position);
                        }
                    
                        if (match.SpawnedBonus != null && match.OriginPoint == gemIdx)
                        {
                            NewGemAt(match.OriginPoint, match.SpawnedBonus);
                        }
                        else
                        {
                            m_EmptyCells.Add(gemIdx);
                        }

                        //
                        if (gem.CurrentState != Gem.State.Disappearing)
                        {
                            LevelData.Instance.Matched(gem);
                            
                            foreach (var matchEffectPrefab in gem.MatchEffectPrefabs)
                            {
                                GameManager.Instance.PoolSystem.PlayInstanceAt(matchEffectPrefab, m_Grid.GetCellCenterWorld(gem.CurrentIndex));
                            }

                            gem.gameObject.SetActive(false);

                            gem.Destroyed();
                        }
                    }
                    else if(gem.CurrentState != Gem.State.Disappearing)
                    {
                        LevelData.Instance.Matched(gem);
                        
                        foreach (var matchEffectPrefab in gem.MatchEffectPrefabs)
                        {
                            GameManager.Instance.PoolSystem.PlayInstanceAt(matchEffectPrefab, m_Grid.GetCellCenterWorld(gem.CurrentIndex));
                        }

                        gem.gameObject.SetActive(false);

                        gem.Destroyed();
                    }
                }

                if (match.MatchingGem.Count == 0)
                {
                    m_TickingMatch.RemoveAt(i);
                    i--;
                }
            }
        }

        public void DestroyGem(Vector3Int cell, bool forcedDeletion = false)
        {
            if(CellContent[cell].ContainingGem?.CurrentMatch != null)
                return;

            var match = new Match()
            {
                DeletionTimer = 0.0f,
                MatchingGem = new List<Vector3Int> { cell },
                OriginPoint = cell,
                SpawnedBonus = null,
                ForcedDeletion = forcedDeletion
            };

            CellContent[cell].ContainingGem.CurrentMatch = match;
        
            m_TickingMatch.Add(match);
        }

        public Vector3 GetCellCenter(Vector3Int cell)
        {
            return m_Grid.GetCellCenterWorld(cell);
        }

        public Vector3Int WorldToCell(Vector3 pos)
        {
            return m_Grid.WorldToCell(pos);
        }

        public void AddNewBoardAction(IBoardAction action)
        {
            m_BoardActions.Add(action);
        }

        //useful for bonus, this will create a new match and you can add anything you want to it.
        public Match CreateCustomMatch(Vector3Int newCell)
        {
            var newMatch = new Match()
            {
                DeletionTimer = 0.0f,
                MatchingGem = new(),
                OriginPoint = newCell,
                SpawnedBonus = null
            };
        
            m_TickingMatch.Add(newMatch);

            return newMatch;
        }

        void EmptyCheck()
        {
            if (m_FreezeMoveLock > 0)
                return;
            
            //go over empty cells
            for (int i = 0; i < m_EmptyCells.Count; ++i)
            {
                var emptyCell = m_EmptyCells[i];

                if (!CellContent[emptyCell].IsEmpty())
                {
                    m_EmptyCells.RemoveAt(i);
                    i--;
                    continue;
                }

                var aboveCellIdx = emptyCell + Vector3Int.up;
                bool aboveCellExist = CellContent.TryGetValue(aboveCellIdx, out var aboveCell);

                //if we have a gem above an empty cell, make that gem fall
                if (aboveCellExist && aboveCell.ContainingGem != null && aboveCell.CanFall)
                {
                    var incomingGem = aboveCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.0f;

                    //add that empty cell to be ticked so the gem goes down into it
                    m_NewTickingCells.Add(emptyCell);

                    //the above cell is now empty and this cell is not empty anymore
                    m_EmptyCells.Add(aboveCellIdx);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if ((!aboveCellExist || aboveCell.BlockFall) &&
                         CellContent.TryGetValue(aboveCellIdx + Vector3Int.right, out var aboveRightCell) &&
                         aboveRightCell.ContainingGem != null && aboveRightCell.CanFall)
                {
                    var incomingGem = aboveRightCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveRightCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.41421356237f;

                    //add that empty cell to be ticked so the gem goes down into it
                    m_NewTickingCells.Add(emptyCell);

                    //the above cell is now empty and this cell is not empty anymore
                    m_EmptyCells.Add(aboveCellIdx + Vector3Int.right);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if ((!aboveCellExist || aboveCell.BlockFall) &&
                         CellContent.TryGetValue(aboveCellIdx + Vector3Int.left, out var aboveLeftCell) &&
                         aboveLeftCell.ContainingGem != null && aboveLeftCell.CanFall)
                {
                    var incomingGem = aboveLeftCell.ContainingGem;
                    CellContent[emptyCell].IncomingGem = incomingGem;
                    aboveLeftCell.ContainingGem = null;

                    incomingGem.StartMoveTimer();
                    incomingGem.SpeedMultiplier = 1.41421356237f;

                    //add that empty cell to be ticked so the gem goes down into it
                    m_NewTickingCells.Add(emptyCell);

                    //the above cell is now empty and this cell is not empty anymore
                    m_EmptyCells.Add(aboveCellIdx + Vector3Int.left);
                    m_EmptyCells.Remove(emptyCell);
                }
                else if (SpawnerPosition.Contains(aboveCellIdx))
                {
                    //spawn a new gem
                    ActivateSpawnerAt(emptyCell);
                }
            }

            //empty cell are only handled once, sow e clear the list everytime it been checked.
            //m_EmptyCells.Clear();
        }

        void DoMatchCheck()
        {
            foreach (var cell in m_CellToMatchCheck)
            {
                DoCheck(cell);
            }

            m_CellToMatchCheck.Clear();
        }

        void DrawDebugCross(Vector3 center)
        {
            Debug.DrawLine(center + Vector3.left * 0.5f + Vector3.up * 0.5f,
                center + Vector3.right * 0.5f + Vector3.down * 0.5f);
            Debug.DrawLine(center + Vector3.left * 0.5f - Vector3.up * 0.5f,
                center + Vector3.right * 0.5f - Vector3.down * 0.5f);
        }

        //if gemPrefab is null, will pick a random gem from the existing one
        Gem NewGemAt(Vector3Int cell, Gem gemPrefab)
        {
            if (gemPrefab == null)
                gemPrefab = ExistingGems[Random.Range(0, ExistingGems.Length)];

            if (gemPrefab.MatchEffectPrefabs.Length != 0)
            {
                foreach (var matchEffectPrefab in gemPrefab.MatchEffectPrefabs)
                {
                    GameManager.Instance.PoolSystem.AddNewInstance(matchEffectPrefab, 16);
                }
            }

            //New Gem may be called after the board was init (as startup doesn't seem to be reliably called BEFORE init)
            if (CellContent[cell].ContainingGem != null)
            {
                Destroy(CellContent[cell].ContainingGem.gameObject);
            }

            var gem = Instantiate(gemPrefab, m_Grid.GetCellCenterWorld(cell), Quaternion.identity);
            CellContent[cell].ContainingGem = gem;
            gem.Init(cell);
        
            return gem;
        }

        void ActivateSpawnerAt(Vector3Int cell)
        {
            var gem = Instantiate(ExistingGems[Random.Range(0, ExistingGems.Length)], m_Grid.GetCellCenterWorld(cell + Vector3Int.up), Quaternion.identity);
            CellContent[cell].IncomingGem = gem;
        
            gem.StartMoveTimer();
            gem.SpeedMultiplier = 1.0f; 
            m_NewTickingCells.Add(cell);

            if (m_EmptyCells.Contains(cell)) m_EmptyCells.Remove(cell);
        }

        void TickSwap()
        {
            var gemToStart = CellContent[m_SwappingCells.Item1].IncomingGem;
            var gemToEnd = CellContent[m_SwappingCells.Item2].IncomingGem;

            var startPosition = m_Grid.GetCellCenterWorld(m_SwappingCells.Item1);
            var endPosition = m_Grid.GetCellCenterWorld(m_SwappingCells.Item2);

            gemToStart.transform.position =
                Vector3.MoveTowards(gemToStart.transform.position, startPosition, Time.deltaTime * m_VisualSettingReference.FallSpeed);
            gemToEnd.transform.position =
                Vector3.MoveTowards(gemToEnd.transform.position, endPosition, Time.deltaTime * m_VisualSettingReference.FallSpeed);

            if (gemToStart.transform.position == startPosition)
            {
                //swap if finished
                if (m_SwapStage == SwapStage.Forward)
                {
                    //temporaly unlock as we need to in order to delete the gems properly if they matched
                    CellContent[m_SwappingCells.Item1].Locked = false;
                    CellContent[m_SwappingCells.Item2].Locked = false;
                    
                    CellContent[m_SwappingCells.Item1].ContainingGem = CellContent[m_SwappingCells.Item1].IncomingGem;
                    CellContent[m_SwappingCells.Item2].ContainingGem = CellContent[m_SwappingCells.Item2].IncomingGem;
                
                    CellContent[m_SwappingCells.Item1].ContainingGem.MoveTo(m_SwappingCells.Item1);
                    CellContent[m_SwappingCells.Item2].ContainingGem.MoveTo(m_SwappingCells.Item2);

                    bool firstCheck = false;
                    bool secondCheck = false;

                    if (CellContent[m_SwappingCells.Item1].ContainingGem.Usable)
                    {
                        CellContent[m_SwappingCells.Item1].ContainingGem.Use(CellContent[m_SwappingCells.Item2].ContainingGem);
                        firstCheck = true;
                    }
                    else
                    {
                        firstCheck = DoCheck(m_SwappingCells.Item1);
                    }

                    if (CellContent[m_SwappingCells.Item2].ContainingGem.Usable)
                    {
                        CellContent[m_SwappingCells.Item2].ContainingGem.Use(CellContent[m_SwappingCells.Item1].ContainingGem);
                        secondCheck = true;
                    }
                    else
                    {
                        secondCheck =  DoCheck(m_SwappingCells.Item2);
                    }

                    if (firstCheck || secondCheck)
                    {
                        CellContent[m_SwappingCells.Item1].IncomingGem = null;
                        CellContent[m_SwappingCells.Item2].IncomingGem = null;

                        m_SwapStage = SwapStage.None;

                        // as swap was successful, we count down 1 move from the level
                        LevelData.Instance.Moved();
                    }
                    else
                    {
                        //if there is no match, we revert the swap
                        (CellContent[m_SwappingCells.Item1].IncomingGem, CellContent[m_SwappingCells.Item2].IncomingGem) = (
                            CellContent[m_SwappingCells.Item2].IncomingGem, CellContent[m_SwappingCells.Item1].IncomingGem);
                        (m_SwappingCells.Item1, m_SwappingCells.Item2) = (m_SwappingCells.Item2, m_SwappingCells.Item1);
                        m_SwapStage = SwapStage.Return;
                        
                        //relock the cells as they are swapping bacl
                        CellContent[m_SwappingCells.Item1].Locked = true;
                        CellContent[m_SwappingCells.Item2].Locked = true;
                    }
                }
                else
                {
                    CellContent[m_SwappingCells.Item1].ContainingGem = CellContent[m_SwappingCells.Item1].IncomingGem;
                    CellContent[m_SwappingCells.Item2].ContainingGem = CellContent[m_SwappingCells.Item2].IncomingGem;
                
                    CellContent[m_SwappingCells.Item1].ContainingGem.MoveTo(m_SwappingCells.Item1);
                    CellContent[m_SwappingCells.Item2].ContainingGem.MoveTo(m_SwappingCells.Item2);

                    CellContent[m_SwappingCells.Item1].IncomingGem = null;
                    CellContent[m_SwappingCells.Item2].IncomingGem = null;
                    
                    //they are not locked anymore and can resume falling/being deleted
                    CellContent[m_SwappingCells.Item1].Locked = false;
                    CellContent[m_SwappingCells.Item2].Locked = false;

                    m_SwapStage = SwapStage.None;
                }
            }
        }

        /// <summary>
        /// This will return true if a match was found. Setting createMatch to false allow to just check for existing match
        /// which is used by the match finder to check for match possible by swipe 
        /// </summary>
        bool DoCheck(Vector3Int startCell, bool createMatch = true)
        {
            // in the case we call this with an empty cell. Shouldn't happen, but let's be safe
            if (!CellContent.TryGetValue(startCell, out var centerGem) || centerGem.ContainingGem == null)
                return false;

            //we ignore that gem if it's already part of another match.
            if (centerGem.ContainingGem.CurrentMatch != null)
                return false;

            Vector3Int[] offsets = new[]
            {
                Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left
            };

            //First find all the connected gem of the same type
            List<Vector3Int> gemList = new List<Vector3Int>();
            List<Vector3Int> checkedCells = new();

            Queue<Vector3Int> toCheck = new();
            toCheck.Enqueue(startCell);

            while (toCheck.Count > 0)
            {
                var current = toCheck.Dequeue();

                gemList.Add(current);
                checkedCells.Add(current);

                foreach (var dir in offsets)
                {
                    var nextCell = current + dir;

                    if (checkedCells.Contains(nextCell))
                        continue;

                    if (CellContent.TryGetValue(current + dir, out var content)
                        && content.CanMatch()
                        && content.ContainingGem.CurrentMatch == null
                        && content.ContainingGem.GemType == centerGem.ContainingGem.GemType)
                    {
                        toCheck.Enqueue(nextCell);
                    }
                }
            }

            //we try to fit any bonus shapes in
            List<Vector3Int> temporaryShapeMatch = new();
            MatchShape matchedShape = null;
            List<BonusGem> matchedBonusGem = new();
            foreach (var bonusGem in GameManager.Instance.Settings.BonusSettings.Bonuses)
            {
                foreach (var shape in bonusGem.Shapes)
                {
                    if (shape.FitIn(gemList, ref temporaryShapeMatch))
                    {
                        if (matchedShape == null || matchedShape.Cells.Count < shape.Cells.Count)
                        {
                            matchedShape = shape;
                            //we have a new shape that have more gem, so we clear our existing list of bonus
                            matchedBonusGem.Clear();
                            matchedBonusGem.Add(bonusGem);
                        }
                        else if (matchedShape.Cells.Count == shape.Cells.Count)
                        {
                            //this new bonus have exactly the same number of the existing bonus, so become a new possible bonus
                            matchedBonusGem.Add(bonusGem);
                        }
                    }
                }
            }

            //-- now we build a list of all line of 3+ gems
            List<Vector3Int> lineList = new();

            foreach (var idx in gemList)
            {
                //for each dir (up/down/left/right) if there is no gem in that dir, that mean this could be the start of
                //a matching line, so we check in the opposite direction till we have no more gem
                foreach (var dir in offsets)
                {
                    if (!gemList.Contains(idx + dir))
                    {
                        var currentList = new List<Vector3Int>() { idx };
                        var next = idx - dir;
                        while (gemList.Contains(next))
                        {
                            currentList.Add(next);
                            next -= dir;
                        }

                        if (currentList.Count >= 3)
                        {
                            lineList = currentList;
                        }
                    }
                }
            }

            //no lines and no bonus match, so there is no match in that.
            if (lineList.Count == 0 && temporaryShapeMatch.Count == 0)
                return false;

            if (createMatch)
            {
                var finalMatch = CreateCustomMatch(startCell);
                finalMatch.SpawnedBonus = matchedBonusGem.Count == 0 ? null : matchedBonusGem[Random.Range(0, matchedBonusGem.Count)];

                foreach (var cell in lineList)
                {
                    if (m_MatchedCallback.TryGetValue(cell, out var clbk))
                        clbk.Invoke();

                    if(CellContent[cell].CanDelete())
                        finalMatch.AddGem(CellContent[cell].ContainingGem);
                }

                foreach (var cell in temporaryShapeMatch)
                {
                    if (m_MatchedCallback.TryGetValue(cell, out var clbk))
                        clbk.Invoke();
                    
                    if(CellContent[cell].CanDelete())
                        finalMatch.AddGem(CellContent[cell].ContainingGem);
                }

                UIHandler.Instance.TriggerCharacterAnimation(UIHandler.CharacterAnimation.Match);
            }

            return true;
        }

        void CheckInput()
        {
            if (!m_InputEnabled)
                return;
            
            var mainCam = Camera.main;
        
            var pressedThisFrame = GameManager.Instance.ClickAction.WasPressedThisFrame();
            var releasedThisFrame = GameManager.Instance.ClickAction.WasReleasedThisFrame();
        
            var clickPos = GameManager.Instance.ClickPosition.ReadValue<Vector2>();
            var worldPos = mainCam.ScreenToWorldPoint(clickPos);
            worldPos.z = 0;
            
            if (m_HoldTrailInstance.gameObject.activeSelf)
            {
                m_HoldTrailInstance.transform.position = worldPos;
            }
        
            if (pressedThisFrame)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //if the debug menu is open we instantiate the selected gem in the click cell
                if (UIHandler.Instance.DebugMenuOpen)
                {
                    if (UIHandler.Instance.SelectedDebugGem != null)
                    {
                        var clickedCell = m_Grid.WorldToCell(Camera.main.ScreenToWorldPoint(clickPos));
                        if (CellContent.TryGetValue(clickedCell, out var cellContent))
                        {
                            if (cellContent.ContainingGem != null)
                            {
                                Destroy(cellContent.ContainingGem.gameObject);
                            }

                            NewGemAt(clickedCell, UIHandler.Instance.SelectedDebugGem);
                        }
                    }
                
                    return;
                }
#endif
                //if we had an activated bonus, clicking somewhere will use it 
                if (m_ActivatedBonus != null)
                {
                    var clickedCell = m_Grid.WorldToCell(mainCam.ScreenToWorldPoint(clickPos));
                    if (CellContent.TryGetValue(clickedCell, out var content) && content.ContainingGem != null)
                    {
                        GameManager.Instance.UseBonusItem(m_ActivatedBonus, clickedCell);
                        m_ActivatedBonus = null;
                        return;
                    }
                }
            
                m_StartClickPosition = clickPos;
                
                var worldStart = mainCam.ScreenToWorldPoint(m_StartClickPosition);
                var startCell = m_Grid.WorldToCell(worldStart);

                if (CellContent.ContainsKey(startCell))
                {
                    if (m_GemHoldVFXInstance != null)
                    {
                        m_GemHoldVFXInstance.transform.position = m_Grid.GetCellCenterWorld(startCell);
                        m_GemHoldVFXInstance.gameObject.SetActive(true);
                    }

                    if (m_HoldTrailInstance)
                    {
                        m_HoldTrailInstance.transform.position = worldPos;
                        m_HoldTrailInstance.gameObject.SetActive(true);
                    }
                }
            }
            else if (releasedThisFrame)
            {
                //m_IsHoldingTouch = false;
                if(m_GemHoldVFXInstance != null) m_GemHoldVFXInstance.gameObject.SetActive(false);
                if(m_HoldTrailInstance != null) m_HoldTrailInstance.gameObject.SetActive(false);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (UIHandler.Instance.DebugMenuOpen)
                {
                    return;
                }
#endif
                //early exit if we already have a swipe queued or in progress, only one can happen at once
                if (m_SwipeQueued || m_SwapStage != SwapStage.None)
                    return;
                
                float clickDelta = Time.time - m_LastClickTime;
                m_LastClickTime = Time.time;

                var worldStart = mainCam.ScreenToWorldPoint(m_StartClickPosition);
                var startCell = m_Grid.WorldToCell(worldStart);
                startCell.z = 0;
            
                //if last than .3 second since last click, this is a double click, activate the gem if that is a gem.
                if (clickDelta < 0.3f)
                {
                    if (CellContent.TryGetValue(startCell, out var content) 
                        && content.ContainingGem != null 
                        && content.ContainingGem.Usable
                        && content.ContainingGem.CurrentMatch == null)
                    {
                        content.ContainingGem.Use(null);
                        return;
                    }
                }

                var endWorldPos = mainCam.ScreenToWorldPoint(clickPos);
            
                //we compute the swipe in world position as then a swipe of 1 is the distance between 2 cell
                var swipe = endWorldPos - worldStart;
                if (swipe.sqrMagnitude < 0.5f * 0.5f)
                {
                    return;
                }

                //the starting cell isn't a valid cell, so we exit
                if (!CellContent.TryGetValue(startCell, out var startCellContent) 
                    || !startCellContent.CanBeMoved)
                {
                    return;
                }

                var endCell = startCell;
            
                if (Mathf.Abs(swipe.x) > Mathf.Abs(swipe.y))
                {
                    if (swipe.x < 0)
                    {
                        endCell += Vector3Int.left;
                    }
                    else
                    {
                        endCell += Vector3Int.right;
                    }
                }
                else
                {
                    if (swipe.y > 0)
                    {
                        endCell += Vector3Int.up;
                    }
                    else
                    {
                        endCell += Vector3Int.down;
                    }
                }

                //the ending cell isn't a valid cell, exit
                if (!CellContent.TryGetValue(endCell, out var endCellContent) || !endCellContent.CanBeMoved)
                    return;
                
                //both work so we lock them so they cannot be deleted or moved until the swap end
                startCellContent.Locked = true;
                endCellContent.Locked = true;
                
                //we make sure to remove those cell from the ticking cell if they are in (we swipped as it was falling)

                m_SwipeQueued = true;
                m_StartSwipe = startCell;
                m_EndSwipe = endCell;
            }
        }

        public void ActivateBonusItem(BonusItem item)
        {
            m_ActivatedBonus = item;
        }

        void FindAllPossibleMatch()
        {
            //TODO : instead of going over every gems just do it on moved gems for optimization
        
            m_PossibleSwaps.Clear();
        
            //we use a double loop instead of directly querying the cells, so we access them in increasing x then y coordinate
            //this allow to just have to test swapping upward then right, as down and left will have been tested by previous
            //gem already

            for (int y = m_BoundsInt.yMin; y <= m_BoundsInt.yMax; ++y)
            {
                for (int x = m_BoundsInt.xMin; x <= m_BoundsInt.xMax; ++x)
                {
                    var idx = new Vector3Int(x, y, 0);
                    if (CellContent.TryGetValue(idx, out var cell) && cell.CanBeMoved)
                    {
                        var topIdx = idx + Vector3Int.up;
                        var rightIdx = idx + Vector3Int.right;
                    
                        if (CellContent.TryGetValue(topIdx, out var topCell) && topCell.CanBeMoved)
                        {
                            //swap the cell
                            (CellContent[idx].ContainingGem, CellContent[topIdx].ContainingGem) = (
                                CellContent[topIdx].ContainingGem, CellContent[idx].ContainingGem);
                        
                            if (DoCheck(topIdx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = idx,
                                    Direction = Vector3Int.up
                                });
                            }

                            if (DoCheck(idx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = topIdx,
                                    Direction = Vector3Int.down
                                });
                            }
                        
                            //swap back
                            (CellContent[idx].ContainingGem, CellContent[topIdx].ContainingGem) = (
                                CellContent[topIdx].ContainingGem, CellContent[idx].ContainingGem);
                        }
                    
                        if (CellContent.TryGetValue(rightIdx, out var rightCell) && rightCell.CanBeMoved)
                        {
                            //swap the cell
                            (CellContent[idx].ContainingGem, CellContent[rightIdx].ContainingGem) = (
                                CellContent[rightIdx].ContainingGem, CellContent[idx].ContainingGem);
                        
                            if (DoCheck(rightIdx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = idx,
                                    Direction = Vector3Int.right
                                });
                            }

                            if (DoCheck(idx, false))
                            {
                                m_PossibleSwaps.Add(new PossibleSwap()
                                {
                                    StartPosition = rightIdx,
                                    Direction = Vector3Int.left
                                });
                            }
                        
                            //swap back
                            (CellContent[idx].ContainingGem, CellContent[rightIdx].ContainingGem) = (
                                CellContent[rightIdx].ContainingGem, CellContent[idx].ContainingGem);
                        }
                    }
                }
            }


            m_PickedSwap = Random.Range(0, m_PossibleSwaps.Count);
        }
    }
}