namespace ObsidianInstaller.Core;

/// <summary>A leitura passiva de um dispositivo USB enumerado pelo Windows.</summary>
public sealed record UsbDeviceObservation(
    string InstanceId,
    string DeviceDescription,
    IReadOnlyList<string> HardwareIds,
    string? Location,
    uint? ProblemCode);

public enum UsbDeviceKind
{
    Unknown,
    FelBootRom,
    TemporaryMassStorage,
    LinuxUsbGadget
}

public sealed record UsbDeviceAssessment(
    UsbDeviceObservation Device,
    string VendorId,
    string ProductId,
    UsbDeviceKind Kind,
    string ModeLabel,
    string CompatibilityLabel,
    string Guidance,
    string DriverLabel,
    IReadOnlyList<HardwareProfileCandidate> CandidateProfiles)
{
    public bool HasUsbId => VendorId != "----";

    public string UsbId => HasUsbId ? $"{VendorId}:{ProductId}" : "sem VID/PID";

    public static UsbDeviceAssessment From(UsbDeviceObservation device, HardwareProfileCatalog profileCatalog)
    {
        var (vendor, product) = ParseIds(device.HardwareIds, device.InstanceId);
        var candidates = profileCatalog.FindCandidates(vendor, product);
        var matchedMode = candidates.FirstOrDefault()?.MatchedMode;
        var kind = matchedMode switch
        {
            "FEL da ROM" => UsbDeviceKind.FelBootRom,
            "Armazenamento USB temporário" => UsbDeviceKind.TemporaryMassStorage,
            "Sistema iniciado" => UsbDeviceKind.LinuxUsbGadget,
            _ => UsbDeviceKind.Unknown
        };

        var (label, guidance) = kind switch
        {
            UsbDeviceKind.FelBootRom => (
                "Modo FEL da ROM",
                "O ID USB corresponde a um modo FEL declarado em um perfil local. O processador e a variante física da placa ainda não foram confirmados."),
            UsbDeviceKind.TemporaryMassStorage => (
                "Armazenamento USB temporário",
                "O ID USB é o armazenamento temporário do console: o cartão de memória está exposto como disco USB. É o modo usado para gravar a imagem na etapa 5."),
            UsbDeviceKind.LinuxUsbGadget => (
                "Gadget USB Linux observado",
                "O ID USB corresponde ao sistema iniciado do console (MTP + serial). Nesse modo o console não expõe o cartão para gravação."),
            _ => (
                "Dispositivo USB sem perfil",
                "O identificador não corresponde a um modo declarado no catálogo local. Nenhuma compatibilidade foi presumida.")
        };

        var compatibility = candidates.Count switch
        {
            0 => "Sem perfil correspondente",
            1 when candidates[0].CustomerInstallationAllowed
                && candidates[0].ValidationLevel == "homologated" => "Candidato homologado; placa não confirmada",
            1 => "Perfil de laboratório (canal lab)",
            _ => "Correspondência ambígua entre perfis"
        };

        if (candidates.Count > 1)
            guidance += " Mais de um perfil declara este ID; a correspondência é ambígua.";
        else if (candidates.Count == 1 && !candidates[0].CustomerInstallationAllowed)
            guidance += $" O perfil {candidates[0].DisplayName} é somente de {candidates[0].ValidationLevel} e não autoriza instalação para clientes.";

        var driver = device.ProblemCode is > 0
            ? $"Windows reportou problema PnP {device.ProblemCode}"
            : "Sem problema PnP reportado";
        if (device.ProblemCode is > 0)
            guidance += " O Windows reportou um problema no dispositivo ou driver; consulte o Gerenciador de Dispositivos.";

        return new UsbDeviceAssessment(device, vendor, product, kind, label, compatibility, guidance, driver, candidates);
    }

    private static (string Vendor, string Product) ParseIds(IReadOnlyList<string> hardwareIds, string instanceId)
    {
        foreach (var id in hardwareIds.Prepend(instanceId))
        {
            var vendorAt = id.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
            var productAt = id.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
            if (vendorAt >= 0 && productAt >= 0 && vendorAt + 8 <= id.Length && productAt + 8 <= id.Length)
                return (id.Substring(vendorAt + 4, 4).ToUpperInvariant(), id.Substring(productAt + 4, 4).ToUpperInvariant());
        }
        return ("----", "----");
    }
}
