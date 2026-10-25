using System.Net.Mime;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Attributes;

public class AllowedFileTypesAttribute : AllowedValuesAttribute
{
    public AllowedFileTypesAttribute() : base(
        MediaTypeNames.Application.Pdf,
        MediaTypeNames.Text.Html,
        MediaTypeNames.Text.Plain,
        MediaTypeNames.Text.Markdown,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document") // Custom literal
    {
    }
}
