namespace ObsidianInstaller.Core;

public enum InstallFlowState
{
    Idle,
    Detecting,
    NeedsDriver,
    NeedsProfileEvidence,
    ProfileVerified,
    PackageVerified,
    AwaitingBootstrapConsent,
    BootstrapLoading,
    AwaitingStorage,
    TargetVerified,
    AwaitingEraseConsent,
    AwaitingElevation,
    Writing,
    ReadbackVerifying,
    PreparingStorage,
    Ejecting,
    AwaitingPowerCycle,
    BootVerifying,
    Succeeded,
    Failed,
    Cancelled,
    NeedsRecovery
}

public enum InstallEvidence
{
    SocMatches,
    BoardAndPanelEvidenceAccepted,
    SupportedProfileSelected,
    CompatibleProfile,
    ImageHashMatches,
    PackageSignatureVerifiedForCustomerRelease,
    ProfileAndPackageStillValid,
    RamLoadConsentRecorded,
    FelDeviceSessionMatches,
    UsbTransitionCorrelated,
    OneUnambiguousRemovableTarget,
    SystemDiskExcluded,
    CapacitySufficient,
    TargetIdentityCaptured,
    EraseConsentMatchesPlan,
    UacAccepted,
    ImmutablePlanVerifiedByHelper,
    TargetIdentityRechecked,
    ExclusiveVolumeLock,
    PackageHashRechecked,
    FullImageReadbackHashMatches,
    StorageLayoutSupported,
    ChangesLimitedToAuthorizedTarget,
    RealPowerCycleRequested,
    NewBootIdentityMatchesSession,
    BootReportMatchesInstallationId,
    SystemVersionMatchesPackage,
    RequestedGameDirectoriesConfirmed
}

public sealed record InstallWorkflowTransition(
    bool Allowed,
    InstallFlowState PreviousState,
    InstallFlowState CurrentState,
    IReadOnlyList<InstallEvidence> MissingEvidence,
    string Message);

/// <summary>
/// In-process gate for the documented installation sequence. Consequential
/// operations must still revalidate the plan and target in the elevated helper.
/// </summary>
public sealed class InstallationWorkflow
{
    private static readonly IReadOnlyDictionary<InstallFlowState, InstallFlowState[]> Transitions =
        new Dictionary<InstallFlowState, InstallFlowState[]>
        {
            [InstallFlowState.Idle] = [InstallFlowState.Detecting, InstallFlowState.Cancelled],
            [InstallFlowState.Detecting] = [InstallFlowState.NeedsDriver, InstallFlowState.NeedsProfileEvidence,
                InstallFlowState.ProfileVerified, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.NeedsDriver] = [InstallFlowState.Detecting, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.NeedsProfileEvidence] = [InstallFlowState.Detecting, InstallFlowState.ProfileVerified,
                InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.ProfileVerified] = [InstallFlowState.PackageVerified, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.PackageVerified] = [InstallFlowState.AwaitingBootstrapConsent, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.AwaitingBootstrapConsent] = [InstallFlowState.BootstrapLoading, InstallFlowState.Cancelled, InstallFlowState.Failed],
            [InstallFlowState.BootstrapLoading] = [InstallFlowState.AwaitingStorage, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.AwaitingStorage] = [InstallFlowState.TargetVerified, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.TargetVerified] = [InstallFlowState.AwaitingEraseConsent, InstallFlowState.Failed, InstallFlowState.Cancelled],
            [InstallFlowState.AwaitingEraseConsent] = [InstallFlowState.AwaitingElevation, InstallFlowState.Cancelled, InstallFlowState.Failed],
            [InstallFlowState.AwaitingElevation] = [InstallFlowState.Writing, InstallFlowState.Cancelled, InstallFlowState.Failed],
            [InstallFlowState.Writing] = [InstallFlowState.ReadbackVerifying, InstallFlowState.NeedsRecovery],
            [InstallFlowState.ReadbackVerifying] = [InstallFlowState.PreparingStorage, InstallFlowState.NeedsRecovery],
            [InstallFlowState.PreparingStorage] = [InstallFlowState.Ejecting, InstallFlowState.NeedsRecovery],
            [InstallFlowState.Ejecting] = [InstallFlowState.AwaitingPowerCycle, InstallFlowState.NeedsRecovery],
            [InstallFlowState.AwaitingPowerCycle] = [InstallFlowState.BootVerifying, InstallFlowState.NeedsRecovery],
            [InstallFlowState.BootVerifying] = [InstallFlowState.Succeeded, InstallFlowState.NeedsRecovery],
            [InstallFlowState.Succeeded] = [],
            [InstallFlowState.Failed] = [],
            [InstallFlowState.Cancelled] = [],
            [InstallFlowState.NeedsRecovery] = []
        };

