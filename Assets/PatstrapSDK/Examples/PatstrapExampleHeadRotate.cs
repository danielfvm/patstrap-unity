using UnityEngine;

namespace Patstrap
{
    /// <summary>
    /// Vibrates the front motor on the side the head rotates toward.
    /// </summary>
    public class PatstrapExampleHeadRotate : MonoBehaviour
    {
        [SerializeField] private float delay = 0.1f;
        [SerializeField] private float minimumRotationSpeed = 1.0f;
        [SerializeField] private float maximumRotationSpeed = 180.0f;
        [SerializeField] private Transform head;

        private float time;
        private float previousYaw;

        private void Start()
        {
            if (head != null)
                previousYaw = head.localEulerAngles.y;
        }

        private void Update()
        {
            if (head == null)
                return;

            time += Time.deltaTime;
            if (time < delay)
                return;

            float sampleDuration = time;
            time = 0.0f;

            float currentYaw = head.localEulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
            previousYaw = currentYaw;

            float rotationSpeed = yawDelta / sampleDuration;
            if (Mathf.Abs(rotationSpeed) <= minimumRotationSpeed)
                return;

            float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(rotationSpeed) / maximumRotationSpeed);
            float leftStrength = rotationSpeed < 0.0f ? normalizedSpeed : 0.0f;
            float rightStrength = rotationSpeed > 0.0f ? normalizedSpeed : 0.0f;

            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, leftStrength, delay * 4.0f);
            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, rightStrength, delay * 4.0f);
        }
    }
}
