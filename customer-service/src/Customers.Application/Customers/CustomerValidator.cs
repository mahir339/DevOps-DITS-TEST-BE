using DevOpsStore.BuildingBlocks.Results;

namespace Customers.Application.Customers;

public static class CustomerValidator
{
    public static Error? Validate(CustomerRequest request) => new ValidationBuilder()
        .AddIf(string.IsNullOrWhiteSpace(request.FirstName), "firstName", "First name is required.")
        .AddIf(string.IsNullOrWhiteSpace(request.LastName), "lastName", "Last name is required.")
        .AddIf(!IsValidEmail(request.Email), "email", "A valid email is required.")
        .ToError();

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim();
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 1 && !email.Contains(' ');
    }
}
