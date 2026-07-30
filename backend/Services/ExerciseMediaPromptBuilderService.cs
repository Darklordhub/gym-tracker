using System.Text;
using backend.Models;

namespace backend.Services;

public class ExerciseMediaPromptBuilderService
{
    public string BuildPrompt(ExerciseCatalogItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Create a short instructional fitness demonstration video for the exercise below.");
        builder.AppendLine("The result should be suitable for later video generation and easy for a gym user to follow.");
        builder.AppendLine();
        builder.AppendLine("Exercise data:");
        AppendField(builder, "Name", GetEffectiveName(item));
        AppendField(builder, "Provider source", item.Source);
        AppendField(builder, "Equipment", item.Equipment);
        AppendField(builder, "Body part/category", GetBodyPartOrCategory(item));
        AppendField(builder, "Target muscle", item.PrimaryMuscle);
        AppendField(builder, "Secondary muscles", item.SecondaryMuscles);
        AppendField(builder, "Instructions", GetEffectiveInstructions(item));
        AppendField(builder, "Existing video state", DescribeMediaState(item.VideoUrl, item.LocalVideoUrlOverride));
        AppendField(builder, "Existing thumbnail state", DescribeMediaState(item.ThumbnailUrl, item.LocalThumbnailUrlOverride));
        AppendField(builder, "Local override state", DescribeOverrideState(item));
        builder.AppendLine();
        builder.AppendLine("Direction:");
        builder.AppendLine("- Show one person performing the exercise in a short instructional fitness demo style.");
        builder.AppendLine("- Demonstrate correct posture, stable alignment, and a controlled tempo from setup through return.");
        builder.AppendLine("- Use realistic human movement and natural range of motion in a neutral, uncluttered gym background.");
        builder.AppendLine("- Make the exercise and equipment clearly visible, with camera framing that supports safe technique review.");
        builder.AppendLine("- Avoid common mistakes for this exercise when the supplied instructions identify them.");
        builder.AppendLine("- Do not show brand logos, unsafe loading, reckless speed, or exaggerated body proportions.");
        builder.AppendLine("- Do not add text overlays, fictional equipment, or movements that are not part of the exercise.");
        builder.AppendLine("- Keep the final result concise, realistic, and appropriate for a general fitness audience.");

        return builder.ToString().Trim();
    }

    private static void AppendField(StringBuilder builder, string label, string? value)
    {
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(string.IsNullOrWhiteSpace(value) ? "Not available" : value.Trim());
    }

    private static string GetBodyPartOrCategory(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.PrimaryMuscle)
            ? item.SecondaryMuscles
            : item.PrimaryMuscle;
    }

    private static string DescribeMediaState(string? providerUrl, string? localOverride)
    {
        var providerState = string.IsNullOrWhiteSpace(providerUrl) ? "missing" : "available";
        var localState = string.IsNullOrWhiteSpace(localOverride) ? "none" : "present";
        return $"provider={providerState}; local override={localState}";
    }

    private static string DescribeOverrideState(ExerciseCatalogItem item)
    {
        var overrides = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.LocalNameOverride))
        {
            overrides.Add("name");
        }

        if (!string.IsNullOrWhiteSpace(item.LocalInstructionsOverride))
        {
            overrides.Add("instructions");
        }

        if (!string.IsNullOrWhiteSpace(item.LocalThumbnailUrlOverride))
        {
            overrides.Add("thumbnail");
        }

        if (!string.IsNullOrWhiteSpace(item.LocalVideoUrlOverride))
        {
            overrides.Add("video");
        }

        return overrides.Count == 0 ? "none" : string.Join(", ", overrides);
    }

    private static string GetEffectiveName(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalNameOverride) ? item.Name : item.LocalNameOverride.Trim();
    }

    private static string? GetEffectiveInstructions(ExerciseCatalogItem item)
    {
        return string.IsNullOrWhiteSpace(item.LocalInstructionsOverride)
            ? item.Instructions
            : item.LocalInstructionsOverride.Trim();
    }
}
