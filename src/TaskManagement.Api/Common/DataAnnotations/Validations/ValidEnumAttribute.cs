using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Common.DataAnnotations.Validations
{
    public class ValidEnumAttribute : ValidationAttribute
    {
        private readonly Type _enumType;

        public ValidEnumAttribute(Type enumType)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException($"{enumType.ToString()} is not an an enum.");
            }
            _enumType = enumType;
        }

        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || !Enum.IsDefined(_enumType, value))
            {
                var validValues = string.Join(", ", Enum.GetNames(_enumType));
                return new ValidationResult($"Please provide one from the list: [{validValues}]");
            }

            return ValidationResult.Success!;
        }
    }
}
