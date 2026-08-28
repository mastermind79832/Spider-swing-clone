using TMPro;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class UpgradePad : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [Header("Upgrade")]
        [SerializeField] private string upgradeId = "Upgrade01";
        [SerializeField, Min(0)] private int cost = 5;
        [SerializeField, Min(1f)] private float xpMultiplier = 2f;
        [SerializeField, Min(0)] private int extraSwings = 3;
        [SerializeField] private Color labelColor = Color.white;

        [Header("Prefab references")]
        [SerializeField] private Renderer floorRenderer;
        [SerializeField] private Transform playerPreviewRoot;
        [SerializeField] private TMP_Text valueText;

        private PlayerDemoRewards demoRewards;
        private bool purchased;
        private MaterialPropertyBlock propertyBlock;

        public string UpgradeId => upgradeId;
        public int Cost => cost;
        public float XpMultiplier => xpMultiplier;
        public int ExtraSwings => extraSwings;
        public bool IsPurchased => purchased;

        public void Configure(
            string configuredId,
            int configuredCost,
            float configuredXpMultiplier,
            int configuredExtraSwings,
            Color configuredLabelColor)
        {
            upgradeId = string.IsNullOrWhiteSpace(configuredId) ? "Upgrade" : configuredId;
            cost = Mathf.Max(0, configuredCost);
            xpMultiplier = Mathf.Max(1f, configuredXpMultiplier);
            extraSwings = Mathf.Max(0, configuredExtraSwings);
            labelColor = configuredLabelColor;
            ResolveReferences();
            RefreshVisuals();
        }

        public bool TryGetSkinMaterials(out Material armMaterial, out Material bodyMaterial)
        {
            ResolveReferences();
            if (PlayerSkinVisual.TryGetMaterials(playerPreviewRoot, out armMaterial, out bodyMaterial))
            {
                return true;
            }

            // Imported model hierarchies can put a renderer one level below the
            // visible Arm/body transform. Prefer any resolved Arm or Body
            // material, then mirror it to the missing half. Most demo skins use
            // one shared material for both pieces, so this stays visually correct.
            armMaterial ??= FindNamedMaterial(playerPreviewRoot, "Arm");
            bodyMaterial ??= FindNamedMaterial(playerPreviewRoot, "Body");
            bodyMaterial ??= armMaterial;
            armMaterial ??= bodyMaterial;
            if (armMaterial != null && bodyMaterial != null)
            {
                return true;
            }

            // Compatibility only: existing pads retain the old inactive
            // placeholder while the user transitions to the preview model.
            // It is never preferred when an authored preview is available.
            var legacyMaterial = FindChild(transform, "Player skin")?.GetComponent<Renderer>()?.sharedMaterial;
            armMaterial = legacyMaterial;
            bodyMaterial = legacyMaterial;
            return legacyMaterial != null;
        }

        public void MarkPurchased()
        {
            purchased = true;
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            ResolveReferences();
            if (floorRenderer != null)
            {
                var floorColor = purchased
                    ? new Color(0.15f, 0.9f, 0.25f)
                    : CanAfford()
                        ? new Color(1f, 0.85f, 0.05f)
                        : new Color(0.9f, 0.15f, 0.12f);
                SetRendererColor(floorRenderer, floorColor);
            }

            if (valueText != null)
            {
                valueText.text = $"{cost} points\nx{xpMultiplier:0.#} xp\n+{extraSwings} swings";
                valueText.color = labelColor;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (demoRewards != null)
            {
                demoRewards.OnPointsChanged += HandlePointsChanged;
            }

            RefreshVisuals();
        }

        private void OnDisable()
        {
            if (demoRewards != null)
            {
                demoRewards.OnPointsChanged -= HandlePointsChanged;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<LocalPlayerController>()
                ?? other.GetComponentInParent<LocalPlayerController>();
            if (player == null || player.IsDead)
            {
                return;
            }

            player.GetComponent<PlayerUpgradeState>()?.TryPurchase(this);
        }

        private bool CanAfford()
        {
            return demoRewards != null && demoRewards.CanAfford(cost);
        }

        private void HandlePointsChanged(int _)
        {
            RefreshVisuals();
        }

        private void ResolveReferences()
        {
            floorRenderer ??= GetComponent<Renderer>();
            playerPreviewRoot ??= FindChild(transform, "player");
            valueText ??= transform.Find("value text")?.GetComponent<TMP_Text>();
            demoRewards ??= FindAnyObjectByType<PlayerDemoRewards>();
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Material FindNamedMaterial(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (string.Equals(renderer.gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase)
                    && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        private void SetRendererColor(Renderer target, Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorProperty, color);
            propertyBlock.SetColor(BaseColorProperty, color);
            target.SetPropertyBlock(propertyBlock);
        }
    }
}
