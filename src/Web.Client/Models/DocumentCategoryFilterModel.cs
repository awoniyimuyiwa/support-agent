using Web.Client.Data;

namespace Web.Client.Models;

class DocumentCategoryFilterModel : FilterModel
{
    public string SortColumn { get; set; } = nameof(DocumentCategory.Name);
    
    public override void Reset()
    {
        base.Reset();
        SortColumn = nameof(DocumentCategory.Name);
    }
}
