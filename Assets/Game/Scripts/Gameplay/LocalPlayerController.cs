using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class LocalPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float rotationSharpness = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 2.25f;

        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private OrbitCamera orbitCamera;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction jumpAction;
        private float verticalVelocity;

        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public float VerticalVelocity => verticalVelocity;

        public void Configure(InputActionAsset actions, OrbitCamera cameraController)
        {
            inputActions = actions;
            orbitCamera = cameraController;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.45f;
            characterController.center = new Vector3(0f, 1f, 0f);
            characterController.stepOffset = 0.35f;

            var primitiveCollider = GetComponent<CapsuleCollider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
            }

            if (inputActions == null)
            {
                Debug.LogError("Local player is missing the Input System action asset.", this);
                enabled = false;
                return;
            }

            moveAction = inputActions.FindAction("Player/Move", true);
            jumpAction = inputActions.FindAction("Player/Jump", true);
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

        private void Update()
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (characterController.isGrounded && jumpAction.WasPressedThisFrame())
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            var moveInput = moveAction.ReadValue<Vector2>();
            var movement = GetCameraRelativeMovement(moveInput);
            if (movement.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSharpness * Time.deltaTime);
            }

            verticalVelocity += gravity * Time.deltaTime;
            var velocity = movement * moveSpeed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
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

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 110f, 420f, 100f), GUI.skin.box);
            GUILayout.Label("Controls: click game • WASD move • Mouse look • Space jump • Esc unlock");
            GUILayout.Label($"State: {(IsGrounded ? "Grounded" : "Airborne")}");
            GUILayout.EndArea();
        }
    }
}
