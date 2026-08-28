using UnityEngine;

namespace SpiderSwing.Gameplay
{
    /// <summary>
    /// Owns the two visible materials that make up a player skin. The demo uses
    /// the authored Arm and Body renderers directly; it does not instantiate or
    /// duplicate materials at runtime.
    /// </summary>
    public sealed class PlayerSkinVisual : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Renderer armRenderer;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Material defaultArmMaterial;
        [SerializeField] private Material defaultBodyMaterial;

        public Transform ModelRoot => modelRoot != null ? modelRoot : transform;

        public void Configure(Transform configuredModelRoot)
        {
            modelRoot = configuredModelRoot != null ? configuredModelRoot : transform;
            ResolveReferences();
            CaptureDefaultsIfNeeded();
        }

        public void Apply(Material armMaterial, Material bodyMaterial)
        {
            ResolveReferences();
            ApplyToHierarchy(ModelRoot, armMaterial, bodyMaterial);
        }

        public void ResetToDefault()
        {
            Apply(defaultArmMaterial, defaultBodyMaterial);
        }

        public bool TryGetCurrentMaterials(out Material armMaterial, out Material bodyMaterial)
        {
            ResolveReferences();
            armMaterial = armRenderer != null ? armRenderer.sharedMaterial : null;
            bodyMaterial = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            return armMaterial != null && bodyMaterial != null;
        }

        public static bool TryGetMaterials(Transform root, out Material armMaterial, out Material bodyMaterial)
        {
            var arm = FindRenderer(root, "Arm");
            var body = FindRenderer(root, "Body");
            armMaterial = arm != null ? arm.sharedMaterial : null;
            bodyMaterial = body != null ? body.sharedMaterial : null;
            return armMaterial != null && bodyMaterial != null;
        }

        public static void ApplyToHierarchy(Transform root, Material armMaterial, Material bodyMaterial)
        {
            if (root == null)
            {
                return;
            }

            var arm = FindRenderer(root, "Arm");
            var body = FindRenderer(root, "Body");
            if (arm != null && armMaterial != null)
            {
                ReplaceRendererMaterials(arm, armMaterial);
            }

            if (body != null && bodyMaterial != null)
            {
                ReplaceRendererMaterials(body, bodyMaterial);
            }
        }

        private static void ReplaceRendererMaterials(Renderer renderer, Material material)
        {
            // A MaterialPropertyBlock can carry a colour override from an old
            // visual path. Clear it first so the assigned material renders with
            // its own authored properties, not an inherited tint.
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
                return;
            }

            for (var index = 0; index < materials.Length; index++)
            {
                materials[index] = material;
                renderer.SetPropertyBlock(null, index);
            }

            renderer.sharedMaterials = materials;
            renderer.SetPropertyBlock(null);
        }

        public static Renderer FindRenderer(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                var directRenderer = root.GetComponent<Renderer>();
                if (directRenderer != null)
                {
                    return directRenderer;
                }
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindRenderer(root.GetChild(index), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureDefaultsIfNeeded();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (modelRoot == null)
            {
                modelRoot = FindChild(transform, "base_rig") ?? transform;
            }

            armRenderer ??= FindRenderer(modelRoot, "Arm");
            bodyRenderer ??= FindRenderer(modelRoot, "Body");
        }

        private void CaptureDefaultsIfNeeded()
        {
            if (defaultArmMaterial == null && armRenderer != null)
            {
                defaultArmMaterial = armRenderer.sharedMaterial;
            }

            if (defaultBodyMaterial == null && bodyRenderer != null)
            {
                defaultBodyMaterial = bodyRenderer.sharedMaterial;
            }
        }

        private static Transform FindChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
