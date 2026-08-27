using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class CoursePlatform : MonoBehaviour
    {
        [SerializeField] private string platformId = "P01";
        [SerializeField] private Transform savePoint;
        [SerializeField] private CourseReturnPoint returnPoint;
        [SerializeField] private int returnReward = 1;

        public string PlatformId => platformId;
        public Transform SavePoint => savePoint != null ? savePoint : transform;
        public CourseReturnPoint ReturnPoint => returnPoint;
        public int ReturnReward => returnReward;

        public void Configure(string id)
        {
            platformId = string.IsNullOrWhiteSpace(id) ? "Platform" : id;
        }

        public void Configure(
            string id,
            Transform configuredSavePoint,
            CourseReturnPoint configuredReturnPoint,
            int configuredReturnReward)
        {
            Configure(id);
            savePoint = configuredSavePoint != null ? configuredSavePoint : transform;
            returnPoint = configuredReturnPoint;
            returnReward = Mathf.Max(0, configuredReturnReward);
        }

        public void Configure(string id, Transform configuredSavePoint, int configuredReturnReward)
        {
            Configure(id, configuredSavePoint, returnPoint, configuredReturnReward);
        }

        public static bool IsTopLanding(Vector3 normal)
        {
            return normal.y >= 0.7f;
        }
    }
}
