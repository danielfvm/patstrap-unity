using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Patstrap;
using UnityEngine;

public class PatstrapSdkManager : MonoBehaviour
{
    PatstrapManager manager;
    PatstrapDevice device;

    private static PatstrapSdkManager instance;

    void Start()
    {
        manager = new PatstrapManager();
        instance = this;
        //PatstrapSdk.Connect();
    }

    public static bool IsConnected() => instance?.device?.IsConnected == true;

    public static void Disconnect()
    {
        if (IsConnected())
            instance.device.Disconnect();
    }

    public static void Scan(Action<bool> onConnect)
    {
        if (instance == null)
            return;
        
        if (instance.device != null && instance.device.IsConnected)
            return;

        Task.Run(() => {
            Debug.Log("Start scanning");

            var devices = instance.manager.Scan();
            Debug.Log("Devices Found: " + devices.Count);
            
            if (devices.Count > 0)
            {
                try
                {
                    instance.device = devices[0];
                    instance.device.Connect();
                    List<PatstrapHaptic> haptics = instance.device.GetHaptics();

                    Debug.Log($"[PatstrapSDK] Connected to device {haptics.Count} haptic(s).");
                    onConnect(true);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PatstrapSDK] {e.Message}");
                    onConnect(false);
                }
            }
            else
            {
                onConnect(false);
            }
        });
    }

    public enum HapticMotor
    {
        FrontLeft = 6,
        FrontRight = 2,

        BackLeft = 4,
        BackRight = 0,
    }

    public static void SendHaptic(HapticMotor motor, float strength, float seconds)
    {
        if (instance == null)
            return;

        if (instance.device == null || !instance.device.IsConnected)
            return;

        if (strength <= 0.05f)
            return;

        instance.device.Vibrate((byte)motor, strength, (uint)(seconds * 1000.0));
    }

    void OnApplicationQuit()
    {
        if (device != null && device.IsConnected)
            device.Disconnect();
    }
}
