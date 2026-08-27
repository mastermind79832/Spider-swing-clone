using UnityEngine;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class TreadmillXpZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rawXpPerSecond = 10f;

        private PlayerProgression activeProgression;

        public float RawXpPerSecond => rawXpPerSecond;

        public void Configure(float configuredRawXpPerSecond)
        {
            rawXpPerSecond = Mathf.Max(0f, configuredRawXpPerSecond);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<LocalPlayerController>()
                ?? other.GetComponentInParent<LocalPlayerController>();
            if (player == null || player.IsDead)
            {
                return;
            }

            activeProgression = player.GetComponent<PlayerProgression>();
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponent<LocalPlayerController>()
                ?? other.GetComponentInParent<LocalPlayerController>();
            if (player != null && activeProgression == player.GetComponent<PlayerProgression>())
            {
                activeProgression = null;
            }
        }

        private void Update()
        {
            if (activeProgression == null)
            {
                return;
            }

            var player = activeProgression.GetComponent<LocalPlayerController>();
            if (player == null || player.IsDead)
            {
                // Death is a hard stop. The player must enter the treadmill again
                // after respawning before training can resume.
                activeProgression = null;
                return;
            }

            activeProgression.AddTrainingXp(rawXpPerSecond * Time.deltaTime);
        }
    }
}
