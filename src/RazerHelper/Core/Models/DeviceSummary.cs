namespace RazerHelper.Core.Models;

public sealed record DeviceSummary(
    string ModelName,
    bool IsConnected,
    string StatusMessage
);