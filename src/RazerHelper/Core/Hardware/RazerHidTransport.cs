using System.ComponentModel;
using System.Runtime.InteropServices;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace RazerHelper.Core.Hardware;

internal sealed class RazerHidTransport : IRazerTransport, IDisposable
{
    private const int RazerVendorId = 0x1532;
    private const int Blade16_2023ProductId = 0x029F;
    private const ushort GetDeviceModeCommand = 0x0084;
    private const int MaximumAttempts = 5;
    private const int ConnectionProbeAttempts = 3;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    // All services share one physical EC command channel. Serializing
    // process-wide keeps fan reads and setting writes from consuming each
    // other's responses when they happen at the same time.
    private static readonly Lock DeviceSyncRoot = new();
    private SafeFileHandle? _deviceHandle;
    private int _featureReportLength;
    private bool _disposed;

    public byte[] Send(ushort command, ReadOnlySpan<byte> arguments)
    {
        lock (DeviceSyncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                EnsureConnected();
                return Exchange(command, arguments);
            }
            catch (RazerCommandNotSupportedException)
            {
                // The device is reachable; keep the connection for other commands.
                throw;
            }
            catch
            {
                Disconnect();
                throw;
            }
        }
    }

    public static bool IsSupportedDevicePresent() =>
        DeviceList.Local
            .GetHidDevices(RazerVendorId, Blade16_2023ProductId)
            .Any();

    public void Dispose()
    {
        lock (DeviceSyncRoot)
        {
            if (_disposed)
                return;

            _disposed = true;
            Disconnect();
        }
    }

    private void EnsureConnected()
    {
        if (_deviceHandle is not null)
            return;

        var foundAnyDevice = false;

        foreach (var device in DeviceList.Local.GetHidDevices(
            RazerVendorId,
            Blade16_2023ProductId))
        {
            foundAnyDevice = true;

            var reportLength = device.GetMaxFeatureReportLength();

            if (reportLength < RazerHidPacket.MinimumFeatureReportLength)
                continue;

            // Windows blocks ordinary read/write handles to some system HID
            // interfaces. A zero-access handle can still exchange feature
            // reports, which is the same fallback used by hidapi on Windows.
            var candidate = CreateFile(
                device.DevicePath,
                desiredAccess: 0,
                shareMode: FileShareRead | FileShareWrite,
                securityAttributes: IntPtr.Zero,
                creationDisposition: OpenExisting,
                flagsAndAttributes: 0,
                templateFile: IntPtr.Zero);

            if (candidate.IsInvalid)
            {
                candidate.Dispose();
                continue;
            }

            _deviceHandle = candidate;
            _featureReportLength = reportLength;

            try
            {
                Exchange(
                    GetDeviceModeCommand,
                    [0x00, 0x00],
                    maximumAttempts: ConnectionProbeAttempts);
                return;
            }
            catch
            {
                Disconnect();
            }
        }

        // Distinguish "wrong machine" from "right machine, interface not
        // answering" so callers and logs can tell them apart.
        if (!foundAnyDevice)
            throw new RazerDeviceNotFoundException(RazerVendorId, Blade16_2023ProductId);

        throw new InvalidOperationException(
            "The Razer Blade 16 (2023) control interface did not respond " +
            $"(VID 0x{RazerVendorId:X4}, PID 0x{Blade16_2023ProductId:X4}).");
    }

    private byte[] Exchange(
        ushort command,
        ReadOnlySpan<byte> arguments,
        int maximumAttempts = MaximumAttempts)
    {
        var deviceHandle = _deviceHandle;
        if (deviceHandle is null || deviceHandle.IsInvalid)
            throw new InvalidOperationException("The Razer HID device is not connected.");

        var request = RazerHidPacket.CreateRequest(
            command,
            arguments,
            _featureReportLength);

        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            Thread.Sleep(1);
            if (!HidD_SetFeature(deviceHandle, request, request.Length))
                throw CreateHidException("send");

            Thread.Sleep(2);
            var response = new byte[_featureReportLength];
            if (!HidD_GetFeature(deviceHandle, response, response.Length))
                throw CreateHidException("receive");

            if (RazerHidPacket.IsMatchingSuccessfulResponse(response, command))
                return response;

            // Retrying cannot change the answer, so fail immediately instead
            // of holding the shared device lock for several retry delays.
            if (RazerHidPacket.IsNotSupportedResponse(response, command))
                throw new RazerCommandNotSupportedException(command);

            if (attempt < maximumAttempts - 1)
            {
                Thread.Sleep(RazerHidPacket.IsBusyResponse(response)
                    ? 20
                    : 500);
            }
        }

        throw new InvalidOperationException(
            $"Razer HID command 0x{command:X4} did not return a valid response.");
    }

    private void Disconnect()
    {
        _deviceHandle?.Dispose();
        _deviceHandle = null;
        _featureReportLength = 0;
    }

    private static Win32Exception CreateHidException(string operation)
    {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(
            error,
            $"Unable to {operation} a Razer HID feature report (Windows error {error}).");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_SetFeature(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetFeature(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);
}
