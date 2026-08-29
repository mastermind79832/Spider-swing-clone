using UnityEngine;

namespace SpiderSwing.Gameplay
{
    /// <summary>
    /// Presentation-only swing visual. It keeps the web attached to the
    /// authored right-arm rig and applies a temporary straight-arm pose after
    /// any Animator evaluation.
    /// </summary>
    public sealed class PlayerSwingVisual : MonoBehaviour
    {
        private const string UpperArmName = "ArmR1";
        private const string LowerArmName = "ArmR2";
        private const string WebOriginName = "RightWebOrigin";

        [Header("Rig references")]
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform rightUpperArm;
        [SerializeField] private Transform rightLowerArm;
        [SerializeField] private Transform webOrigin;

        [Header("Web")]
        [SerializeField] private LineRenderer webLine;
        [SerializeField, Min(0.001f)] private float webWidth = 0.04f;

        [Header("Pose")]
        [SerializeField] private Vector3 armAimAxis = Vector3.up;
        [SerializeField, Min(0.01f)] private float poseBlendDuration = 0.1f;

        private Animator animator;
        private Vector3 swingAnchor;
        private Quaternion restUpperLocalRotation;
        private Quaternion restLowerLocalRotation;
        private float poseWeight;
        private bool swingActive;
        private bool hasRestPose;
        private bool warnedMissingRig;
        private Material runtimeWebMaterial;

        public bool IsSwinging => swingActive;
        public bool IsWebVisible => webLine != null && webLine.enabled;
        public Transform WebOrigin => webOrigin;
        public Transform RightUpperArm => rightUpperArm;
        public Transform RightLowerArm => rightLowerArm;

        public void Configure(
            Transform configuredModelRoot = null,
            Transform configuredUpperArm = null,
            Transform configuredLowerArm = null,
            Transform configuredWebOrigin = null,
            LineRenderer configuredWebLine = null)
        {
            if (configuredModelRoot != null)
            {
                modelRoot = configuredModelRoot;
            }

            if (configuredUpperArm != null)
            {
                rightUpperArm = configuredUpperArm;
            }

            if (configuredLowerArm != null)
            {
                rightLowerArm = configuredLowerArm;
            }

            if (configuredWebOrigin != null)
            {
                webOrigin = configuredWebOrigin;
            }

            if (configuredWebLine != null)
            {
                webLine = configuredWebLine;
            }

            ResolveReferences(createMissingSocket: true);
            CaptureRestPoseIfNeeded();
            ConfigureWebLine();
        }

        public void SetSwingState(bool active, Vector3 anchor)
        {
            swingAnchor = anchor;
            if (active)
            {
                ResolveReferences(createMissingSocket: true);
                CaptureRestPoseIfNeeded();
                swingActive = true;
                if (webLine != null)
                {
                    webLine.enabled = true;
                }

                return;
            }

            swingActive = false;
            if (webLine != null)
            {
                webLine.enabled = false;
            }
        }

        public void SetSwingAnchor(Vector3 anchor)
        {
            swingAnchor = anchor;
        }

        private void Awake()
        {
            ResolveReferences(createMissingSocket: true);
            CaptureRestPoseIfNeeded();
            ConfigureWebLine();
        }

        private void OnDisable()
        {
            swingActive = false;
            if (webLine != null)
            {
                webLine.enabled = false;
            }

            RestoreRestPoseImmediately();
        }

        private void OnDestroy()
        {
            if (runtimeWebMaterial != null)
            {
                Destroy(runtimeWebMaterial);
            }
        }

        private void LateUpdate()
        {
            ResolveReferences(createMissingSocket: false);
            CaptureRestPoseIfNeeded();

            var blendStep = poseBlendDuration > 0f
                ? Time.deltaTime / poseBlendDuration
                : 1f;
            poseWeight = Mathf.MoveTowards(poseWeight, swingActive ? 1f : 0f, blendStep);

            if (swingActive)
            {
                ApplyAimPose(poseWeight);
                UpdateWebLine();
            }
            else if (poseWeight > 0f)
            {
                RestorePose(poseWeight);
            }
            else
            {
                RestoreRestPoseImmediately();
            }
        }

