using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

namespace Match3
{
    /// <summary>
    /// Bonus that will delete all gems of a given type in the board.
    /// </summary>
    public class ColorClean : BonusGem
    {
        public VisualEffect UseEffect;
        public AudioClip TriggerSound;
        
        private Texture2D m_PositionMap;
        
        public override void Awake()
        {
            m_Usable = true;
            
            GameManager.Instance.PoolSystem.AddNewInstance(UseEffect, 2);
            m_PositionMap = new Texture2D(64, 1, TextureFormat.RGBAFloat, false);
        }

        public override void Use(Gem swappedGem, bool isBonus = true)
        {
            //this allow to stop recursion on some bonus (like bomb trying to explode themselve again and again)
            //if isBonus is true, this is not a gem on the board so no risk of recursion we can ignore this
            if (!isBonus && m_Used)
                return;

            m_Used = true;
            
            int type = -1;
        
            //first find which type to delete. If we swapped a gem, this is this gem type
            if (swappedGem != null)
                type = swappedGem.GemType;

            if (type < 0)
            {//we either swapped with another bonus or we double clicked, so that bonus will clear the gem with the most type
                Dictionary<int, int> typeCount = new();

                foreach (var (cell, content) in GameManager.Instance.Board.CellContent)
                {
                    if (content.ContainingGem != null)
                    {
                        if (typeCount.ContainsKey(content.ContainingGem.GemType))
                            typeCount[content.ContainingGem.GemType] += 1;
                        else
                            typeCount[content.ContainingGem.GemType] = 1;
                    }
                }

                int highestCount = 0;
                int highestType = 0;
                foreach (var (gemType, count) in typeCount)
                {
                    if (count > highestCount)
                    {
                        highestCount = count;
                        highestType = gemType;
                    }
                }

                type = highestType;
            }

            Color[] infoColor = new Color[64];
            int currentColor = 0;

            //we create a new match in the board, set its type to force deletion (as this match came from a bonus, not a swap)
            var newMatch = GameManager.Instance.Board.CreateCustomMatch(m_CurrentIndex);
            newMatch.ForcedDeletion = true;
            //we grab from the cell and not use "this" because when used as a Bonus Item, the item at this index won't be the gem
            HandleContent(GameManager.Instance.Board.CellContent[m_CurrentIndex], newMatch);

            foreach (var (cell, content) in GameManager.Instance.Board.CellContent)
            {
                
                if (content.ContainingGem?.GemType == type)
                {
                    if (content.Obstacle != null)
                    {
                        content.Obstacle.Damage(1);
                    }
                    else if(content.ContainingGem.CurrentMatch == null)
                    {
                        HandleContent(content, newMatch);
                        var pos = content.ContainingGem.transform.position;
                        infoColor[currentColor] = new Color(pos.x, pos.y, pos.z);
                        currentColor++;
                    }
                }
            }
            
            m_PositionMap.filterMode = FilterMode.Point;
            m_PositionMap.wrapMode = TextureWrapMode.Repeat;
            m_PositionMap.SetPixels(infoColor, 0);
            m_PositionMap.Apply();

            var vfxInst = GameManager.Instance.PoolSystem.GetInstance(UseEffect);
            
            vfxInst.Stop();
            vfxInst.SetTexture(Shader.PropertyToID("PosMap"), m_PositionMap);
            vfxInst.SetInt(Shader.PropertyToID("PosCount"), currentColor);

            vfxInst.transform.position = GameManager.Instance.Board.GetCellCenter(m_CurrentIndex);
            vfxInst.Play();
            
            GameManager.Instance.PlaySFX(TriggerSound);
        }
    }
}