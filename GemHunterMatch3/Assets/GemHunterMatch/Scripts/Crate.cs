using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Match3
{
    public class Crate : Gem
    {
        public bool CanBeDestroyedWithAdjacentMatch = true;
        public Sprite[] HealthStates;
        public int Health = 3;

        public AudioClip DamagedClip;
        public VisualEffect DamageEffect;
    
        protected SpriteRenderer m_Renderer;

        private void Awake()
        {
            m_CanMove = false;
            m_Renderer = GetComponent<SpriteRenderer>();
            m_HitPoints = Health;
        }

        public override void Init(Vector3Int startIdx)
        {
            base.Init(startIdx);
            
            if(DamageEffect != null)
                GameManager.Instance.PoolSystem.AddNewInstance(DamageEffect, 6);

            if (CanBeDestroyedWithAdjacentMatch)
            {
                foreach (var neighbour in BoardCell.Neighbours)
                {
                    var adjacentCell = startIdx + neighbour;
                    Board.RegisterDeletedCallback(adjacentCell, AdjacentMatch);
                }
            }
        }

        public override bool Damage(int damage)
        {
            GameManager.Instance.PlaySFX(DamagedClip);
            
            if(DamageEffect != null)
                GameManager.Instance.PoolSystem.PlayInstanceAt(DamageEffect, transform.position);
            
            var ret = base.Damage(damage);
            UpdateState();
            return ret;
        }

        private void OnDestroy()
        {
            if (CanBeDestroyedWithAdjacentMatch)
            {
                foreach (var neighbour in BoardCell.Neighbours)
                {
                    var adjacentCell = m_CurrentIndex + neighbour;
                    Board.UnregisterDeletedCallback(adjacentCell, AdjacentMatch);
                }
            }
        }

        void AdjacentMatch()
        {
            if (!Damage(1))
            {
                GameManager.Instance.Board.DestroyGem(m_CurrentIndex);
            }
        }

        void UpdateState()
        {
            float ratio = m_HitPoints / (float)Health;
            int state = Mathf.RoundToInt((1.0f - ratio) * HealthStates.Length);
        
            if(state >= 0 && state < HealthStates.Length)
                m_Renderer.sprite = HealthStates[state];
        }
    }
}