        private void ResolveReferences(bool createMissingSocket)
        {
            if (modelRoot == null)
            {
                modelRoot = FindTransform(transform, "base_rig") ?? transform;
            }

            rightUpperArm ??= FindTransform(modelRoot, UpperArmName);
            rightLowerArm ??= FindTransform(modelRoot, LowerArmName);
            webOrigin ??= FindTransform(modelRoot, WebOriginName);

            if (webOrigin == null && createMissingSocket && rightLowerArm != null)
            {
                var socketObject = new GameObject(WebOriginName);
                webOrigin = socketObject.transform;
                webOrigin.SetParent(rightLowerArm, false);
                webOrigin.localPosition = CalculateDefaultSocketPosition();
                webOrigin.localRotation = Quaternion.identity;
                webOrigin.localScale = Vector3.one;
            }

            animator = modelRoot != null ? modelRoot.GetComponentInParent<Animator>() : null;
            if ((rightUpperArm == null || rightLowerArm == null) && !warnedMissingRig)
            {
                warnedMissingRig = true;
                Debug.LogWarning(
                    "PlayerSwingVisual could not find ArmR1 and ArmR2. " +
                    "Swing movement will continue with the fallback web origin.",
                    this);
            }
        }

        private void CaptureRestPoseIfNeeded()
        {
            if (hasRestPose || rightUpperArm == null || rightLowerArm == null)
            {
                return;
            }

            restUpperLocalRotation = rightUpperArm.localRotation;
            restLowerLocalRotation = rightLowerArm.localRotation;
            hasRestPose = true;
        }

        private Vector3 CalculateDefaultSocketPosition()
        {
            var worldLength = GetLowerArmLength();
            var localScale = rightLowerArm != null ? rightLowerArm.lossyScale.y : 1f;
            var localLength = worldLength / Mathf.Max(0.001f, Mathf.Abs(localScale));
            return Vector3.up * Mathf.Max(0.005f, localLength);
        }

        private float GetLowerArmLength()
        {
            if (rightLowerArm != null)
            {
                for (var index = 0; index < rightLowerArm.childCount; index++)
                {
                    var child = rightLowerArm.GetChild(index);
                    if (child.name == WebOriginName)
                    {
                        continue;
                    }

                    var childLength = Vector3.Distance(rightLowerArm.position, child.position);
                    if (childLength > 0.0001f)
                    {
                        return childLength;
                    }
                }
            }

            // The imported Generic rig has no hand bone, so use the authored
            // shoulder-to-elbow segment as the stable fallback length.
            if (rightUpperArm != null && rightLowerArm != null)
            {
                return Vector3.Distance(rightUpperArm.position, rightLowerArm.position);
            }

            return 0.6f;
        }

        private void ConfigureWebLine()
        {
            if (webLine == null)
            {
                return;
            }

            webLine.useWorldSpace = true;
            webLine.positionCount = 2;
            webLine.widthMultiplier = webWidth;
            webLine.enabled = swingActive;

            if (webLine.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    runtimeWebMaterial = new Material(shader)
                    {
                        color = Color.white,
                    };
                    webLine.sharedMaterial = runtimeWebMaterial;
                }
            }
        }

        private void UpdateWebLine()
        {
            if (webLine == null || !webLine.enabled)
            {
                return;
            }

            var origin = webOrigin != null
                ? webOrigin.position
                : transform.position + Vector3.up;
            webLine.SetPosition(0, origin);
            webLine.SetPosition(1, swingAnchor);
        }

