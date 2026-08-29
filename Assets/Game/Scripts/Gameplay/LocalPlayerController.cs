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
        [SerializeField] private PlayerSwingVisual swingVisual;
        [SerializeField] private PlayerAnimationController animationController;

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
        private float swingHorizontalSpeed;
        private float swingElapsed;
        private float swingStartY;
        private CoursePlatform lastTopPlatform;
        private Material runtimeWebMaterial;
        private bool initialized;
        private bool swingVisualActive;
        private float landingRecoveryRemaining;
        private bool landingExitBlendStarted;
        private float jumpLandingGraceRemaining;

        public event Action<float> OnTraversalDistanceMoved;
        public event Action<bool, Vector3> OnSwingStateChanged;

        public PlayerMovementState State => state;
        public bool IsGrounded => characterController != null
            && HasGroundContact()
            && (state == PlayerMovementState.Grounded
                || (jumpLandingGraceRemaining <= 0f && verticalVelocity <= 0f));
        public bool IsSwinging => state == PlayerMovementState.Swinging;
        public bool IsDead => state == PlayerMovementState.Dead;
        public bool IsLandingRecovery => state == PlayerMovementState.Landing;
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
        public float MaximumTravelSpeed => balanceConfig != null
            ? Mathf.Max(1f, balanceConfig.maximumTravelSpeed)
            : 50f;
        public Vector3 SwingDirection => swingDirection;
        public Vector3 SwingAnchor => swingAnchor;
        public float SwingHorizontalSpeed => swingHorizontalSpeed;
        public float SwingElapsed => swingElapsed;
        public float JumpLandingGraceRemaining => jumpLandingGraceRemaining;
        public PlayerAnimationController AnimationController => animationController;

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
            animationController?.Configure(configuredBlendDuration: GetBalanceValue(
                value => value.animationBlendDuration,
                0.1f));
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
            swingVisual = swingVisual != null
                ? swingVisual
                : GetComponent<PlayerSwingVisual>();
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
            demoRewards = GetComponent<PlayerDemoRewards>();
            ConfigureWebLine();
            swingVisual?.Configure(configuredWebLine: webLine, configuredWebOrigin: webOrigin);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
            demoRewards = GetComponent<PlayerDemoRewards>();
            progression = GetComponent<PlayerProgression>();
            swingVisual = GetComponent<PlayerSwingVisual>();
            animationController = GetComponent<PlayerAnimationController>();
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
            animationController?.Configure(configuredBlendDuration: GetBalanceValue(
                value => value.animationBlendDuration,
                0.1f));
            ConfigureWebLine();
            swingVisual?.Configure(configuredWebLine: webLine, configuredWebOrigin: webOrigin);
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
            SetSwingVisualState(false);
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
                moveSpeed = ProgressionRules.ClampTravelSpeed(
                    balanceConfig.moveSpeed,
                    balanceConfig.maximumTravelSpeed);
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

        private void StartJump()
        {
            verticalVelocity = SwingRules.EvaluateJumpVelocity(jumpHeight, gravity);
            jumpLandingGraceRemaining = Mathf.Max(
                0f,
                GetBalanceValue(value => value.jumpLandingGraceDuration, 0.12f));
            state = PlayerMovementState.Airborne;
            SetAnimationState(PlayerAnimationState.Jump);
        }

        private void Update()
        {
            jumpLandingGraceRemaining = Mathf.Max(
                0f,
                jumpLandingGraceRemaining - Mathf.Max(0f, Time.deltaTime));

            if (state == PlayerMovementState.Dead)
            {
                return;
            }

            if (state == PlayerMovementState.Landing)
            {
                UpdateLandingRecovery(Time.deltaTime);
                return;
            }

            if (state == PlayerMovementState.Swinging)
            {
                UpdateSwing(Time.deltaTime);
                return;
            }

            var groundedContact = HasGroundContact();
            var grounded = groundedContact
                && (state == PlayerMovementState.Grounded
                    || (jumpLandingGraceRemaining <= 0f && verticalVelocity <= 0f));
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
                    StartJump();
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

            if (state == PlayerMovementState.Grounded)
            {
                SetAnimationState(movement.sqrMagnitude > 0.001f
                    ? PlayerAnimationState.Walk
                    : PlayerAnimationState.Idle);
            }
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
            var duration = GetBalanceValue(value => value.swingDuration, 1.25f);
            swingHorizontalSpeed = ProgressionRules.ClampTravelSpeed(
                moveSpeed * swingForwardMultiplier,
                MaximumTravelSpeed);
            swingAnchor = SwingRules.CalculateProjectedAnchor(
                transform.position,
                swingDirection,
                swingHorizontalSpeed,
                duration,
                GetSwingCurve(),
                GetBalanceValue(value => value.webAnchorHeightOffset, 20f));
            state = PlayerMovementState.Swinging;
            verticalVelocity = 0f;
            SetAnimationState(PlayerAnimationState.SwingBack);
            SetSwingVisualState(true);
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
            SetSwingAnimationState(duration);
            var desiredY = SwingRules.EvaluateVertical(
                swingStartY,
                GetSwingCurve(),
                swingElapsed,
                duration);
            var delta = swingDirection
                * swingHorizontalSpeed
                * deltaTime;
            delta.y = desiredY - transform.position.y;

            MoveAndTrack(delta, false);
            UpdateWebLine();

            if (state == PlayerMovementState.Dead
                || state == PlayerMovementState.Grounded
                || state == PlayerMovementState.Landing)
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
            SetSwingVisualState(false);
            state = PlayerMovementState.Airborne;
            SetAnimationState(PlayerAnimationState.Jump);
            swingHorizontalSpeed = 0f;
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
            SetSwingVisualState(false);
            state = PlayerMovementState.Airborne;
            SetAnimationState(PlayerAnimationState.Jump);
            swingHorizontalSpeed = 0f;
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

            SetSwingVisualState(false);
            landingRecoveryRemaining = 0f;
            landingExitBlendStarted = false;
            jumpLandingGraceRemaining = 0f;
            state = PlayerMovementState.Dead;
            SetAnimationState(PlayerAnimationState.Jump);
            swingHorizontalSpeed = 0f;
            verticalVelocity = 0f;
        }

        public void ResetAfterRespawn()
        {
            SetSwingVisualState(false);
            landingRecoveryRemaining = 0f;
            landingExitBlendStarted = false;
            jumpLandingGraceRemaining = 0f;
            currentSwings = maxSwings;
            swingHorizontalSpeed = 0f;
            verticalVelocity = -2f;
            state = PlayerMovementState.Grounded;
            SetAnimationState(PlayerAnimationState.Idle);
        }

        public void ApplyProgressionStats(
            float configuredMoveSpeed,
            float configuredSwingForwardMultiplier,
            int configuredMaxSwings)
        {
            moveSpeed = ProgressionRules.ClampTravelSpeed(
                configuredMoveSpeed,
                MaximumTravelSpeed);
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
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

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

            var canStartLanding = LandingRules.CanStartRecovery(
                state,
                nowGrounded,
                delta.y,
                jumpLandingGraceRemaining);

            if (canStartLanding)
            {
                if (lastTopPlatform == null)
                {
                    lastTopPlatform = FindTopPlatformBelow();
                }

                RegisterTopLanding(lastTopPlatform);

                if (state == PlayerMovementState.Swinging)
                {
                    SetSwingVisualState(false);
                    swingHorizontalSpeed = 0f;
                }

                verticalVelocity = -2f;
                jumpLandingGraceRemaining = 0f;
                BeginLandingRecovery();
            }
            else if (nowGrounded && state == PlayerMovementState.Grounded)
            {
                RegisterTopLanding(FindTopPlatformBelow());
            }
            else if (!nowGrounded && state == PlayerMovementState.Grounded)
            {
                state = PlayerMovementState.Airborne;
                SetAnimationState(PlayerAnimationState.Jump);
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
            var canRegisterPlatform = state == PlayerMovementState.Grounded
                || ((state == PlayerMovementState.Airborne
                        || state == PlayerMovementState.Swinging)
                    && jumpLandingGraceRemaining <= 0f
                    && verticalVelocity <= 0f);
            if (canRegisterPlatform && CoursePlatform.IsTopLanding(hit.normal))
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

        private bool HasGroundContact()
        {
            return characterController != null
                && (characterController.isGrounded || HasGroundBelow());
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
            SetSwingVisualState(visible);
        }

        private void SetSwingVisualState(bool active)
        {
            if (active)
            {
                swingVisualActive = true;
                if (swingVisual != null)
                {
                    swingVisual.SetSwingState(true, swingAnchor);
                }
                else
                {
                    SetLegacyWebVisible(true);
                }

                OnSwingStateChanged?.Invoke(true, swingAnchor);
                return;
            }

            if (!swingVisualActive)
            {
                SetLegacyWebVisible(false);
                return;
            }

            swingVisualActive = false;
            if (swingVisual != null)
            {
                swingVisual.SetSwingState(false, swingAnchor);
            }
            else
            {
                SetLegacyWebVisible(false);
            }

            OnSwingStateChanged?.Invoke(false, swingAnchor);
        }

        private void SetLegacyWebVisible(bool visible)
        {
            if (webLine != null)
            {
                webLine.enabled = visible;
            }
        }

        private void UpdateWebLine()
        {
            if (swingVisual != null)
            {
                swingVisual.SetSwingAnchor(swingAnchor);
                return;
            }

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

        private void BeginLandingRecovery()
        {
            jumpLandingGraceRemaining = 0f;
            state = PlayerMovementState.Landing;
            landingRecoveryRemaining = Mathf.Max(
                0f,
                GetBalanceValue(value => value.landingRecoveryDuration, 0.4f));
            landingExitBlendStarted = false;
            SetAnimationState(PlayerAnimationState.Landing);

            if (landingRecoveryRemaining <= 0f)
            {
                state = PlayerMovementState.Grounded;
                SetGroundAnimationState();
            }
        }

        private void UpdateLandingRecovery(float deltaTime)
        {
            landingRecoveryRemaining = Mathf.Max(
                0f,
                landingRecoveryRemaining - Mathf.Max(0f, deltaTime));

            var blendDuration = GetBalanceValue(
                value => value.animationBlendDuration,
                0.1f);
            if (!landingExitBlendStarted
                && landingRecoveryRemaining <= Mathf.Min(blendDuration, GetBalanceValue(
                    value => value.landingRecoveryDuration,
                    0.4f)))
            {
                landingExitBlendStarted = true;
                SetGroundAnimationState();
            }

            if (landingRecoveryRemaining > 0f)
            {
                return;
            }

            state = PlayerMovementState.Grounded;
            SetGroundAnimationState();
        }

        private void SetGroundAnimationState()
        {
            var movement = GetCameraRelativeMovement(moveAction != null
                ? moveAction.ReadValue<Vector2>()
                : Vector2.zero);
            SetAnimationState(movement.sqrMagnitude > 0.001f
                ? PlayerAnimationState.Walk
                : PlayerAnimationState.Idle);
        }

        private void SetSwingAnimationState(float duration)
        {
            var normalizedTime = duration > 0f ? swingElapsed / duration : 1f;
            SetAnimationState(normalizedTime < 0.5f
                ? PlayerAnimationState.SwingBack
                : PlayerAnimationState.SwingForward);
        }

        private void SetAnimationState(PlayerAnimationState animationState)
        {
            animationController?.SetState(animationState);
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
