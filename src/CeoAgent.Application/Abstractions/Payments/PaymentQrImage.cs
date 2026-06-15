namespace CeoAgent.Application.Abstractions.Payments;

public sealed record PaymentQrImage(
    byte[] Content,
    string ContentType,
    string FileName);
