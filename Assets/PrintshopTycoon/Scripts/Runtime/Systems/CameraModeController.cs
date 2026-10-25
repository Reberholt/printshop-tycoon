using System;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    public enum CameraMode { Character, Factory }

    /// <summary>
    /// One camera, two poses: a portrait-friendly third-person follow cam and an elevated
    /// isometric factory overview. Switching only blends the camera - game state is untouched.
    /// </summary>
    public class CameraModeController : MonoBehaviour
    {
        // Character mode framing (tuned for narrow 9:16 screens).
        public float followDistance = 10.5f;
        public float followPitch = 52f;
        public float lookAhead = 1.6f;
        public float followSmoothing = 8f;

        // Factory mode framing.
        public float factoryPitch = 55f;
        public float factoryYaw = 32f;
        public float factoryMinDistance = 11f;
        public float factoryMaxDistance = 30f;

        public float transitionSeconds = 0.8f;

        readonly RaycastHit[] hitBuffer = new RaycastHit[8];
        Camera cam;
        Transform target;
        Vector3 followFocus;
        float currentFollowDistance;
        Vector3 factoryFocus;
        float factoryDistance = 21f;
        float blend;        // 0 = character, 1 = factory
        float blendTarget;

        public CameraMode Mode { get; private set; } = CameraMode.Character;
        public bool IsTransitioning => !Mathf.Approximately(blend, blendTarget);
        public Camera Camera => cam;
        public event Action<CameraMode> ModeChanged;

        public void Init(Camera camera, Transform followTarget)
        {
            cam = camera;
            target = followTarget;
            followFocus = target.position;
            currentFollowDistance = followDistance;
            factoryFocus = new Vector3(0f, 0f, 0.5f);
            SnapToCurrent();
        }

        public void SetMode(CameraMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            blendTarget = mode == CameraMode.Factory ? 1f : 0f;
            if (mode == CameraMode.Factory)
            {
                // Start the overview centred near Hue so the switch feels connected.
                factoryFocus = ClampFocus(Vector3.Lerp(new Vector3(0f, 0f, 0.5f), target.position, 0.35f));
            }
            ModeChanged?.Invoke(mode);
        }

        public void SnapToCurrent()
        {
            blend = blendTarget;
            if (cam == null) return;
            Apply(1000f);
        }

        void LateUpdate()
        {
            if (cam == null || target == null) return;
            Apply(Time.unscaledDeltaTime);
        }

        void Apply(float dt)
        {
            blend = Mathf.MoveTowards(blend, blendTarget, dt / Mathf.Max(0.01f, transitionSeconds));
            float t = blend * blend * (3f - 2f * blend); // smoothstep

            followFocus = Vector3.Lerp(followFocus, target.position + Vector3.forward * lookAhead, 1f - Mathf.Exp(-followSmoothing * dt));
            GetFollowPose(dt, out var cPos, out var cRot);
            GetFactoryPose(out var fPos, out var fRot);

            cam.transform.SetPositionAndRotation(Vector3.Lerp(cPos, fPos, t), Quaternion.Slerp(cRot, fRot, t));
        }

        void GetFollowPose(float dt, out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(followPitch, 0f, 0f);
            Vector3 lookPoint = followFocus + Vector3.up * 0.8f;
            Vector3 back = rot * Vector3.back;

            // Pull the camera in when a wall would block the view.
            float wanted = followDistance;
            int count = Physics.SphereCastNonAlloc(lookPoint, 0.3f, back, hitBuffer, followDistance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hitBuffer[i].collider.GetComponentInParent<CameraOccluder>() != null)
                    wanted = Mathf.Min(wanted, Mathf.Max(3f, hitBuffer[i].distance - 0.2f));
            currentFollowDistance = wanted < currentFollowDistance
                ? wanted
                : Mathf.Lerp(currentFollowDistance, wanted, 1f - Mathf.Exp(-4f * dt));
            pos = lookPoint + back * currentFollowDistance;
        }

        void GetFactoryPose(out Vector3 pos, out Quaternion rot)
        {
            rot = Quaternion.Euler(factoryPitch, factoryYaw, 0f);
            pos = factoryFocus + rot * Vector3.back * factoryDistance;
        }

        /// <summary>Drag in screen pixels moves the overview over the floor.</summary>
        public void Pan(Vector2 screenDelta)
        {
            if (Mode != CameraMode.Factory) return;
            float worldPerPixel = 2f * factoryDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            var yawRot = Quaternion.Euler(0f, factoryYaw, 0f);
            Vector3 right = yawRot * Vector3.right;
            Vector3 fwd = yawRot * Vector3.forward;
            factoryFocus -= (right * screenDelta.x + fwd * screenDelta.y * 1.3f) * worldPerPixel;
            factoryFocus = ClampFocus(factoryFocus);
        }

        public void Zoom(float factor)
        {
            if (Mode != CameraMode.Factory || factor <= 0f) return;
            factoryDistance = Mathf.Clamp(factoryDistance / factor, factoryMinDistance, factoryMaxDistance);
        }

        static Vector3 ClampFocus(Vector3 f)
        {
            return new Vector3(
                Mathf.Clamp(f.x, -WorkshopLayout.HalfWidth + 1f, WorkshopLayout.HalfWidth - 1f),
                0f,
                Mathf.Clamp(f.z, -WorkshopLayout.HalfDepth + 1f, WorkshopLayout.HalfDepth - 1f));
        }

        /// <summary>Point on the floor under a screen position.</summary>
        public bool ScreenToFloor(Vector2 screen, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            point = Vector3.zero;
            return false;
        }
    }
}
