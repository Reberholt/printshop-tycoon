using UnityEngine;

namespace PrintshopTycoon
{
    public enum CharacterPose { Idle, Walk, Work, Celebrate }

    /// <summary>
    /// Procedural animation for the code-built rig: idle breathing, walk cycle with arm/leg
    /// swing, arms forward while carrying, pumping arms while working, a little jump to celebrate.
    /// No Animator/Mecanim needed; swap for real animation clips together with real models.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        Transform visual, armL, armR, legL, legR, head, hips;
        float phase;
        float celebrateUntil;
        Vector3 baseScale = Vector3.one;

        public CharacterPose Pose { get; set; }
        /// <summary>0..1 walk speed.</summary>
        public float Speed { get; set; }
        public bool Carrying { get; set; }

        void Awake()
        {
            visual = transform.Find("Visual");
            if (visual == null) return;
            baseScale = visual.localScale;
            armL = visual.Find("ArmL"); armR = visual.Find("ArmR");
            legL = visual.Find("LegL"); legR = visual.Find("LegR");
            head = visual.Find("Head"); hips = visual.Find("Hips");
        }

        public void Celebrate(float seconds = 1.2f) => celebrateUntil = Time.time + seconds;

        void LateUpdate()
        {
            if (visual == null) return;
            var pose = Time.time < celebrateUntil ? CharacterPose.Celebrate : Pose;
            float dt = Time.deltaTime;
            float bob = 0f, armSwing = 0f, legSwing = 0f, armBase = 0f, headTilt = 0f, lean = 0f, squash = 0f;

            switch (pose)
            {
                case CharacterPose.Walk:
                    phase += dt * Mathf.Lerp(8f, 13f, Speed);
                    float s = Mathf.Sin(phase);
                    legSwing = s * 32f * Mathf.Max(0.4f, Speed);
                    armSwing = -s * 30f * Mathf.Max(0.4f, Speed);
                    bob = Mathf.Abs(Mathf.Cos(phase)) * 0.07f;
                    lean = 6f * Speed;
                    headTilt = Mathf.Sin(phase * 0.5f) * 3f;
                    break;
                case CharacterPose.Work:
                    phase += dt * 9f;
                    armBase = 55f + Mathf.Sin(phase) * 18f;
                    bob = Mathf.Max(0f, Mathf.Sin(phase)) * 0.035f;
                    headTilt = 8f;
                    lean = 8f;
                    break;
                case CharacterPose.Celebrate:
                    phase += dt * 10f;
                    bob = Mathf.Abs(Mathf.Sin(phase)) * 0.22f;
                    armBase = -150f;
                    armSwing = Mathf.Sin(phase * 2f) * 15f;
                    break;
                default:
                    phase += dt * 2.2f;
                    squash = Mathf.Sin(phase) * 0.015f;
                    armSwing = Mathf.Sin(phase) * 3f;
                    headTilt = Mathf.Sin(phase * 0.5f) * 4f;
                    break;
            }

            if (Carrying && pose != CharacterPose.Celebrate)
            {
                armBase = 70f;   // both arms forward, holding the stack
                armSwing *= 0.15f;
            }

            visual.localPosition = Vector3.Lerp(visual.localPosition, new Vector3(0f, bob, 0f), 1f - Mathf.Exp(-20f * dt));
            visual.localScale = Vector3.Scale(baseScale, new Vector3(1f - squash, 1f + squash, 1f - squash));
            Rotate(legL, legSwing, 0f, dt);
            Rotate(legR, -legSwing, 0f, dt);
            Rotate(armL, armBase + armSwing, pose == CharacterPose.Celebrate ? -20f : 0f, dt);
            Rotate(armR, armBase - armSwing, pose == CharacterPose.Celebrate ? 20f : 0f, dt);
            // Negative X tips upright parts toward the face (-z in the rig), i.e. forward.
            Rotate(head, -headTilt * 0.3f, headTilt, dt);
            Rotate(hips, -lean * 0.5f, 0f, dt);
        }

        static void Rotate(Transform t, float x, float z, float dt)
        {
            if (t == null) return;
            var target = Quaternion.Euler(x, 0f, z);
            t.localRotation = Quaternion.Slerp(t.localRotation, target, 1f - Mathf.Exp(-18f * dt));
        }
    }
}