        private void ApplyAimPose(float weight)
        {
            if (rightUpperArm == null || rightLowerArm == null)
            {
                return;
            }

            var upperParentRotation = rightUpperArm.parent != null
                ? rightUpperArm.parent.rotation
                : Quaternion.identity;
            var upperBaseLocalRotation = GetBaseLocalRotation(rightUpperArm, restUpperLocalRotation);
            var upperTargetLocalRotation = AimLocalRotation(
                rightUpperArm,
                upperParentRotation,
                upperBaseLocalRotation);
            var upperTargetWorldRotation = upperParentRotation * upperTargetLocalRotation;

            var lowerBaseLocalRotation = GetBaseLocalRotation(rightLowerArm, restLowerLocalRotation);
            var lowerParentRotation = rightLowerArm.parent == rightUpperArm
                ? upperTargetWorldRotation
                : rightLowerArm.parent != null
                    ? rightLowerArm.parent.rotation
                    : Quaternion.identity;
            var lowerTargetLocalRotation = AimLocalRotation(
                rightLowerArm,
                lowerParentRotation,
                lowerBaseLocalRotation);

            rightUpperArm.localRotation = BlendLocalRotationKeepingY(
                upperBaseLocalRotation,
                upperTargetLocalRotation,
                weight);
            rightLowerArm.localRotation = BlendLocalRotationKeepingY(
                lowerBaseLocalRotation,
                lowerTargetLocalRotation,
                weight);
        }

        private Quaternion AimLocalRotation(
            Transform arm,
            Quaternion parentWorldRotation,
            Quaternion baseLocalRotation)
        {
            var direction = swingAnchor - arm.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }

            direction.Normalize();
            var axis = armAimAxis.sqrMagnitude > 0.0001f ? armAimAxis.normalized : Vector3.up;
            var baseWorldRotation = parentWorldRotation * baseLocalRotation;
            var currentAxis = baseWorldRotation * axis;
            var targetWorldRotation = Quaternion.FromToRotation(currentAxis, direction) * baseWorldRotation;
            var targetLocalRotation = Quaternion.Inverse(parentWorldRotation) * targetWorldRotation;
            return KeepLocalYRotation(targetLocalRotation, baseLocalRotation);
        }

        private Quaternion GetBaseLocalRotation(Transform arm, Quaternion restLocalRotation)
        {
            if (HasActiveAnimator())
            {
                return arm.localRotation;
            }

            return restLocalRotation;
        }

        private bool HasActiveAnimator()
        {
            return animator != null && animator.runtimeAnimatorController != null && animator.enabled;
        }

        private void RestorePose(float weight)
        {
            if (rightUpperArm == null || rightLowerArm == null || !hasRestPose)
            {
                return;
            }

            if (HasActiveAnimator())
            {
                return;
            }

            var upperCurrentLocalRotation = rightUpperArm.localRotation;
            var lowerCurrentLocalRotation = rightLowerArm.localRotation;
            rightUpperArm.localRotation = BlendLocalRotationKeepingY(
                restUpperLocalRotation,
                upperCurrentLocalRotation,
                weight);
            rightLowerArm.localRotation = BlendLocalRotationKeepingY(
                restLowerLocalRotation,
                lowerCurrentLocalRotation,
                weight);
        }

        private static Quaternion BlendLocalRotationKeepingY(
            Quaternion baseLocalRotation,
            Quaternion targetLocalRotation,
            float weight)
        {
            return KeepLocalYRotation(
                Quaternion.Slerp(baseLocalRotation, targetLocalRotation, weight),
                baseLocalRotation);
        }

        private static Quaternion KeepLocalYRotation(
            Quaternion rotation,
            Quaternion referenceLocalRotation)
        {
            var euler = rotation.eulerAngles;
            euler.y = referenceLocalRotation.eulerAngles.y;
            return Quaternion.Euler(euler);
        }

        private void RestoreRestPoseImmediately()
        {
            if (!hasRestPose || HasActiveAnimator())
            {
                return;
            }

            if (rightUpperArm != null)
            {
                rightUpperArm.localRotation = restUpperLocalRotation;
            }

            if (rightLowerArm != null)
            {
                rightLowerArm.localRotation = restLowerLocalRotation;
            }
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindTransform(root.GetChild(index), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
