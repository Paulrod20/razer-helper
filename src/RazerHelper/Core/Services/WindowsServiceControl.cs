using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace RazerHelper.Core.Services;

/// <summary>
/// The real thing: the Windows service control manager. Reading needs no
/// special rights; stopping, starting and changing startup types need
/// administrator rights, so those run in the elevated helper process.
/// </summary>
internal sealed class WindowsServiceControl : IServiceControl
{
    private static readonly TimeSpan StopStartTimeout = TimeSpan.FromSeconds(30);

    public IReadOnlyList<ServiceState> Find(Func<string, string, bool> matches)
    {
        var found = new List<ServiceState>();

        foreach (var service in ServiceController.GetServices())
        {
            using (service)
            {
                if (!matches(service.ServiceName, service.DisplayName))
                    continue;

                found.Add(new ServiceState(
                    service.ServiceName,
                    service.DisplayName,
                    service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending,
                    service.StartType));
            }
        }

        return found.OrderBy(service => service.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void Stop(string name)
    {
        using var controller = new ServiceController(name);
        StopWithDependents(controller);
    }

    public void Start(string name)
    {
        using var controller = new ServiceController(name);
        controller.Refresh();

        if (controller.Status == ServiceControllerStatus.Running)
            return;

        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, StopStartTimeout);
    }

    // Windows refuses to stop a service while services that depend on it are
    // running, so those go first (as PowerShell's Stop-Service -Force does).
    private static void StopWithDependents(ServiceController controller)
    {
        controller.Refresh();

        if (controller.Status == ServiceControllerStatus.Stopped)
            return;

        foreach (var dependent in controller.DependentServices)
        {
            using (dependent)
                StopWithDependents(dependent);
        }

        controller.Refresh();

        if (controller.Status != ServiceControllerStatus.StopPending)
            controller.Stop();

        controller.WaitForStatus(ServiceControllerStatus.Stopped, StopStartTimeout);
    }

    public void SetStartMode(string name, ServiceStartMode mode)
    {
        // ServiceController can read the startup type but not change it, so
        // this calls the service control manager directly. The numeric values
        // of ServiceStartMode are the same as Windows' SERVICE_*_START.
        var manager = OpenSCManager(null, null, ScManagerConnect);

        if (manager == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var service = OpenService(manager, name, ServiceChangeConfig);

            if (service == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var changed = ChangeServiceConfig(
                    service,
                    ServiceNoChange,       // service type
                    (uint)mode,            // start type
                    ServiceNoChange,       // error control
                    null, null, IntPtr.Zero, null, null, null, null);

                if (!changed)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr manager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
}
