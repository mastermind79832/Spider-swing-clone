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
        [SerializeField] private Material playerSkinMaterial;
        [SerializeField] private Color labelColor = Color.white;

        [Header("Prefab references")]
        [SerializeField] private Renderer floorRenderer;
        [SerializeField] private Renderer playerSkinRenderer;
        [SerializeField] private TMP_Text valueText;

        private PlayerDemoRewards demoRewards;
        private bool purchased;
        private MaterialPropertyBlock propertyBlock;

        public string UpgradeId => upgradeId;
        public int Cost => cost;
        public float XpMultiplier => xpMultiplier;
        public int ExtraSwings => extraSwings;
        public Material PlayerSkinMaterial => playerSkinMaterial;
        public bool IsPurchased => purchased;

        public void Configure(
            string configuredId,
            int configuredCost,
            float configuredXpMultiplier,
            int configuredExtraSwings,
            Material configuredPlayerSkinMaterial,
            Color configuredLabelColor)
        {
            upgradeId = string.IsNullOrWhiteSpace(configuredId) ? "Upgrade" : configuredId;
            cost = Mathf.Max(0, configuredCost);
            xpMultiplier = Mathf.Max(1f, configuredXpMultiplier);
            extraSwings = Mathf.Max(0, configuredExtraSwings);
            playerSkinMaterial = configuredPlayerSkinMaterial;
            labelColor = configuredLabelColor;
            ResolveReferences();
            ApplyPrefabVisuals();
            RefreshVisuals();
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
            ApplyPrefabVisuals();
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
            playerSkinRenderer ??= transform.Find("Player skin")?.GetComponent<Renderer>();
            valueText ??= transform.Find("value text")?.GetComponent<TMP_Text>();
            demoRewards ??= FindAnyObjectByType<PlayerDemoRewards>();
        }

        private void ApplyPrefabVisuals()
        {
            if (playerSkinRenderer != null && playerSkinMaterial != null)
            {
                playerSkinRenderer.sharedMaterial = playerSkinMaterial;
            }
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
