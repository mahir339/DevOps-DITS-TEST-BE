namespace DevOpsStore.BuildingBlocks.Results;

/// <summary>Collects field validation messages and turns them into a validation <see cref="Error"/>.</summary>
public sealed class ValidationBuilder
{
    private readonly Dictionary<string, string[]> _errors = new();

    public ValidationBuilder AddIf(bool condition, string field, string message)
    {
        if (condition)
        {
            _errors[field] = _errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];
        }
        return this;
    }

    public Error? ToError() => _errors.Count == 0 ? null : Error.Validation(_errors);
}
