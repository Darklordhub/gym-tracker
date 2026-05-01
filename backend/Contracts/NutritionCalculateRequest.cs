using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public class NutritionCalculateRequest : IValidatableObject
{
    public List<NutritionCalculateIngredientRequest> Ingredients { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ingredients.Count == 0)
        {
            yield return new ValidationResult(
                "At least one ingredient is required.",
                [nameof(Ingredients)]);
        }
    }
}
