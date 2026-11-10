using Domain.Shared;
using Web.Client.Data;

namespace Web.Client.Models;

public class FilterModel
{
    public static int PageSize => Domain.Shared.Constants.MaxPageSize;

    public string SearchTerm { get; set; } = string.Empty;

    public SyncStatus? SyncStatus { get; set; }

    public int PageNumber { get; set; } = 1;

    public bool SortDescending { get; set; } = true;

    public virtual void Reset()
    {
        PageNumber = 1;
        SearchTerm = string.Empty;
        SortDescending = true;
        SyncStatus = null;
    }
}

