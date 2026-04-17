using UnityEngine;
using UnityEngine.VFX;

namespace Match3
{
    /// <summary>
    /// A Gem is the base class of all thing that live inside cell on the board. It can be subclassed to specialize them (see
    /// BonusGem and WoodenCrate).
    /// This class contains a bunch of cached data mainly used for visual effect on the gem (movement, bounce etc..)
    /// </summary>
    public class Gem : MonoBehaviour
    {
        public enum State
        {
            Still,
            Falling,
            Bouncing,
            Disappearing
        }

        public int GemType;

        public VisualEffect[] MatchEffectPrefabs;
        public Sprite UISprite;
        
        //When a gem get added to a match, this match get stored here so we can now if this gem is currently in a match and 
        //cannot be used for anything else.
        public Match CurrentMatch = null;
        //this is set to sqrt(2) when falling in diagonal so the time of a diagonal fall is the same as a direct one
        [HideInInspector]
        public float SpeedMultiplier = 1.0f;
        public bool CanMove => m_CanMove;
        public Vector3Int CurrentIndex => m_CurrentIndex;
        public bool Usable => m_Usable;
        public bool Used => m_Used;
        public float FallTime => m_FallTime;
        public State CurrentState => m_CurrentState;
        public int HitPoint => m_HitPoints;
    
        //If this is set to true, the Use function will be called when swapping or double clicking the gem.
        //Not used in this base class, but useful for BonusGem.
        protected bool m_Usable = false;
        protected bool m_Used = false;
        protected bool m_CanMove = true;
        protected Vector3Int m_CurrentIndex;
    
        protected int m_HitPoints = 1;
    
        private float m_FallTime = 0.0f;

        private State m_CurrentState = State.Still;

        public virtual void Init(Vector3Int startIdx)
        {
            m_CurrentIndex = startIdx;
        }

        // Called when swapping a Gem that have its Usable set to true. SwappedGem will contains the other gem it was swiped
        // with or null if that was a use triggered by a double click
        //deleteSelf will be true in most case but is set to false when a bonus item is used. Bonus item just call Use on
        //a "temporary" gem it hold, and that gem should not be deleted
        public virtual void Use(Gem swappedGem, bool isBonus = false)
        {
        
        }
    
        public virtual bool Damage(int damage)
        {
            m_HitPoints -= damage;
            return m_HitPoints > 0;
        }

        public void MoveTo(Vector3Int newCell)
        {
            m_CurrentIndex = newCell;
        }

        public void StartMoveTimer()
        { 
            m_FallTime = 0;

            m_CurrentState = State.Falling;
        }

        public void TickMoveTimer(float deltaTime)
        {
            m_FallTime += deltaTime;
        }

        public void StopFalling()
        {
            m_CurrentState = State.Bouncing;
            m_FallTime = 0;
        }

        public void StopBouncing()
        {
            m_CurrentState = State.Still;
        }

        public void Destroyed()
        {
            m_CurrentState = State.Disappearing;
        }
    }
}