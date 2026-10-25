using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Attributes;

public class AllowedFileTypesAttribute : AllowedValuesAttribute
{
    public AllowedFileTypesAttribute() : base(Constants.AllowedFileTypes) 
    {
    }
}
