using UnityEngine;

namespace DungeonsNShops
{
    internal sealed class ActorMotion
    {
        private readonly Transform rig, body, leftArm, rightArm, leftLeg, rightLeg;
        private Vector3 previous;
        private bool visible;
        private float speed, stride;

        public ActorMotion(Transform rig, Transform body, Transform leftArm, Transform rightArm,
            Transform leftLeg, Transform rightLeg)
        {
            this.rig = rig;
            this.body = body;
            this.leftArm = leftArm;
            this.rightArm = rightArm;
            this.leftLeg = leftLeg;
            this.rightLeg = rightLeg;
        }

        public void Update(Vector3 position, bool walking, bool crouched, bool guarding, float windup, float swing, float dt)
        {
            float traveled = Map.Distance(previous, position);
            if (!visible || traveled > 1.8f) traveled = 0;
            previous = position;
            visible = true;
            speed = Mathf.MoveTowards(speed, walking ? 1 : Mathf.Clamp01(traveled / Mathf.Max(0.001f, dt * 2)), dt * 9);
            stride += dt * (crouched ? 6 : 9);
            float gait = Mathf.Sin(stride) * speed;
            float idle = Mathf.Sin(Time.time * 2.5f) * 0.009f;
            rig.localScale = Vector3.Lerp(rig.localScale, new Vector3(1, crouched ? 0.67f : 1, 1), dt * 12);
            rig.localPosition = new Vector3(0, (rig.localScale.y - 1) * 0.95f + Mathf.Abs(gait) * 0.045f + idle, 0);
            float strike = Mathf.Sin(Mathf.Clamp01(swing / 0.36f) * Mathf.PI);
            body.localRotation = Quaternion.Euler(crouched ? 15 : 0, gait * 4 - strike * 14, 0);
            leftLeg.localRotation = Quaternion.Euler(gait * 31, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-gait * 31, 0, 0);
            leftArm.localRotation = Quaternion.Euler(guarding ? -74 : -gait * 25, 0, guarding ? -17 : -11);
            rightArm.localRotation = Quaternion.Euler(gait * 25 - windup * 105 - strike * 110, strike * 34, 12);
        }

        public void Hide() { visible = false; speed = 0; }
    }
}
