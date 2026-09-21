using UnityEngine;

namespace Patstrap
{
    /// <summary>
    /// Example
    public class PatstrapExampleHeadMove : MonoBehaviour
    {
        [SerializeField] private float delay = 0.1f;
        [SerializeField] private float minimumMovement = 0.001f;
        [SerializeField] private Transform head;

        private float time;
        private Vector3 previousHeadPosition;

        private void Start()
        {
            if (head != null)
                previousHeadPosition = head.position;
        }

        private void Update()
        {
            if (head == null)
                return;

            time += Time.deltaTime;
            if (time > delay)
            {
                time -= delay;

                Vector3 worldMovement = head.position - previousHeadPosition;
                previousHeadPosition = head.position;

                Vector3 headMovement = head.InverseTransformDirection(worldMovement) * 5.0f;
                headMovement.y = 0.0f;

                if (headMovement.sqrMagnitude <= minimumMovement * minimumMovement)
                    return;

                float forward = Mathf.Max(0.0f, headMovement.z);
                float backward = Mathf.Max(0.0f, -headMovement.z);
                float right = Mathf.Max(0.0f, headMovement.x);
                float left = Mathf.Max(0.0f, -headMovement.x);

                float frontLeft = Mathf.Clamp01(forward + left);
                float frontRight = Mathf.Clamp01(forward + right);
                float backLeft = Mathf.Clamp01(backward + left);
                float backRight = Mathf.Clamp01(backward + right);

                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, frontLeft, delay * 4.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, frontRight, delay * 4.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, backRight, delay * 4.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, backLeft, delay * 4.0f);
            }
        }

        private void OnDisable()
        {
            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, 0f, 0f);
            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, 0f, 0f);
            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, 0f, 0f);
            PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, 0f, 0f);
        }
    }
}