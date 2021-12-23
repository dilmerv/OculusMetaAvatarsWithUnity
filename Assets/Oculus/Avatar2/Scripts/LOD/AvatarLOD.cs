using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace Oculus.Avatar2
{
    public class AvatarLOD : MonoBehaviour
    {
        private bool prevDebug_;

        public bool culled { get; private set; }

        public bool overrideLOD = false;
        private bool prevOverrideLOD_ = false;

        public int overrideLevel
        {
            get { return Mathf.Clamp(overrideLevel_, -1, maxLodLevel); }
            set { overrideLevel_ = value; }
        }

        private int overrideLevel_ = 0;
        private int prevOverrideLevel_ = 0;

        public Transform centerXform;
        public readonly List<Transform> extraXforms = new List<Transform>();
        private List<AvatarLODGroup> lodGroups_ = new List<AvatarLODGroup>();
        public readonly List<int> vertexCounts = new List<int>();
        public readonly List<int> triangleCounts = new List<int>();

        private int _minLodLevel = -1;
        public int minLodLevel => _minLodLevel;
        private int _maxLodLevel = -1;
        public int maxLodLevel => _maxLodLevel;

        public float distance;
        public float screenPercent;
        public int wantedLevel;
        public int dynamicLevel;
        public float lodImportance;
        public float updateImportance;
        public float lastUpdateAgeTicks; // clock time since last update, ticks
        public float previousUpdateAgeWindowTicks; // total max age during previous two updates, ticks
        public float lastUpdateAgeSeconds; // clock time since last update, seconds
        public float previousUpdateAgeWindowSeconds; // total max age during previous two updates, seconds
        public int lastUpdateCost = -1;
        public OvrAvatarEntity.StreamLOD dynamicStreamLod;

        public Action<bool> CulledChangedEvent; // events are also available from the AvatarLODManager but this is more efficient for specific AvatarLOD component actions

        private GameObject debugCanvas_;

        private int level_;
        private int prevLevel_;
        private bool forceDisabled_;

        public int Level
        {
            get { return level_; }
            set
            {
                if (value == prevLevel_)
                {
                    return;
                }
                level_ = value;

                if (!overrideLOD)
                {
                    UpdateLOD();
                    UpdateDebugLabel();
                }

                prevLevel_ = level_;
            }
        }

        public void SetForceDisable(bool tf)
        {
            forceDisabled_ = tf;
        }

        private void Start()
        {
            AvatarLODManager.Instance.AddLOD(this);
            if (centerXform == null)
            {
                centerXform = transform;
            }
        }

        // Returns true upon a state transition
        public bool SetCulled(bool nextCulled)
        {
            if (nextCulled != culled)
            {
                culled = nextCulled;
                CulledChangedEvent?.Invoke(culled);
                return true;
            }
            return false;
        }

        private bool HasValidLODParent()
        {
            bool found = false;
            var lodParents = gameObject.transform.parent.GetComponents<AvatarLODParent>();
            foreach (AvatarLODParent lodParent in lodParents)
            {
                found |= !lodParent.beingDestroyed;
            }

            return found;
        }

        private void DestroyLODParentIfOnlyChild()
        {
            if (gameObject.transform.parent)
            {
                var lodParents = gameObject.transform.parent.gameObject.GetComponents<AvatarLODParent>();
                foreach (AvatarLODParent lodParent in lodParents)
                {
                    lodParent.DestroyIfOnlyLODChild(this);
                }
            }
        }


        private void OnBeforeTransformParentChanged()
        {
            DestroyLODParentIfOnlyChild();
        }

        private void OnTransformParentChanged()
        {
            Transform t = gameObject.transform.parent;
            if (t != null && !HasValidLODParent())
            {
                t.gameObject.AddComponent<AvatarLODParent>();
            }

            AvatarLODManager.ParentStateChanged(this);
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        public void UpdateOverride()
        {
            if (!isActiveAndEnabled || forceDisabled_)
            {
                return;
            }

            Profiler.BeginSample("AvatarLOD::UpdateOverride");

            bool needsDebugLabelUpdate = false;
            if (overrideLevel != prevOverrideLevel_)
            {
                if (overrideLOD)
                {
                    UpdateLOD();
                    needsDebugLabelUpdate = true;
                }
                prevOverrideLevel_ = overrideLevel;
            }

            if (overrideLOD != prevOverrideLOD_)
            {
                UpdateLOD();
                needsDebugLabelUpdate = true;
                prevOverrideLOD_ = overrideLOD;
            }

            bool debug = AvatarLODManager.Instance.debug.displayLODLabels || AvatarLODManager.Instance.debug.displayAgeLabels;
            if (debug || debug != prevDebug_)
            {
                needsDebugLabelUpdate = true;
                prevDebug_ = debug;
            }

            if (needsDebugLabelUpdate)
            {
                UpdateDebugLabel();
            }

            Profiler.EndSample();
        }

        void OnDestroy()
        {
            DestroyLODParentIfOnlyChild();
            AvatarLODManager.RemoveLOD(this);
        }

        public void UpdateDebugLabel()
        {
            if (AvatarLODManager.Instance.debug.displayLODLabels || AvatarLODManager.Instance.debug.displayAgeLabels)
            {
                if (debugCanvas_ == null && AvatarLODManager.Instance.avatarLodDebugCanvas != null)
                {
                    GameObject canvasPrefab = AvatarLODManager.Instance.avatarLodDebugCanvas; //LoadAssetWithFullPath<GameObject>($"{AvatarPaths.ASSET_SOURCE_PATH}/LOD/Prefabs/AVATAR_LOD_DEBUG_CANVAS.prefab");
                    if (canvasPrefab != null)
                    {
                        debugCanvas_ = Instantiate(canvasPrefab, centerXform);

                        // Set position instead of localPosition to keep the label in a steady readable location.
                        debugCanvas_.transform.position = debugCanvas_.transform.parent.position + AvatarLODManager.Instance.debug.displayLODLabelOffset;

                        debugCanvas_.SetActive(true);
                    }
                    else
                    {
                        OvrAvatarLog.LogWarning("DebugLOD will require the avatarLodDebugCanvas prefab to be specified. This has a simple UI card that allows for world space display of LOD.");
                    }
                }

                if (debugCanvas_ != null)
                {
                    var text = debugCanvas_.GetComponentInChildren<UnityEngine.UI.Text>();

                    // Set position instead of localPosition to keep the label in a steady readable location.
                    debugCanvas_.transform.position = debugCanvas_.transform.parent.position + AvatarLODManager.Instance.debug.displayLODLabelOffset;

                    if (AvatarLODManager.Instance.debug.displayLODLabels)
                    {
                        int actualLevel = overrideLOD ? overrideLevel : Level;
                        text.color = actualLevel == -1 ? Color.gray : AvatarLODManager.LOD_COLORS[actualLevel];
                        text.text = actualLevel.ToString();
                        text.fontSize = 40;
                    }

                    if (AvatarLODManager.Instance.debug.displayAgeLabels)
                    {
                        text.text = previousUpdateAgeWindowSeconds.ToString();
                        text.color = new Color(Math.Max(Math.Min(-1.0f + 2.0f * previousUpdateAgeWindowSeconds, 1.0f), 0.0f), Math.Max(Math.Min(previousUpdateAgeWindowSeconds * 2.0f, 2.0f - 2.0f * previousUpdateAgeWindowSeconds), 0f), Math.Max(Math.Min(1.0f - 2.0f * previousUpdateAgeWindowSeconds, 1.0f), 0.0f));
                        text.fontSize = 10;
                    }
                }
            }
            else
            {
                if (debugCanvas_ != null)
                {
                    debugCanvas_.SetActive(false);
                    debugCanvas_ = null;
                }
            }
        }

        public void UpdateLOD()
        {
            if (forceDisabled_)
                return;
            for (int i = 0; i < lodGroups_.Count; i++)
            {
                lodGroups_[i].Level = overrideLOD ? overrideLevel : Level;
            }
        }

        public void ForceUpdateLOD<T>()
        {
            for (int i = 0; i < lodGroups_.Count; i++)
            {
                if (lodGroups_[i] is T)
                    lodGroups_[i].UpdateLODGroup();
            }
        }

        public void ForceUpdateLOD()
        {
            for (int i = 0; i < lodGroups_.Count; i++)
            {
                lodGroups_[i].UpdateLODGroup();
            }
        }

        public void ResetLODGroups()
        {
            for (int i = 0; i < lodGroups_.Count; i++)
            {
                lodGroups_[i].ResetLODGroup();
            }
        }

        public void SetLODGroups(List<AvatarLODGroup> groups)
        {
            lodGroups_ = groups;
            for (int i = 0; i < lodGroups_.Count; i++)
            {
                lodGroups_[i].parentLOD = this;
            }
        }

        public void AddLODGroup(AvatarLODGroup group)
        {
            lodGroups_.Add(group);
            group.parentLOD = this;
            group.Level = overrideLOD ? overrideLevel : Level;
        }

        public void RemoveLODGroup(AvatarLODGroup group)
        {
            lodGroups_.Remove(group);
        }

        public void ClearLODGameObjects()
        {
            // Vertex counts will be reset by this function.
            vertexCounts.Clear();
            triangleCounts.Clear();

            _minLodLevel = -1;
            _maxLodLevel = -1;
        }

        public void AddLODGameObjectGroupByAvatarSkinnedMeshRenderers(GameObject parentGameObject, Dictionary<string, List<GameObject>> suffixToObj)
        {
            foreach (var kvp in suffixToObj)
            {
                AvatarLODGameObjectGroup gameObjectGroup = parentGameObject.GetOrAddComponent<AvatarLODGameObjectGroup>();
                gameObjectGroup.GameObjects = kvp.Value.ToArray();
                AddLODGroup(gameObjectGroup);
            }
        }

        public void AddLODGameObjectGroupBySdkRenderers(Dictionary<int, Oculus.Avatar2.OvrAvatarEntity.LodData> lodObjects)
        {
            // Vertex counts will be reset by this function.
            vertexCounts.Clear();
            triangleCounts.Clear();

            // first see what the limits could be...
            _minLodLevel = lodObjects.First().Key;
            _maxLodLevel = minLodLevel;
            foreach (var entry in lodObjects)
            {
                if (_minLodLevel > entry.Key) _minLodLevel = entry.Key;
                if (_maxLodLevel < entry.Key) _maxLodLevel = entry.Key;
            }

            // first find common parent and children;
            GameObject commonParent = null;
            GameObject[] children = new GameObject[maxLodLevel + 1];
            if (maxLodLevel >= vertexCounts.Count || maxLodLevel >= triangleCounts.Count)
            {
                vertexCounts.Capacity = maxLodLevel;
                triangleCounts.Capacity = maxLodLevel;
                while (maxLodLevel >= vertexCounts.Count)
                {
                    vertexCounts.Add(0);
                }
                while (maxLodLevel >= triangleCounts.Count)
                {
                    triangleCounts.Add(0);
                }
            }

            for (int i = minLodLevel; i < maxLodLevel + 1; i++)
            {
                if (lodObjects.TryGetValue(i, out var lodData))
                {
                    var go = lodData.gameObject;
                    vertexCounts[i] += lodData.vertexCount;
                    triangleCounts[i] += lodData.triangleCount;
                    children[i] = go;
                    var localParent = go.transform.parent.gameObject;
                    if (commonParent == null)
                    {
                        commonParent = localParent;
                    }
                    else if (commonParent != localParent)
                    {
                        OvrAvatarLog.LogError("Expected all lodObjects to have the same parent object.");
                    }
                }
            }
            AvatarLODGameObjectGroup gameObjectGroup = commonParent.GetOrAddComponent<AvatarLODGameObjectGroup>();
            gameObjectGroup.GameObjects = children;
            AddLODGroup(gameObjectGroup);
        }

        public AvatarLODBehaviourStateGroup AddLODBehaviourStateGroup(
          GameObject go,
          Behaviour[] behaviours,
          bool[] lodStates)
        {
            AvatarLODBehaviourStateGroup behaviourLODGroup = go.GetOrAddComponent<AvatarLODBehaviourStateGroup>();
            behaviourLODGroup.LodBehaviours = new List<Behaviour>(behaviours);
            behaviourLODGroup.EnabledStates = new List<bool>(lodStates);
            AddLODGroup(behaviourLODGroup);
            return behaviourLODGroup;
        }

        public AvatarLODBehaviourStateGroup AddLODBehaviourStateGroup(
          Behaviour behaviour,
          bool[] lodStates)
        {
            return AddLODBehaviourStateGroup(behaviour.gameObject, new Behaviour[] { behaviour }, lodStates);
        }

        public AvatarLODBehaviourStateGroup AddLODBehaviourStateGroup(GameObject go, Type[] behaviourTypes,
          bool[] lodStates)
        {
            var behaviours = new List<Behaviour>();

            foreach (var type in behaviourTypes)
            {
                Component[] cmpts = go.GetComponentsInChildren(type, true);
                foreach (var component in cmpts)
                {
                    behaviours.Add((Behaviour)component);
                }
            }

            return behaviours.Count > 0 ? AddLODBehaviourStateGroup(go, behaviours.ToArray(), lodStates) : null;
        }

        public AvatarLODActionGroup AddLODActionGroup(GameObject go, Action[] actions)
        {
            AvatarLODActionGroup actionLODGroup = go.GetOrAddComponent<AvatarLODActionGroup>();
            actionLODGroup.Actions = new List<Action>(actions);
            AddLODGroup(actionLODGroup);
            return actionLODGroup;
        }

        public AvatarLODActionGroup AddLODActionGroup(GameObject go, Action action, int levels)
        {
            var actions = new Action[levels];
            for (int i = 0; i < levels; i++)
            {
                actions[i] = action;
            }

            return AddLODActionGroup(go, actions);
        }

        // Find a valid LOD near the requested one
        public int CalcAdjustedLod(int lod)
        {
            var adjustedLod = Math.Min(Math.Max(lod, minLodLevel), maxLodLevel);
            if (adjustedLod != -1 && vertexCounts[adjustedLod] == 0)
            {
                adjustedLod = GetNextLod(lod);
                if (adjustedLod == -1)
                {
                    adjustedLod = GetPreviousLod(lod);
                }
            }
            return adjustedLod;
        }

        public int GetNextLod(int lod)
        {
            if (maxLodLevel >= 0)
            {
                for (int nextLod = lod + 1; nextLod <= maxLodLevel; ++nextLod)
                {
                    if (vertexCounts[nextLod] != 0)
                    {
                        return nextLod;
                    }
                }
            }
            return -1;
        }

        public int GetPreviousLod(int lod)
        {
            if (minLodLevel >= 0)
            {
                for (int prevLod = lod - 1; prevLod >= minLodLevel; --prevLod)
                {
                    if (vertexCounts[prevLod] != 0)
                    {
                        return prevLod;
                    }
                }
            }
            return -1;
        }

        public void Reset()
        {
            centerXform = transform;
            extraXforms.Clear();
        }
    }
}
