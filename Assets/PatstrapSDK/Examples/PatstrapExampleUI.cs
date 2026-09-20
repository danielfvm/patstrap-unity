using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Patstrap
{
    /// <summary>
    /// Example
    public class PatstrapExampleUI : MonoBehaviour
    {
        [SerializeField] private Slider sliderStrength;
        [SerializeField] private TMPro.TMP_Text connectionText;

        public void Connect()
        {
            if (PatstrapSdkManager.IsConnected())
            {
                PatstrapSdkManager.Disconnect();
                connectionText.text = "Connect";
            }
            else
            {
                connectionText.text = "Connecting...";
                PatstrapSdkManager.Scan((status) => {
                    EditorApplication.delayCall += () => connectionText.text = status ? "Disconnect" : "Error";
                });
            }
        }

        public void SendHaptic(int motor)
        {
            PatstrapSdkManager.SendHaptic((PatstrapSdkManager.HapticMotor)motor, sliderStrength.value, 1);
        }
    }
}