using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Common.DataAnnotations.Validations
{
    public class FutureDateTime : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not DateTimeOffset dateTimeOffset)
            {
                return new ValidationResult($"Invalid DateTime format.");
            }

            if (dateTimeOffset.UtcDateTime < DateTime.UtcNow)
            {
                return new ValidationResult("DateTime must not be in the past.");
            }

            return ValidationResult.Success;
        }
    }
}
