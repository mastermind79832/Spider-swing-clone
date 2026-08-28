using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LocalPlayerController : MonoBehaviour
    {
        [Header("Balance")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float rotationSharpness = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 2.25f;

        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private OrbitCamera orbitCamera;
        [SerializeField] private SwingForbiddenZone swingForbiddenZone;
        [SerializeField] private PlayerDeathController deathController;
        [SerializeField] private PlayerCheckpointProgress checkpointProgress;
        [SerializeField] private PlayerDemoRewards demoRewards;
        [SerializeField] private PlayerProgression progression;
        [SerializeField] private LineRenderer webLine;
        [SerializeField] private Transform webOrigin;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction jumpAction;
        private float verticalVelocity;
        private int currentSwings;
        private int maxSwings;
        private float swingForwardMultiplier = 1f;
        private PlayerMovementState state = PlayerMovementState.Grounded;
        private Vector3 swingDirection;
        private Vector3 swingAnchor;
        private float swingElapsed;
        private float swingStartY;
        private CoursePlatform lastTopPlatform;
        private Material runtimeWebMaterial;
        private bool initialized;

        public event Action<float> OnTraversalDistanceMoved;

        public PlayerMovementState State => state;
        public bool IsGrounded => characterController != null
            && (characterController.isGrounded || HasGroundBelow());
        public bool IsSwinging => state == PlayerMovementState.Swinging;
        public bool IsDead => state == PlayerMovementState.Dead;
        public bool IsWebVisible => webLine != null && webLine.enabled;
        public float VerticalVelocity => verticalVelocity;
        public int CurrentSwings => currentSwings;
        public int MaxSwings => maxSwings;
        public float BaseMoveSpeed => balanceConfig != null ? balanceConfig.moveSpeed : 7f;
        public float BaseSwingForwardMultiplier => balanceConfig != null
            ? balanceConfig.swingForwardMultiplier
            : 1f;
        public int BaseMaxSwings => balanceConfig != null ? Mathf.Max(1, balanceConfig.maxSwings) : 2;
        public float MoveSpeed => moveSpeed;
        public float SwingForwardMultiplier => swingForwardMultiplier;
        public Vector3 SwingDirection => swingDirection;
        public float SwingElapsed => swingElapsed;

        public void Configure(InputActionAsset actions, OrbitCamera cameraController)
        {
            Configure(actions, cameraController, balanceConfig);
        }

        public void Configure(
            InputActionAsset actions,
            OrbitCamera cameraController,
            GameBalanceConfig configuredBalance)
        {
            inputActions = actions;
            orbitCamera = cameraController;
            balanceConfig = configuredBalance;

            ApplyBalance();
            ResolveInputActions();
            if (isActiveAndEnabled)
            {
                moveAction?.Enable();
                jumpAction?.Enable();
            }
        }

        public void ConfigureWorld(
            SwingForbiddenZone forbiddenZone,
            PlayerDeathController configuredDeathController,
            LineRenderer configuredWebLine)
        {
            swingForbiddenZone = forbiddenZone;
            deathController = configuredDeathController;
            webLine = configuredWebLine != null ? configuredWebLine : webLine;
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
            demoRewards = GetComponent<PlayerDemoRewards>();
            ConfigureWebLine();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
            demoRewards = GetComponent<PlayerDemoRewards>();
            progression = GetComponent<PlayerProgression>();
            characterController.height = 2f;
            characterController.radius = 0.45f;
            characterController.center = Vector3.zero;
            characterController.stepOffset = 0.35f;

            var primitiveCollider = GetComponent<CapsuleCollider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
            }

            ApplyBalance();
            ResolveInputActions();
            ConfigureWebLine();
            initialized = true;
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
        }

        private void OnDestroy()
        {
            if (runtimeWebMaterial != null)
            {
                Destroy(runtimeWebMaterial);
            }
        }

        private void ApplyBalance()
        {
            if (balanceConfig != null)
            {
                moveSpeed = balanceConfig.moveSpeed;
                rotationSharpness = balanceConfig.rotationSharpness;
                gravity = balanceConfig.gravity;
                jumpHeight = balanceConfig.jumpHeight;
                maxSwings = Mathf.Max(1, balanceConfig.maxSwings);
                swingForwardMultiplier = Mathf.Max(0f, balanceConfig.swingForwardMultiplier);
            }
            else
            {
                maxSwings = 2;
                swingForwardMultiplier = 1f;
            }

            if (!initialized)
            {
                currentSwings = maxSwings;
            }
            else
            {
                currentSwings = Mathf.Clamp(currentSwings, 0, maxSwings);
            }
        }

        private void ResolveInputActions()
        {
            moveAction = inputActions != null
                ? inputActions.FindAction("Player/Move", false)
                : null;
            jumpAction = inputActions != null
                ? inputActions.FindAction("Player/Jump", false)
                : null;

            // Configuration can be injected immediately after AddComponent in tests
            // and editor setup. A missing asset therefore leaves movement inert until
            // Configure supplies one, instead of creating a noisy hard failure.
        }

        private void ConfigureWebLine()
        {
            if (webLine == null)
            {
                return;
            }

            webLine.useWorldSpace = true;
            webLine.positionCount = 2;
            webLine.widthMultiplier = 0.04f;
            webLine.enabled = false;

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

        private void Update()
        {
            if (state == PlayerMovementState.Dead)
            {
                return;
            }

            if (state == PlayerMovementState.Swinging)
            {
                UpdateSwing(Time.deltaTime);
                return;
            }

            var grounded = characterController.isGrounded || HasGroundBelow();
            if (grounded && state != PlayerMovementState.Grounded)
            {
                state = PlayerMovementState.Grounded;
            }

            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                if (grounded)
                {
                    verticalVelocity = SwingRules.EvaluateJumpVelocity(jumpHeight, gravity);
                    state = PlayerMovementState.Airborne;
                }
                else if (TryStartSwing())
                {
                    return;
                }
            }

            var movement = GetCameraRelativeMovement(moveAction != null
                ? moveAction.ReadValue<Vector2>()
                : Vector2.zero);

            RotateTowards(movement);
            verticalVelocity += gravity * Time.deltaTime;
            var velocity = movement * moveSpeed;
            velocity.y = verticalVelocity;
            MoveAndTrack(velocity * Time.deltaTime, grounded);
        }

        public bool TryStartSwing()
        {
            if (state != PlayerMovementState.Airborne)
            {
                return false;
            }

            var swingPermitted = swingForbiddenZone == null
                || !swingForbiddenZone.Contains(transform.position);
            if (!SwingRules.CanStartSwing(
                    IsGrounded,
                    currentSwings,
                    swingPermitted,
                    IsSwinging))
            {
                return false;
            }

            var movement = GetCameraRelativeMovement(moveAction != null
                ? moveAction.ReadValue<Vector2>()
                : Vector2.zero);
            swingDirection = SwingRules.CaptureDirection(movement, transform.forward);
            currentSwings--;
            swingElapsed = 0f;
            swingStartY = transform.position.y;
            swingAnchor = transform.position
                + swingDirection * GetBalanceValue(value => value.webAnchorForwardOffset, 6f)
                + Vector3.up * GetBalanceValue(value => value.webAnchorHeightOffset, 10f);
            state = PlayerMovementState.Swinging;
            verticalVelocity = 0f;
            SetWebVisible(true);
            UpdateWebLine();
            return true;
        }

        private void UpdateSwing(float deltaTime)
        {
            var duration = GetBalanceValue(value => value.swingDuration, 1.25f);
            var wasReleased = jumpAction != null && jumpAction.WasReleasedThisFrame();
            if (wasReleased)
            {
                EndSwingEarly();
                return;
            }

            swingElapsed = Mathf.Min(duration, swingElapsed + Mathf.Max(0f, deltaTime));
            var desiredY = SwingRules.EvaluateVertical(
                swingStartY,
                GetSwingCurve(),
                swingElapsed,
                duration);
            var delta = swingDirection
                * moveSpeed
                * swingForwardMultiplier
                * deltaTime;
            delta.y = desiredY - transform.position.y;

            MoveAndTrack(delta, false);
            UpdateWebLine();

            if (state == PlayerMovementState.Dead || state == PlayerMovementState.Grounded)
            {
                return;
            }

            if (swingElapsed >= duration)
            {
                EndSwingAtDuration();
            }
        }

        private void EndSwingAtDuration()
        {
            SetWebVisible(false);
            state = PlayerMovementState.Airborne;
            verticalVelocity = SwingRules.EvaluateExitVelocity(
                GetSwingCurve(),
                1f,
                GetBalanceValue(value => value.swingDuration, 1.25f),
                GetBalanceValue(value => value.releaseVelocityMinimum, -12f),
                GetBalanceValue(value => value.releaseVelocityMaximum, 12f));
        }

        private void EndSwingEarly()
        {
            var duration = GetBalanceValue(value => value.swingDuration, 1.25f);
            var normalizedTime = duration > 0f ? swingElapsed / duration : 1f;
            SetWebVisible(false);
            state = PlayerMovementState.Airborne;
            verticalVelocity = SwingRules.EvaluateExitVelocity(
                GetSwingCurve(),
                normalizedTime,
                duration,
                GetBalanceValue(value => value.releaseVelocityMinimum, -12f),
                GetBalanceValue(value => value.releaseVelocityMaximum, 12f));
        }

        public void EnterDeadState()
        {
            if (state == PlayerMovementState.Dead)
            {
                return;
            }

            SetWebVisible(false);
            state = PlayerMovementState.Dead;
            verticalVelocity = 0f;
        }

        public void ResetAfterRespawn()
        {
            SetWebVisible(false);
            currentSwings = maxSwings;
            verticalVelocity = -2f;
            state = PlayerMovementState.Grounded;
        }

        public void ApplyProgressionStats(
            float configuredMoveSpeed,
            float configuredSwingForwardMultiplier,
            int configuredMaxSwings)
        {
            moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
            swingForwardMultiplier = Mathf.Max(0f, configuredSwingForwardMultiplier);
            var previousMaximum = maxSwings;
            maxSwings = Mathf.Max(1, configuredMaxSwings);
            currentSwings = ProgressionRules.SwingsAfterMaxChange(
                currentSwings,
                previousMaximum,
                maxSwings);
        }

        public void ApplyPlayerSkin(Material armMaterial, Material bodyMaterial)
        {
            var skinVisual = GetComponent<PlayerSkinVisual>() ?? gameObject.AddComponent<PlayerSkinVisual>();
            skinVisual?.Apply(armMaterial, bodyMaterial);
        }

        // Kept for existing editor/test callers. New gameplay should use the
        // Arm/Body-specific overload so authored model materials remain intact.
        public void ApplyPlayerSkinMaterial(Material skinMaterial)
        {
            ApplyPlayerSkin(skinMaterial, skinMaterial);
        }

        public void ConfigureProgression(PlayerProgression configuredProgression)
        {
            progression = configuredProgression;
        }

        private void MoveAndTrack(Vector3 delta, bool wasGrounded)
        {
            if (state == PlayerMovementState.Dead || characterController == null)
            {
                return;
            }

            lastTopPlatform = null;
            var previousPosition = transform.position;
            var flags = characterController.Move(delta);
            ClampMaximumY();
            var nowGrounded = characterController.isGrounded
                || (flags & CollisionFlags.Below) != 0
                || HasGroundBelow();

            var callbackState = state;
            if (!wasGrounded && callbackState == PlayerMovementState.Grounded)
            {
                callbackState = PlayerMovementState.Airborne;
            }

            if (state != PlayerMovementState.Dead
                && TraversalDistanceRules.TryGetDistance(
                    previousPosition,
                    transform.position,
                    callbackState,
                    deathController != null && deathController.IsRespawning,
                    out var distance))
            {
                OnTraversalDistanceMoved?.Invoke(distance);
            }

            if (state == PlayerMovementState.Dead)
            {
                return;
            }

            if (nowGrounded)
            {
                if (lastTopPlatform == null)
                {
                    lastTopPlatform = FindTopPlatformBelow();
                }

                RegisterTopLanding(lastTopPlatform);

                if (state == PlayerMovementState.Swinging)
                {
                    SetWebVisible(false);
                }

                state = PlayerMovementState.Grounded;
                verticalVelocity = -2f;
            }
            else if (state == PlayerMovementState.Grounded)
            {
                state = PlayerMovementState.Airborne;
            }

            deathController?.CheckPosition(transform.position);
        }

        private CoursePlatform FindTopPlatformBelow()
        {
            if (characterController == null || !characterController.enabled)
            {
                return null;
            }

            var bounds = characterController.bounds;
            var origin = bounds.center + Vector3.up * 0.05f;
            var distance = bounds.extents.y + 0.25f;
            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out var hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                || !CoursePlatform.IsTopLanding(hit.normal))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<CoursePlatform>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (CoursePlatform.IsTopLanding(hit.normal))
            {
                RegisterTopLanding(hit.collider.GetComponentInParent<CoursePlatform>());
            }
        }

        private void RegisterTopLanding(CoursePlatform platform)
        {
            if (platform == null)
            {
                return;
            }

            // Every valid platform uses the same landing contract: restore all
            // swing charges and remember that platform as the latest checkpoint.
            lastTopPlatform = platform;
            currentSwings = maxSwings;
            checkpointProgress?.Reach(platform);
        }

        private void ClampMaximumY()
        {
            var maximumY = GetBalanceValue(value => value.maximumY, 60f);
            if (transform.position.y <= maximumY)
            {
                return;
            }

            var clampedPosition = WorldLimitRules.ClampMaximumY(transform.position, maximumY);
            characterController.enabled = false;
            transform.position = clampedPosition;
            characterController.enabled = true;
            verticalVelocity = Mathf.Min(0f, verticalVelocity);
        }

        private bool HasGroundBelow()
        {
            if (characterController == null || !characterController.enabled)
            {
                return false;
            }

            var bounds = characterController.bounds;
            var origin = new Vector3(bounds.center.x, bounds.min.y + 0.12f, bounds.center.z);
            return Physics.Raycast(
                origin,
                Vector3.down,
                0.25f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void RotateTowards(Vector3 movement)
        {
            if (movement.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSharpness * Time.deltaTime);
        }

        private Vector3 GetCameraRelativeMovement(Vector2 moveInput)
        {
            var viewTransform = orbitCamera != null
                ? orbitCamera.transform
                : Camera.main != null ? Camera.main.transform : null;

            if (viewTransform == null)
            {
                return Vector3.zero;
            }

            var forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up).normalized;
            var movement = forward * moveInput.y + right * moveInput.x;
            return Vector3.ClampMagnitude(movement, 1f);
        }

        private AnimationCurve GetSwingCurve()
        {
            if (balanceConfig != null && balanceConfig.swingVerticalCurve != null)
            {
                return balanceConfig.swingVerticalCurve;
            }

            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -2f),
                new Keyframe(1f, 6f));
        }

        private float GetBalanceValue(
            Func<GameBalanceConfig, float> selector,
            float fallback)
        {
            return balanceConfig != null ? selector(balanceConfig) : fallback;
        }

        private void SetWebVisible(bool visible)
        {
            if (webLine != null)
            {
                webLine.enabled = visible;
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

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 110f, 420f, 125f), GUI.skin.box);
            GUILayout.Label("Controls: click game • WASD move • Mouse look • Space jump/swing • Esc unlock");
            GUILayout.Label($"State: {state} • Swings: {currentSwings}/{maxSwings}");
            GUILayout.Label("Traversal distance callback: ready for future XP");
            GUILayout.EndArea();

            GUILayout.BeginArea(
                new Rect(Screen.width * 0.5f - 170f, Screen.height - 135f, 340f, 113f),
                GUI.skin.box);
            if (progression != null)
            {
                var xpText = progression.IsAtMaximumLevel
                    ? "MAX"
                    : $"{Mathf.FloorToInt(progression.CurrentXp)}/{Mathf.CeilToInt(progression.XpToNextLevel)}";
                GUILayout.Label($"Level {progression.Level}  XP: {xpText}");
                GUILayout.Label($"Speed: {moveSpeed:0.##}  Swing speed: {swingForwardMultiplier:0.##}  x{progression.XpMultiplier:0.##}");
            }

            GUILayout.Label($"Swing: {currentSwings}/{maxSwings}");
            GUILayout.Label($"Points: {demoRewards?.ReturnPoints ?? 0}");
            GUILayout.EndArea();
        }
    }
}
