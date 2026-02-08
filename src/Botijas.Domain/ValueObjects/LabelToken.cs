namespace Botijas.Domain.ValueObjects;

public record LabelToken
{
    public string Value { get; private init; }

    private LabelToken(string value)
    {
        Value = value;
    }

    public static LabelToken Create(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Label token cannot be empty", nameof(token));
        }

        return new LabelToken(token.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;
}
