using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class FloatingRewardText : MonoBehaviour
    {
        private const float Lifetime = 1f;
        private const float RiseSpeed = 1.25f;
        private TextMesh textMesh;
        private float elapsed;

        public static FloatingRewardText Show(int value, Vector3 worldPosition)
        {
            var rewardObject = new GameObject($"RewardText_+{Mathf.Max(0, value)}");
            rewardObject.transform.position = worldPosition + Vector3.up * 1.5f;

            var text = rewardObject.AddComponent<TextMesh>();
            text.text = $"+{Mathf.Max(0, value)}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.25f;
            text.color = Color.yellow;

            var floatingText = rewardObject.AddComponent<FloatingRewardText>();
            floatingText.textMesh = text;
            return floatingText;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            var camera = Camera.main;
            if (camera != null)
            {
                transform.forward = camera.transform.forward;
            }

            if (textMesh != null)
            {
                var alpha = 1f - Mathf.Clamp01(elapsed / Lifetime);
                var color = textMesh.color;
                color.a = alpha;
                textMesh.color = color;
            }

            if (elapsed >= Lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
