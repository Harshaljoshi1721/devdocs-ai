using FluentValidation;
using AppValidationException = DevDocsAI.Application.Common.Exceptions.ValidationException;

namespace DevDocsAI.Application.Common.Validation;

internal static class ValidationExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> and throws the application-level
    /// <see cref="AppValidationException"/> (grouped by property) when invalid.
    /// </summary>
    public static async Task ValidateAndThrowAppAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        throw new AppValidationException(errors);
    }
}
