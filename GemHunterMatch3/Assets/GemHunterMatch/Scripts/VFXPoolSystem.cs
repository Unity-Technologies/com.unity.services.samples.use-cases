using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;
namespace Match3
{
    public class VFXPoolSystem
    {
        public class VFXInstance
        {
            public VisualEffect Instance;
            public int FrameCount;
        }

        private Dictionary<VisualEffect, Queue<VFXInstance>> m_Lookup = new();
        private List<VFXInstance> m_AllVFX = new();
        private int m_StartIndex;

        public void Clean()
        {
            m_AllVFX.Clear();
            m_Lookup.Clear();

            m_StartIndex = 0;
        }

        public void Update()
        {
            if (m_AllVFX.Count == 0)
                return;

            var endIdx = m_StartIndex + 16;
            while (endIdx >= m_AllVFX.Count) endIdx -= m_AllVFX.Count;

            while (m_StartIndex != endIdx)
            {
                var vfx = m_AllVFX[m_StartIndex];

                if (vfx.Instance.gameObject.activeInHierarchy)
                {
                    var frameDiff = Time.frameCount - vfx.FrameCount;
                    //particle count is updated every 60 frame, so we make sure we have enough frame since starting the vfx
                    //to check particle count. if no particles left, we disable the vfx. This is because as most VFX use
                    //GPU event they never get to sleep. This would break if we had vfx that have moment with no particle,
                    //but non of our vfx are like this in this project so this is good enough for us.
                    if (frameDiff > 100 && vfx.Instance.aliveParticleCount == 0)
                    {
                        vfx.Instance.gameObject.SetActive(false);
                    }
                }

                m_StartIndex++;
                while (m_StartIndex >= m_AllVFX.Count) m_StartIndex -= m_AllVFX.Count;
            }
        }

        public void AddNewInstance(VisualEffect prefab, int count)
        {
            if (m_Lookup.ContainsKey(prefab))
                return;

            var queue = new Queue<VFXInstance>(count);

            for (int i = 0; i < count; ++i)
            {
                var instance = Object.Instantiate(prefab);
                instance.gameObject.SetActive(false);

                var vfxInstance = new VFXInstance()
                {
                    Instance = instance,
                    FrameCount = Time.frameCount
                };

                queue.Enqueue(vfxInstance);
                m_AllVFX.Add(vfxInstance);
            }

            m_Lookup.Add(prefab, queue);
        }

        public VisualEffect PlayInstanceAt(VisualEffect prefab, Vector3 position)
        {
            var inst = GetInstance(prefab);

            if (inst == null)
                return null;

            inst.transform.position = position;
            inst.Stop();
            inst.Play();

            return inst;
        }

        //This both activate the gameobject and update the starting framecount for that instance so you need to call play the same frame!
        public VisualEffect GetInstance(VisualEffect prefab)
        {
            if (!m_Lookup.TryGetValue(prefab, out var vfxInstance))
            {
                Debug.LogError($"No VFX instantiated for prefab {prefab}");
                return null;
            }

            var inst = vfxInstance.Dequeue();
            vfxInstance.Enqueue(inst);

            inst.FrameCount = Time.frameCount;
            inst.Instance.gameObject.SetActive(true);

            return inst.Instance;
        }
    }
}
