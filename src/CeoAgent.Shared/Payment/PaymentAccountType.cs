using System.Text.Json.Serialization;

namespace CeoAgent.Shared.Payment;

[JsonConverter(typeof(JsonStringEnumConverter<PaymentAccountType>))]
public enum PaymentAccountType
{
    Ahorros = 1,
    Corriente = 2,
}
