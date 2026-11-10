using Domain.Shared;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Web.Client.Extensions;

public static class StringFormatting
{
    extension(Enum enumValue)
    {
        public string DisplayName
        {
            get
            {
                // Intercept UI overrides for Domain Enums first
                if (enumValue is TicketStatus status)
                {
                    return status switch
                    {
                        TicketStatus.InProgress => "In Progress",
                        _ => status.ToString()
                    };
                }

                // Fallback to [Display(Name = "...")] via reflection
                var displayAttribute = enumValue.GetType()
                    .GetField(enumValue.ToString())?
                    .GetCustomAttribute<DisplayAttribute>();

                return displayAttribute?.Name ?? enumValue.ToString();
            }
        }
    }
}