    private static readonly IReadOnlyDictionary<InstallFlowState, InstallEvidence[]> RequiredEvidence =
        new Dictionary<InstallFlowState, InstallEvidence[]>
        {
            [InstallFlowState.ProfileVerified] = [InstallEvidence.SocMatches,
                InstallEvidence.BoardAndPanelEvidenceAccepted, InstallEvidence.SupportedProfileSelected],
            [InstallFlowState.PackageVerified] = [InstallEvidence.CompatibleProfile, InstallEvidence.ImageHashMatches,
                InstallEvidence.PackageSignatureVerifiedForCustomerRelease],
            [InstallFlowState.BootstrapLoading] = [InstallEvidence.ProfileAndPackageStillValid,
                InstallEvidence.RamLoadConsentRecorded, InstallEvidence.FelDeviceSessionMatches],
            [InstallFlowState.TargetVerified] = [InstallEvidence.UsbTransitionCorrelated,
                InstallEvidence.OneUnambiguousRemovableTarget, InstallEvidence.SystemDiskExcluded,
                InstallEvidence.CapacitySufficient, InstallEvidence.TargetIdentityCaptured],
            [InstallFlowState.Writing] = [InstallEvidence.EraseConsentMatchesPlan, InstallEvidence.UacAccepted,
                InstallEvidence.ImmutablePlanVerifiedByHelper, InstallEvidence.TargetIdentityRechecked,
                InstallEvidence.ExclusiveVolumeLock, InstallEvidence.PackageHashRechecked],
            [InstallFlowState.PreparingStorage] = [InstallEvidence.FullImageReadbackHashMatches,
                InstallEvidence.StorageLayoutSupported, InstallEvidence.ChangesLimitedToAuthorizedTarget],
            [InstallFlowState.BootVerifying] = [InstallEvidence.RealPowerCycleRequested,
                InstallEvidence.NewBootIdentityMatchesSession],
            [InstallFlowState.Succeeded] = [InstallEvidence.BootReportMatchesInstallationId,
                InstallEvidence.SystemVersionMatchesPackage, InstallEvidence.RequestedGameDirectoriesConfirmed]
        };

    public InstallFlowState State { get; private set; } = InstallFlowState.Idle;

    public bool IsTerminal => Transitions[State].Length == 0;

    public InstallWorkflowTransition TryTransition(InstallFlowState next,
        IEnumerable<InstallEvidence>? evidence = null)
    {
        var previous = State;
        if (!Transitions.TryGetValue(State, out var allowed) || !allowed.Contains(next))
            return new(false, previous, State, [], $"Transição não permitida: {previous} → {next}.");

        var provided = evidence?.ToHashSet() ?? [];
        var missing = RequiredEvidence.TryGetValue(next, out var required)
            ? required.Where(item => !provided.Contains(item)).ToArray()
            : [];
        if (missing.Length > 0)
            return new(false, previous, State, missing,
                $"A etapa {next} exige evidências ainda não confirmadas: {string.Join(", ", missing)}.");

        State = next;
        return new(true, previous, State, [], $"Etapa alterada para {next}.");
    }
}
