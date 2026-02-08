namespace Botijas.Domain.ValueObjects;

public record PhoneNumber
{
    public string Value { get; private init; }

    private PhoneNumber(string value)
    {
        Value = Normalize(value);
    }

    public static PhoneNumber Create(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("Phone number cannot be empty", nameof(phone));
        }

        var normalized = Normalize(phone);
        
        if (normalized.Length < 9)
        {
            throw new ArgumentException("Phone number is too short", nameof(phone));
        }

        return new PhoneNumber(normalized);
    }

    private static string Normalize(string phone)
    {
        // Remove espaços, hífens, parênteses e outros caracteres
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    public override string ToString() => Value;
}
