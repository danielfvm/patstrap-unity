using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Patstrap
{
    internal static class Native
    {
        // Resolve the native library name per target platform.
#if UNITY_STANDALONE_WIN
        private const string Lib = "patstrap_sdk";
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
        private const string Lib = "patstrap_sdk";
#else
        private const string Lib = "__Internal"; // WebGL static linkage
#endif

        // PatstrapHandle

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr patstrap_new();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void patstrap_free(IntPtr handle);

        // Scanning

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr patstrap_scan(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr patstrap_device_list_len(IntPtr list);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr patstrap_device_list_get(IntPtr list, UIntPtr index);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void patstrap_device_list_free(IntPtr list);

        // DeviceHandle

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr patstrap_device_get_name(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr patstrap_device_get_addr(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_connect(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_disconnect(IntPtr handle);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_get_battery(IntPtr handle, out float battery);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_get_rssi(IntPtr handle, out short rssi);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_get_haptic_count(IntPtr handle, out byte count);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int patstrap_device_vibrate(IntPtr handle, byte id, float strength, uint durationMs);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void patstrap_device_free(IntPtr handle);

        // String helpers

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void patstrap_string_free(IntPtr s);

        // Utility

        /// <summary>
        /// Marshal a native UTF-8 string pointer into a managed string and
        /// then release the native memory with <c>patstrap_string_free</c>.
        /// Returns <c>null</c> if <paramref name="ptr"/> is zero.
        /// </summary>
        public static string MarshalAndFreeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUTF8(ptr);
            }
            finally
            {
                patstrap_string_free(ptr);
            }
        }
    }

    // PatstrapManager

    /// <summary>
    /// Entry point for the Patstrap SDK.  Initialises the BLE subsystem and
    /// provides device discovery.
    /// </summary>
    /// <remarks>
    /// Always dispose via <c>using</c> or an explicit <see cref="Dispose"/>
    /// call; the finaliser will clean up as a last resort.
    /// </remarks>
    public sealed class PatstrapManager : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        /// <summary>
        /// Initialises the BLE subsystem.
        /// </summary>
        /// <exception cref="PatstrapException">
        /// Thrown when the native library fails to initialise.
        /// </exception>
        public PatstrapManager()
        {
            _handle = Native.patstrap_new();
            if (_handle == IntPtr.Zero)
                throw new PatstrapException("Failed to initialise Patstrap BLE manager.");
        }

        /// <summary>
        /// Scan for visible Patstrap BLE devices.
        /// </summary>
        /// <returns>
        /// A list of <see cref="PatstrapDevice"/> objects — each caller is
        /// responsible for disposing them when done.
        /// </returns>
        /// <exception cref="PatstrapException">Thrown on scan failure.</exception>
        /// <exception cref="ObjectDisposedException">If this manager is disposed.</exception>
        public List<PatstrapDevice> Scan()
        {
            ThrowIfDisposed();

            IntPtr listPtr = Native.patstrap_scan(_handle);
            if (listPtr == IntPtr.Zero)
                throw new PatstrapException("BLE scan failed.");

            try
            {
                int count = (int)Native.patstrap_device_list_len(listPtr).ToUInt32();
                var devices = new List<PatstrapDevice>(count);

                for (int i = 0; i < count; i++)
                {
                    // Always extract at index 0: swap-remove shifts remaining
                    // elements, so the "next" device is always at 0.
                    IntPtr devicePtr = Native.patstrap_device_list_get(listPtr, (UIntPtr)0);
                    if (devicePtr != IntPtr.Zero)
                        devices.Add(new PatstrapDevice(devicePtr));
                }

                return devices;
            }
            finally
            {
                // Free the (now empty) list container.
                Native.patstrap_device_list_free(listPtr);
            }
        }

        // IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PatstrapManager() => Dispose(false);

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (_handle != IntPtr.Zero)
            {
                Native.patstrap_free(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PatstrapManager));
        }
    }

    // PatstrapDevice

    /// <summary>
    /// Represents a single Patstrap BLE device discovered during a scan.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Connect"/> before accessing connection-dependent
    /// properties such as battery level or haptic motors.
    /// </remarks>
    public sealed class PatstrapDevice : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;
        private bool _connected;

        // Cached after Connect() so subsequent reads are zero-copy.
        private byte _hapticCount;

        internal PatstrapDevice(IntPtr handle)
        {
            _handle = handle;
        }

        // Identity

        /// <summary>Human-readable device name, or <c>null</c> if unavailable.</summary>
        public string Name
        {
            get
            {
                ThrowIfDisposed();
                return Native.MarshalAndFreeString(Native.patstrap_device_get_name(_handle));
            }
        }

        /// <summary>Bluetooth address string (e.g. <c>"AA:BB:CC:DD:EE:FF"</c>).</summary>
        public string Address
        {
            get
            {
                ThrowIfDisposed();
                return Native.MarshalAndFreeString(Native.patstrap_device_get_addr(_handle));
            }
        }

        // Connection

        /// <summary>Whether the device is currently connected.</summary>
        public bool IsConnected => _connected && !_disposed;

        /// <summary>
        /// Connect to the device and start the BLE notification pipeline.
        /// Reads the haptic motor count immediately after connecting.
        /// </summary>
        /// <exception cref="PatstrapException">Thrown on failure.</exception>
        public void Connect()
        {
            ThrowIfDisposed();
            if (Native.patstrap_device_connect(_handle) != 0)
                throw new PatstrapException($"Failed to connect to device \"{Name}\".");

            _connected = true;

            // Cache haptic count so Haptics is allocation-free after the first call.
            byte count;
            if (Native.patstrap_device_get_haptic_count(_handle, out count) == 0)
                _hapticCount = count;
        }

        /// <summary>Disconnect from the device.</summary>
        /// <exception cref="PatstrapException">Thrown on failure.</exception>
        public void Disconnect()
        {
            ThrowIfDisposed();
            if (Native.patstrap_device_disconnect(_handle) != 0)
                throw new PatstrapException("Failed to disconnect from device.");
            _connected = false;
        }

        // Device state

        /// <summary>
        /// Returns the last-known battery level in the range [0, 1].
        /// Requires an active connection.
        /// </summary>
        /// <exception cref="PatstrapException">Thrown when not connected or on read failure.</exception>
        public float GetBattery()
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            if (Native.patstrap_device_get_battery(_handle, out float value) != 0)
                throw new PatstrapException("Failed to read battery level.");
            return value;
        }

        /// <summary>
        /// Returns the RSSI of the device in dBm.
        /// Requires an active connection.
        /// </summary>
        /// <exception cref="PatstrapException">Thrown when not connected or on read failure.</exception>
        public short GetRssi()
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            if (Native.patstrap_device_get_rssi(_handle, out short rssi) != 0)
                throw new PatstrapException("Failed to read RSSI.");
            return rssi;
        }

        /// <summary>
        /// Number of haptic motors on this device (cached after <see cref="Connect"/>).
        /// </summary>
        public byte HapticCount => _hapticCount;

        /// <summary>
        /// Returns a list of <see cref="PatstrapHaptic"/> motor views.
        /// Each call creates a new list; cache the result if performance matters.
        /// </summary>
        public List<PatstrapHaptic> GetHaptics()
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            var haptics = new List<PatstrapHaptic>(_hapticCount);
            for (byte i = 0; i < _hapticCount; i++)
                haptics.Add(new PatstrapHaptic(this, i));
            return haptics;
        }

        /// <summary>Returns a view for a specific haptic motor by zero-based ID.</summary>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public PatstrapHaptic GetHaptic(byte id)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();
            if (id >= _hapticCount)
                throw new ArgumentOutOfRangeException(nameof(id), $"Motor ID {id} exceeds motor count {_hapticCount}.");
            return new PatstrapHaptic(this, id);
        }

        // Vibration

        /// <summary>
        /// Trigger a vibration on a specific haptic motor.
        /// </summary>
        /// <param name="motorId">Zero-based motor ID.</param>
        /// <param name="strength">Strength in [0, 1]. Values are clamped.</param>
        /// <param name="durationMs">Duration in milliseconds (max 65 535).</param>
        /// <exception cref="PatstrapException">Thrown on failure.</exception>
        public void Vibrate(byte motorId, float strength, uint durationMs)
        {
            ThrowIfDisposed();
            ThrowIfNotConnected();

            strength = Math.Min(Math.Max(strength, 0), 1);
            if (Native.patstrap_device_vibrate(_handle, motorId, strength, durationMs) != 0)
                throw new PatstrapException($"Vibrate command failed for motor {motorId}.");
        }

        // IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PatstrapDevice() => Dispose(false);

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (_handle != IntPtr.Zero)
            {
                // patstrap_device_free disconnects if still connected.
                Native.patstrap_device_free(_handle);
                _handle = IntPtr.Zero;
            }
            _connected = false;
            _disposed = true;
        }

        // Guards

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PatstrapDevice));
        }

        private void ThrowIfNotConnected()
        {
            if (!_connected)
                throw new PatstrapException("Device is not connected. Call Connect() first.");
        }
    }

    // PatstrapHaptic

    /// <summary>
    /// Lightweight view of a single haptic motor on a <see cref="PatstrapDevice"/>.
    /// Does not own any native memory; the parent device must stay alive.
    /// </summary>
    public sealed class PatstrapHaptic
    {
        private readonly PatstrapDevice _device;

        /// <summary>Zero-based motor identifier.</summary>
        public byte Id { get; }

        internal PatstrapHaptic(PatstrapDevice device, byte id)
        {
            _device = device;
            Id = id;
        }

        /// <summary>Trigger a vibration on this motor.</summary>
        /// <param name="strength">Strength in [0, 1].</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        public void Vibrate(float strength, uint durationMs) =>
            _device.Vibrate(Id, strength, durationMs);

        /// <summary>
        /// Apply squared (perceptual) scaling before vibrating — feels more
        /// linear to the wearer when mapping 0–1 slider values.
        /// </summary>
        /// <param name="strength">Linear input strength in [0, 1].</param>
        /// <param name="durationMs">Duration in milliseconds.</param>
        public void VibrateLinear(float strength, uint durationMs) =>
            _device.Vibrate(Id, strength * strength, durationMs);

        /// <summary>
        /// Play a short impact pulse: full strength for 50 ms.
        /// </summary>
        public void Impact() => _device.Vibrate(Id, 1f, 50);
    }

    // PatstrapException

    /// <summary>
    /// Thrown when a Patstrap SDK operation fails.
    /// </summary>
    public sealed class PatstrapException : Exception
    {
        public PatstrapException(string message) : base(message) { }
        public PatstrapException(string message, Exception inner) : base(message, inner) { }
    }
}