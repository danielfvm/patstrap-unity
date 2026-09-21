using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Patstrap
{
    /// <summary>
    /// Example
    public class PatstrapExampleSine : MonoBehaviour
    {
        [SerializeField] private float delay = 0.1f;

        private float time;

        private void Update()
        {
            time += Time.deltaTime;
            if (time > delay)
            {
                time -= delay;

                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontLeft, Mathf.Pow(Mathf.Sin(Time.time) * 0.5f + 0.5f, 2f), delay * 8.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.FrontRight, Mathf.Pow(Mathf.Sin(Time.time + 3.1415f * 0.5f) * 0.5f + 0.5f, 2f), delay * 8.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackRight, Mathf.Pow(Mathf.Sin(Time.time + 3.1415f) * 0.5f + 0.5f, 2f), delay * 8.0f);
                PatstrapSdkManager.SendHaptic(PatstrapSdkManager.HapticMotor.BackLeft, Mathf.Pow(Mathf.Sin(Time.time + 3.1415f * 1.5f) * 0.5f + 0.5f, 2f), delay * 8.0f);
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