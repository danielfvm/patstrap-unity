using UnityEngine;

namespace Patstrap
{
    /// <summary>
    /// Vibrates toward the side of the boundary the player is trying to cross.
    /// Attach this to the boundary cube and use either a trigger or solid collider.
    /// </summary>
    public class PatstrapExampleBoundaryHaptic : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float pulseInterval = 0.1f;
        [SerializeField] private float pulseDuration = 0.4f;
        [SerializeField] private float strength = 1.0f;

        private float nextPulseTime;
        private Collider boundaryCollider;

        private void Awake()
        {
            boundaryCollider = GetComponent<Collider>();
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayerCollider(other))
                VibrateTowardBoundary();
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsPlayerCollider(collision.collider))
                VibrateTowardBoundary();
        }

        private bool IsPlayerCollider(Collider other)
        {
            return head != null && (other.transform == head || other.transform.IsChildOf(head) || head.IsChildOf(other.transform));
        }

        private void VibrateTowardBoundary()
        {
            if (Time.time < nextPulseTime || boundaryCollider == null)
                return;

            Vector3 boundaryPoint = boundaryCollider.ClosestPoint(head.position);
            Vector3 boundaryDirection = boundaryPoint - head.position;
            if (boundaryDirection.sqrMagnitude <= Mathf.Epsilon)
                boundaryDirection = head.position - transform.position;

            if (boundaryDirection.sqrMagnitude <= Mathf.Epsilon)
                return;

            nextPulseTime = Time.time + pulseInterval;

            Vector3 localDirection = head.InverseTransformDirection(boundaryDirection.normalized);
            float front = Mathf.Max(0.0f, localDirection.z);
            float back = Mathf.Max(0.0f, -localDirection.z);
            float right = Mathf.Max(0.0f, localDirection.x);
            float left = Mathf.Max(0.0f, -localDirection.x);

            float pulseStrength = Mathf.Clamp01(strength);
            SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, front + left, pulseStrength);
            SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, front + right, pulseStrength);
            SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, back + left, pulseStrength);
            SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, back + right, pulseStrength);
        }

        private void SendHaptic(PatstrapSdkManager.HapticMotor motor, float directionStrength, float pulseStrength)
        {
            float motorStrength = Mathf.Clamp01(directionStrength) * pulseStrength;
            PatstrapSdkManager.SendHaptic(motor, motorStrength, pulseDuration);
        }
    }
}
