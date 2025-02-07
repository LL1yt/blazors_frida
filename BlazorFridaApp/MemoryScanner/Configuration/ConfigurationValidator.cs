using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace BlazorFridaApp.MemoryScanner.Configuration;

public class ConfigurationValidator : IValidateOptions<MemoryScannerSettings>
{
    public ValidateOptionsResult Validate(string? name, MemoryScannerSettings options)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);

        if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
        {
            var errors = validationResults
                .Select(r => r.ErrorMessage ?? "Validation failed")
                .ToArray();
            return ValidateOptionsResult.Fail(errors);
        }

        if (options.DefaultReadSize <= 0 || options.DefaultReadSize > options.MaxReadSize)
        {
            return ValidateOptionsResult.Fail($"DefaultReadSize must be between 1 and {options.MaxReadSize}");
        }

        if (options.DefaultMemoryRanges.DefaultEnd <= options.DefaultMemoryRanges.DefaultStart)
        {
            return ValidateOptionsResult.Fail("DefaultEnd must be greater than DefaultStart in memory ranges");
        }

        return ValidateOptionsResult.Success;
    }
} 