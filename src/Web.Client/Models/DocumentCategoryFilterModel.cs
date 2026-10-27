using Web.Client.Data;

namespace Web.Client.Models;

class DocumentCategoryFilterModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public SyncStatus? SyncStatusFilter { get; set; }

    public string SortColumn { get; set; } = nameof(DocumentCategory.Name);

    public bool SortDescending { get; set; } = false;

    public int PageNumber { get; set; } = 1;
    
    public int PageSize { get; set; } = 100;
    
    public void Reset()
    {
        PageNumber = 1;
        SearchTerm = string.Empty;
        SortColumn = nameof(DocumentCategory.Name);
        SortDescending = false;
        SyncStatusFilter = null;
    }
}
