using Botijas.Domain.Entities;

namespace Botijas.Application.Customers;

internal static class CustomerPhoneTypeParser
{
    public static bool TryParse(string? value, out CustomerPhoneType phoneType)
    {
        if (string.Equals(value, "pt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "PT", StringComparison.OrdinalIgnoreCase))
        {
            phoneType = CustomerPhoneType.PT;
            return true;
        }

        if (string.Equals(value, "international", StringComparison.OrdinalIgnoreCase))
        {
            phoneType = CustomerPhoneType.International;
            return true;
        }

        phoneType = default;
        return false;
    }
}
