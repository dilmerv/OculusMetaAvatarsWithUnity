using System;

using UnityEngine;

/**
 * @file OvrAvatarRenderable.cs
 */
namespace Oculus.Avatar2
{
    [RequireComponent(typeof(MeshFilter))]
    /**
     * @class OvrAvatarRenderable
     * Component that encapsulates the meshes of an avatar.
     * This component can only be added to game objects that
     * have a Unity Mesh and a Mesh filter.
     * 
     * Each OvrAvatarRenderable has one OvrAvatarPrimitive
     * that encapsulates the Unity Mesh and Material rendered.
     * Primitives may be shared across renderables but
     * renderables cannot be shared across avatars.
     * 
     * @see OvrAvatarPrimitive
     * @see ApplyMeshPrimitive
     */
    public class OvrAvatarRenderable : MonoBehaviour, IDisposable
    {
        public const string OVR_VERTEX_HAS_TANGENTS_KEYWORD = "OVR_VERTEX_HAS_TANGENTS";

        protected Mesh _mesh;
        protected MeshFilter _meshFilter;

        /// Designates whether this renderable is visible or not.
        public bool Visible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnVisibiltyChanged();
                }
            }
        }

        /// Designates whether this renderable is hidden or not.
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    _isHidden = value;
                    OnVisibiltyChanged();
                }
            }
        }

        /// Triangle and vertex counts for all levels of detail.
        public ref readonly OvrAvatarEntity.LodCostData CostData => ref _appliedPrimitive.CostData;

        /// Get which view(s) (first person, third person) this renderable applies to.
        /// These are established when the renderable is loaded.
        public CAPI.ovrAvatar2EntityViewFlags viewFlags => _appliedPrimitive.viewFlags;

        /// LOD bit flags for this renderable.
        /// These flags indicate which levels of detail this renderable is used by.
        public CAPI.ovrAvatar2EntityLODFlags lodFlags => _appliedPrimitive.lodFlags;

        /// Get which body parts of the avatar this renderable is used by.
        /// These are established when the renderable is loaded.
        public CAPI.ovrAvatar2EntityManifestationFlags manifestationFlags => _appliedPrimitive.manifestationFlags;


        /// True if this renderable has tangents for each vertex.
        protected bool HasTangents { get; private set; }

        private Material _materialCopy;

#pragma warning disable CA2213 // Disposable fields should be disposed - it is not owned by this class
        private OvrAvatarPrimitive _appliedPrimitive = null;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private bool _isVisible = false;
        private bool _isHidden = false;

        protected MeshFilter MyMeshFilter
        {
            get
            {
                if (_meshFilter == null)
                {
                    _meshFilter = GetComponent<MeshFilter>();
                }

                return _meshFilter;
            }
        }

        /// Get the Unity Renderer used to render this renderable.
        public Renderer rendererComponent { get; protected set; }

        protected virtual void AddDefaultRenderer()
        {
            AddRenderer<MeshRenderer>();
        }

        protected void CheckDefaultRenderer()
        {
            if (rendererComponent == null)
            {
                AddDefaultRenderer();
            }
        }

        protected virtual void Awake()
        {
            AddDefaultRenderer();
        }

        // TODO: This probably isn't a good pattern, too easy for subclasses to stomp on each other
        protected T AddRenderer<T>() where T : Renderer
        {
            var customRenderer = GetComponent<T>();
            if (!customRenderer)
            {
                customRenderer = gameObject.AddComponent<T>();
            }

            rendererComponent = customRenderer;

            return customRenderer;
        }

        protected virtual void OnDestroy()
        {
            MyMeshFilter.sharedMesh = null;
            _appliedPrimitive = null;

            Dispose();
        }

        protected virtual void OnVisibiltyChanged()
        {
            enabled = _isVisible && !_isHidden;
            rendererComponent.enabled = _isVisible && !_isHidden;
        }

        /**
         * Replaces the primitive with the Unity mesh and material.
         * Each renderable can reference a single primitive.
         * This primitive can be changed at run-time.
         * 
         * The *OVR_VERTEX_HAS_TANGENTS* shader keyword is set. 
         * based on whether this primitive has per-vertex tangents.
         */
        public virtual void ApplyMeshPrimitive(OvrAvatarPrimitive primitive)
        {
            OvrAvatarLog.Assert(_appliedPrimitive == null);

            CheckDefaultRenderer();

            _appliedPrimitive = primitive;

            _mesh = primitive.mesh;
            HasTangents = primitive.hasTangents;

            // TODO: Prefer sharedMesh/sharedMaterial
            MyMeshFilter.mesh = _mesh;  // NOTE: This does not duplicate the mesh currently. Perhaps we need to change a parameter on the .mesh for it to take place.
            rendererComponent.sharedMaterial = primitive.material;

            bool hasTangentsEnabled = rendererComponent.sharedMaterial.IsKeywordEnabled(OVR_VERTEX_HAS_TANGENTS_KEYWORD);

            // Check if has tangents keywords needs enabling or not but don't enable/disable
            // if material already has keyword enabled or not (save material copy at this point in time)
            if (HasTangents && !hasTangentsEnabled)
            {
                SetMaterialKeyword(OVR_VERTEX_HAS_TANGENTS_KEYWORD, true);
            }
            else if (!HasTangents && hasTangentsEnabled)
            {
                SetMaterialKeyword(OVR_VERTEX_HAS_TANGENTS_KEYWORD, false);
            }
        }

        protected void CopyMaterial()
        {
            if (_materialCopy == null)
            {
                _materialCopy = new Material(rendererComponent.sharedMaterial);
                rendererComponent.sharedMaterial = _materialCopy;
            }
        }

        /**
         * Sets the specified shader keyword for the material on this renderable.
         * @see SetShader
         * @see OvrAvatarMaterial
         */
        public void SetMaterialKeyword(string keyword, bool enable)
        {
            CopyMaterial();
            if (enable)
            {
                _materialCopy.EnableKeyword(keyword);
            }
            else
            {
                _materialCopy.DisableKeyword(keyword);
            }
        }

        /**
         * Sets the shader for the material on this renderable.
         * @see SetMaterialKeyword
         * @see OvrAvatarMaterial
         */
        public void SetShader(Shader shader)
        {
            CopyMaterial();
            _materialCopy.shader = shader;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (_materialCopy != null)
                {
                    Material.Destroy(_materialCopy);
                }
            }
        }
    }
}
