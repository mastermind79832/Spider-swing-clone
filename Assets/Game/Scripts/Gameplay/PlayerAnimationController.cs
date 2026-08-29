using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    /// <summary>
    /// Small code-driven presenter for the authored PlayerAnim controller.
    /// The Animator remains the source of base poses; this component only
    /// selects states and asks Unity to blend between them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private const string BaseLayerName = "Base Layer.";
        private const string BaseRigName = "base_rig";
        private const float DefaultBlendDuration = 0.1f;

        private static readonly int IdleHash = Animator.StringToHash(BaseLayerName + "idle");
        private static readonly int WalkHash = Animator.StringToHash(BaseLayerName + "walk");
        private static readonly int JumpHash = Animator.StringToHash(BaseLayerName + "Jump");
        private static readonly int SwingBackHash = Animator.StringToHash(BaseLayerName + "Swing back");
        private static readonly int SwingForwardHash = Animator.StringToHash(BaseLayerName + "Swing forward");
        private static readonly int LandingHash = Animator.StringToHash(BaseLayerName + "Landing");

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float blendDuration = DefaultBlendDuration;
        [SerializeField] private PlayerAnimationState currentState = PlayerAnimationState.Idle;

        private bool hasRequestedState;
        private bool warnedMissingAnimator;
        private bool warnedMissingState;

        public PlayerAnimationState CurrentState => currentState;
        public Animator Animator => animator;
        public event Action<PlayerAnimationState> OnAnimationStateChanged;

        public void Configure(
            Transform configuredModelRoot = null,
            Animator configuredAnimator = null,
            float configuredBlendDuration = -1f)
        {
            if (configuredModelRoot != null)
            {
                modelRoot = configuredModelRoot;
            }

            if (configuredAnimator != null)
            {
                animator = configuredAnimator;
            }

            if (configuredBlendDuration >= 0f)
            {
                blendDuration = configuredBlendDuration;
            }

            ResolveAnimator();
            ConfigureAnimator();
        }

        public bool SetState(PlayerAnimationState state, bool immediate = false)
        {
            if (!Enum.IsDefined(typeof(PlayerAnimationState), state))
            {
                return false;
            }

            if (hasRequestedState && currentState == state)
            {
                return false;
            }

            currentState = state;
            hasRequestedState = true;
            ResolveAnimator();
            ConfigureAnimator();
            ApplyState(state, immediate);
            OnAnimationStateChanged?.Invoke(state);
            return true;
        }

        public static bool IsValidState(int value)
        {
            return value >= (int)PlayerAnimationState.Idle
                && value <= (int)PlayerAnimationState.Landing;
        }

        private void Awake()
        {
            ResolveAnimator();
            ConfigureAnimator();
        }

        private void OnDisable()
        {
            if (animator != null)
            {
                animator.speed = 1f;
            }
        }

        private void ResolveAnimator()
        {
            if (modelRoot == null)
            {
                modelRoot = FindTransform(transform, BaseRigName);
            }

            if (animator == null && modelRoot != null)
            {
                animator = modelRoot.GetComponent<Animator>()
                    ?? modelRoot.GetComponentInChildren<Animator>(true)
                    ?? modelRoot.GetComponentInParent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void ConfigureAnimator()
        {
            if (animator == null)
            {
                if (!warnedMissingAnimator)
                {
                    warnedMissingAnimator = true;
                    Debug.LogWarning(
                        "PlayerAnimationController could not find an Animator. " +
                        "Gameplay will continue without animation playback.",
                        this);
                }

                return;
            }

            animator.applyRootMotion = false;
        }

        private void ApplyState(PlayerAnimationState state, bool immediate)
        {
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            var stateHash = GetStateHash(state);
            if (!animator.HasState(0, stateHash))
            {
                if (!warnedMissingState)
                {
                    warnedMissingState = true;
                    Debug.LogWarning(
                        "PlayerAnimationController could not find one or more expected states " +
                        "in the assigned Animator Controller.",
                        this);
                }

                return;
            }

            animator.CrossFadeInFixedTime(
                stateHash,
                immediate ? 0f : Mathf.Max(0f, blendDuration),
                0,
                0f);
        }

        private static int GetStateHash(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Walk:
                    return WalkHash;
                case PlayerAnimationState.Jump:
                    return JumpHash;
                case PlayerAnimationState.SwingBack:
                    return SwingBackHash;
                case PlayerAnimationState.SwingForward:
                    return SwingForwardHash;
                case PlayerAnimationState.Landing:
                    return LandingHash;
                default:
                    return IdleHash;
            }
        }

        private static Transform FindTransform(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
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
