using System.ComponentModel;

namespace CeoAgent.Shared.Enums;

public enum ProviderSendLedgerStatus
{
    [Description("The provider send attempt has started but has not completed yet.")]
    SendAttemptStarted = 1,

    [Description("The provider accepted the send request and returned a provider message id or success response.")]
    ProviderAccepted = 2,

    [Description("The provider rejected the send request with a non-retryable error.")]
    ProviderRejected = 3,

    [Description("The provider did not respond before the configured timeout.")]
    ProviderTimeout = 4,

    [Description("The provider or network was temporarily unavailable.")]
    ProviderUnavailable = 5,

    [Description("The send attempt failed before the external provider call was made.")]
    FailedBeforeProviderCall = 6,
}
