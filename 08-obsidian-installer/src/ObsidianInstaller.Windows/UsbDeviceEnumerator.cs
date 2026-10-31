using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ObsidianInstaller.Core;

namespace ObsidianInstaller.Windows;

/// <summary>Enumera USB via SetupAPI. Não envia comandos ao console nem acessa discos.</summary>
public sealed class UsbDeviceEnumerator
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpHardwareId = 0x00000001;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private const uint SpdrpLocationInformation = 0x0000000D;
    private static readonly IntPtr InvalidHandle = new(-1);

    /// <summary>Dispositivos observados sem VID/PID (hubs, controladores) e ignorados.</summary>
    public int SkippedWithoutId { get; private set; }

    public IReadOnlyList<UsbDeviceObservation> Enumerate()
    {
        var set = Native.SetupDiGetClassDevs(IntPtr.Zero, "USB", IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (set == InvalidHandle)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "O Windows não conseguiu consultar a lista de dispositivos USB.");

        try
        {
            var found = new List<UsbDeviceObservation>();
            var skippedWithoutId = 0;
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (uint index = 0; ; index++)
            {
                var info = new SpDevInfoData { CbSize = (uint)Marshal.SizeOf<SpDevInfoData>() };
                if (!Native.SetupDiEnumDeviceInfo(set, index, ref info))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259) // ERROR_NO_MORE_ITEMS
                        break;
                    throw new Win32Exception(error, "A enumeração USB foi interrompida pelo Windows.");
                }

                var instanceId = ReadInstanceId(set, ref info);
                var hardwareIds = ReadRegistryStrings(set, ref info, SpdrpHardwareId);
                if (!instanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) || hardwareIds.Length == 0)
                    continue;
                // Sem VID/PID não há identificação possível (hubs, controladores, dispositivos sem descritor).
                if (!HasVendorProductId(instanceId, hardwareIds))
                {
                    skippedWithoutId++;
                    continue;
                }
                if (!seenIds.Add(instanceId))
                    continue;

                var description = ReadRegistryStrings(set, ref info, SpdrpFriendlyName).FirstOrDefault()
                    ?? ReadRegistryStrings(set, ref info, SpdrpDeviceDesc).FirstOrDefault()
                    ?? instanceId;
                var location = ReadRegistryStrings(set, ref info, SpdrpLocationInformation).FirstOrDefault();
                uint? problemCode = Native.CM_Get_DevNode_Status(out _, out var problem, info.DevInst, 0) == 0 && problem != 0
                    ? problem
                    : null;

                found.Add(new UsbDeviceObservation(instanceId, description, hardwareIds, location, problemCode));
            }

            SkippedWithoutId = skippedWithoutId;
            return found;
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static bool HasVendorProductId(string instanceId, IReadOnlyList<string> hardwareIds)
    {
        foreach (var id in hardwareIds.Prepend(instanceId))
        {
            if (id.Contains("VID_", StringComparison.OrdinalIgnoreCase) &&
                id.Contains("PID_", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string[] ReadRegistryStrings(IntPtr set, ref SpDevInfoData info, uint property)
    {
        Native.SetupDiGetDeviceRegistryProperty(set, ref info, property, out _, null, 0, out var needed);
        if (needed == 0 || needed > 65536)
            return [];
        var buffer = new byte[checked((int)needed)];
        if (!Native.SetupDiGetDeviceRegistryProperty(set, ref info, property, out _, buffer, needed, out _))
            return [];
        return Encoding.Unicode.GetString(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ReadInstanceId(IntPtr set, ref SpDevInfoData info)
    {
        var buffer = new StringBuilder(512);
        return Native.SetupDiGetDeviceInstanceId(set, ref info, buffer, buffer.Capacity, out _)
            ? buffer.ToString()
            : string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevInfoData
    {
        public uint CbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public UIntPtr Reserved;
    }

    private static class Native
    {
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevInfoData deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData,
            uint property, out uint propertyRegDataType, [Out] byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevInfoData deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("cfgmgr32.dll")]
        internal static extern uint CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
    }
}
