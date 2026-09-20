using System;
using UnityEngine;
using UnityEngine.UI;


namespace Patstrap
{
    /// <summary>
    /// Example
    public class PatstrapExampleHeadHaptic : MonoBehaviour
    {
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable pointer;
        [SerializeField] private float delay = 0.05f;
        [SerializeField] private Material material;
        [SerializeField] private Transform player;
        [SerializeField] private bool withDistance;

        private bool active;
        private float time;

        private void Start()
        {
            pointer.activated.AddListener((_) =>
            {
                active = !active;
                material.SetColor("_BaseColor", active ? Color.red : Color.green);

                if (!active)
                {
                    PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, 0.0f, 0.1f);
                    PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, 0.0f, 0.1f);
                    PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, 0.0f, 0.1f);
                    PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, 0.0f, 0.1f);
                }
            });
        }

        private void Update()
        {
            if (active)
            {
                time += Time.deltaTime;
                if (time > delay)
                {
                    if (withDistance)
                    {
                        Vector3 toTarget = transform.position - player.position;
                        float distance = toTarget.magnitude;

                        // Define your thresholds
                        float maxDistance = 5.0f; // Distance where haptics start fading in
                        float minDistance = 0.5f; // Distance inside of which ALL motors fire at full strength

                        // Calculate base distance intensity: 1 when close/inside, 0 when far away
                        float distanceIntensity = Mathf.Clamp01(1.0f - (distance - minDistance) / (maxDistance - minDistance));

                        // If the player is right on top or inside the object, force distance intensity to 1 for all motors
                        if (distance <= minDistance)
                        {
                            distanceIntensity = 1.0f;
                        }

                        Vector3 directionToTarget = toTarget.normalized;

                        Vector3 camForward = player.forward;
                        camForward.y = 0;
                        camForward.Normalize();

                        Vector3 camRight = player.right;
                        camRight.y = 0;
                        camRight.Normalize();

                        float forwardAmount = Vector3.Dot(camForward, directionToTarget); // Positive = Front, Negative = Back
                        float rightAmount = Vector3.Dot(camRight, directionToTarget);     // Positive = Right, Negative = Left

                        // Calculate directional weights
                        float fl = Mathf.Clamp01(forwardAmount - rightAmount);
                        float fr = Mathf.Clamp01(forwardAmount + rightAmount);
                        float bl = Mathf.Clamp01(-forwardAmount - rightAmount);
                        float br = Mathf.Clamp01(-forwardAmount + rightAmount);

                        float omniFactor = Mathf.Clamp01(1.0f - (distance / minDistance));

                        fl = Mathf.Lerp(fl, 1.0f, omniFactor);
                        fr = Mathf.Lerp(fr, 1.0f, omniFactor);
                        bl = Mathf.Lerp(bl, 1.0f, omniFactor);
                        br = Mathf.Lerp(br, 1.0f, omniFactor);

                        time = 0;
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, distanceIntensity * fl, 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, distanceIntensity * fr, 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, distanceIntensity * bl, 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, distanceIntensity * br, 2f);
                    }
                    else
                    {
                        Vector3 directionToTarget = (transform.position - player.position).normalized;

                        Vector3 camForward = player.forward;
                        camForward.y = 0;
                        camForward.Normalize();

                        Vector3 camRight = player.right;
                        camRight.y = 0;
                        camRight.Normalize();

                        float forwardAmount = Vector3.Dot(camForward, directionToTarget); // Positive = Front, Negative = Back
                        float rightAmount = Vector3.Dot(camRight, directionToTarget);     // Positive = Right, Negative = Left

                        time = 0;
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, Mathf.Clamp01(forwardAmount - rightAmount), 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, Mathf.Clamp01(forwardAmount + rightAmount), 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, Mathf.Clamp01(-forwardAmount - rightAmount), 2f);
                        PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, Mathf.Clamp01(-forwardAmount + rightAmount), 2f);
                    }
                }
            }
        }
    }
}
