using TMPro;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class CoursePlatform : MonoBehaviour
    {
        [SerializeField] private string platformId = "P01";
        [SerializeField] private Transform savePoint;
        [SerializeField] private CourseReturnPoint returnPoint;
        [SerializeField] private int returnReward = 1;
        [SerializeField] private TMP_Text pointText;

        public string PlatformId => platformId;
        public Transform SavePoint => savePoint != null ? savePoint : transform;
        public CourseReturnPoint ReturnPoint => returnPoint;
        public int ReturnReward => returnReward;
        public TMP_Text PointText => pointText != null ? pointText : FindPointText();

        public void Configure(string id)
        {
            platformId = string.IsNullOrWhiteSpace(id) ? "Platform" : id;
            RefreshPointText();
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
            RefreshPointText();
        }

        public void Configure(string id, Transform configuredSavePoint, int configuredReturnReward)
        {
            Configure(id, configuredSavePoint, returnPoint, configuredReturnReward);
        }

        public static bool IsTopLanding(Vector3 normal)
        {
            return normal.y >= 0.7f;
        }

        public void RefreshPointText()
        {
            pointText = PointText;
            if (pointText == null)
            {
                return;
            }

            pointText.text = returnReward == 1
                ? "1 point"
                : $"{returnReward} points";
        }

        private void Awake()
        {
            RefreshPointText();
        }

        private void OnEnable()
        {
            RefreshPointText();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                RefreshPointText();
            }
        }
#endif

        private TMP_Text FindPointText()
        {
            var pointTextObject = transform.Find("point text");
            return pointTextObject != null ? pointTextObject.GetComponent<TMP_Text>() : null;
        }
    }
}
