using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace PrintshopTycoon
{
    /// <summary>
    /// Moves Hue relative to the camera using the virtual joystick (keyboard in the editor).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 4.2f;
        public float turnSpeed = 720f;

        CharacterController controller;
        Transform visual;
        Transform cameraTransform;
        VirtualJoystick joystick;
        AudioManager audioManager;
        float stepTimer;
        float bobPhase;
        float verticalVelocity;

        public bool InputEnabled { get; set; } = true;
        public bool IsMoving { get; private set; }
        public CarryStack Carry { get; private set; }

        public void Init(Transform cam, VirtualJoystick stick, AudioManager audio)
        {
            cameraTransform = cam;
            joystick = stick;
            audioManager = audio;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.stepOffset = 0.2f;
            visual = transform.Find("Visual");
            Carry = CarryStack.Create(transform, new Vector3(0f, 0.95f, 0.45f));
        }

        void Update()
        {
            Vector2 input = InputEnabled ? ReadInput() : Vector2.zero;
            Vector3 move = Vector3.zero;
            if (input.sqrMagnitude > 0.0025f && cameraTransform != null)
            {
                Vector3 fwd = cameraTransform.forward; fwd.y = 0f; fwd.Normalize();
                Vector3 right = cameraTransform.right; right.y = 0f; right.Normalize();
                move = Vector3.ClampMagnitude(fwd * input.y + right * input.x, 1f);
            }

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity - 20f * Time.deltaTime;
            controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

            IsMoving = move.sqrMagnitude > 0.01f;
            if (IsMoving)
            {
                var targetRot = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            AnimateWalk(move.magnitude);
        }

        void AnimateWalk(float speed01)
        {
            if (visual == null) return;
            if (speed01 > 0.05f)
            {
                bobPhase += Time.deltaTime * 14f * Mathf.Lerp(0.6f, 1f, speed01);
                float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.08f;
                visual.localPosition = new Vector3(0f, bob, 0f);
                visual.localScale = Vector3.one;
                visual.localRotation = Quaternion.Euler(0f, 180f, Mathf.Sin(bobPhase) * 4f);
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    stepTimer = 0.32f;
                    if (audioManager != null) audioManager.Play(Sfx.Footstep, 0.35f);
                }
            }
            else
            {
                // Idle "breathing" squash so Hue feels alive.
                float breathe = Mathf.Sin(Time.time * 2.4f) * 0.015f;
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, Time.deltaTime * 10f);
                visual.localScale = new Vector3(1f - breathe, 1f + breathe, 1f - breathe);
                visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }

        Vector2 ReadInput()
        {
            Vector2 v = joystick != null ? joystick.Value : Vector2.zero;
            Vector2 k = KeyboardInput();
            if (k.sqrMagnitude > v.sqrMagnitude) v = k;
            return Vector2.ClampMagnitude(v, 1f);
        }

        static Vector2 KeyboardInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#else
            return Vector2.zero;
#endif
        }

        public static bool InteractKeyPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
#else
            return false;
#endif
        }

        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
        }
    }
}
