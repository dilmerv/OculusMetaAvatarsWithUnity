using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Profiling;

namespace Oculus.Skinning.GpuSkinning
{

    public class OvrAvatarGpuSkinningController
    {
        private readonly HashSet<OvrGpuMorphTargetsCombiner> _combiners = new HashSet<OvrGpuMorphTargetsCombiner>();
        private readonly HashSet<IOvrGpuSkinner> _skinners = new HashSet<IOvrGpuSkinner>();

        private readonly List<OvrGpuMorphTargetsCombiner> _combinerList = new List<OvrGpuMorphTargetsCombiner>();
        private readonly List<IOvrGpuSkinner> _skinnerList = new List<IOvrGpuSkinner>();

        private readonly List<OvrGpuMorphTargetsCombiner> _activeCombinerList = new List<OvrGpuMorphTargetsCombiner>();
        private readonly List<IOvrGpuSkinner> _activeSkinnerList = new List<IOvrGpuSkinner>();


        internal void AddCombiner(OvrGpuMorphTargetsCombiner combiner)
        {
            Debug.Assert(combiner != null);
            if (_combiners.Add(combiner))
            {
                _combinerList.Add(combiner);
                combiner.parentController = this;
            }
        }

        internal void RemoveCombiner(OvrGpuMorphTargetsCombiner combiner)
        {
            Debug.Assert(combiner != null);
            if (_combiners.Remove(combiner))
            {
                _combinerList.Remove(combiner);
                combiner.parentController = null;
            }
        }

        internal void AddActiveCombiner(OvrGpuMorphTargetsCombiner combiner)
        {
            Debug.Assert(combiner != null);
            _activeCombinerList.Add(combiner);
        }

        internal void AddSkinner(IOvrGpuSkinner skinner)
        {
            Debug.Assert(skinner != null);
            if (_skinners.Add(skinner))
            {
                _skinnerList.Add(skinner);
                skinner.ParentController = this;
            }
        }

        internal void RemoveSkinner(IOvrGpuSkinner skinner)
        {
            Debug.Assert(skinner != null);
            if (_skinners.Remove(skinner))
            {
                _skinnerList.Remove(skinner);
                skinner.ParentController = null;
            }
        }

        internal void AddActiveSkinner(IOvrGpuSkinner skinner)
        {
            Debug.Assert(skinner != null);
            _activeSkinnerList.Add(skinner);
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        public void UpdateInternal()
        {
            Profiler.BeginSample("OvrAvatarGpuSkinningController::UpdateInternal");

            Profiler.BeginSample("OvrAvatarGpuSkinningController.CombinerCalls");
            foreach (var combiner in _activeCombinerList)
            {
                combiner.CombineMorphTargetWithCurrentWeights();
            }
            _activeCombinerList.Clear();
            Profiler.EndSample(); // "OvrAvatarGpuSkinningController.CombinerCalls"

            Profiler.BeginSample("OvrAvatarGpuSkinningController.SkinnerCalls");
            foreach (var skinner in _activeSkinnerList)
            {
                skinner.UpdateOutputTexture();
            }
            _activeSkinnerList.Clear();
            Profiler.EndSample(); // "OvrAvatarGpuSkinningController.SkinnerCalls"


            Profiler.EndSample();
        }
    }
}
