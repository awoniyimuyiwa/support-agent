namespace Web.Client.Models;

public class DocumentFilterModel : FilterModel
{
    public Guid? CategoryId { get; set; }

    public override void Reset()
    {
        base.Reset();
        CategoryId = null;
    }
}

