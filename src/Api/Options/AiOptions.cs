namespace Api.Options;


using System.ComponentModel.DataAnnotations;

public class AiOptions
{
    public const string SectionName = "AiOptions";

    [Required(AllowEmptyStrings = false, ErrorMessage = "'ChatModelName' must be specified and cannot be empty.")]
    public string ChatModelName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "'EmbeddingModelName' must be specified and cannot be empty.")]
    public string EmbeddingModelName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "'MaxSafeInputTokens' must be a positive integer greater than zero.")]
    public int MaxSafeInputTokens { get; set; }

    [Range(1, 4096, ErrorMessage = "'MaxOutputTokens' must be a positive integer between 1 and 4096.")]
    public int MaxOutputTokens { get; set; }
}

