using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSwing.Gameplay
{
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float distance = 9f;
        [SerializeField] private float targetHeight = 1.2f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 65f;

        private InputAction lookAction;
        private float yaw;
        private float pitch = 25f;

        public void Configure(InputActionAsset actions, Transform followTarget)
        {
            inputActions = actions;
            target = followTarget;
        }

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("Orbit camera is missing the Input System action asset.", this);
                enabled = false;
                return;
            }

            lookAction = inputActions.FindAction("Player/Look", true);
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }

        private void OnEnable()
        {
            lookAction?.Enable();
        }

        private void OnDisable()
        {
            lookAction?.Disable();
        }

        private void Update()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var look = lookAction.ReadValue<Vector2>();
            yaw += look.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * mouseSensitivity, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var pivot = target.position + Vector3.up * targetHeight;
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }
    }
}
