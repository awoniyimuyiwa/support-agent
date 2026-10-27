using Web.Client.Data;

namespace Web.Client.Models;

public class DocumentFilterModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public Guid? CategoryIdFilter { get; set; }

    public SyncStatus? SyncStatusFilter { get; set; }

    public int PageNumber { get; set; } = 1;


    public int PageSize { get; set; } = 10;

    public bool SortDescending { get; set; } = true;

    public void Reset()
    {
        CategoryIdFilter = null;
        PageNumber = 1;
        SearchTerm = string.Empty;
        SyncStatusFilter = null;
    }
}

