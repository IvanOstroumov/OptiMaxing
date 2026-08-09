using OptiMaxing.Core.Safety;

namespace OptiMaxing.Core.Testing;

public sealed class FakeRestorePointService : IRestorePointService
{
    public RestorePointStatus Status { get; set; } =
        new(ProtectionEnabled: false, BlockedByPolicy: false, LastRestorePointUtc: null);

    public bool CreateResult { get; set; } = true;
    public bool EnableProtectionResult { get; set; } = true;

    public RestorePointStatus GetStatus() => Status;
    public Task<bool> CreateAsync(string description, CancellationToken ct) => Task.FromResult(CreateResult);
    public Task<bool> EnableProtectionAsync(string driveLetter, CancellationToken ct) => Task.FromResult(EnableProtectionResult);
}
