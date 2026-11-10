using System.ComponentModel.DataAnnotations;

namespace Web.Client.Data;

public enum SyncStatus : byte
{
    Synced = 0,

    [Display(Name = "Pending Insert")]
    PendingInsert = 1,

    [Display(Name = "Pending Update")]
    PendingUpdate = 2,

    [Display(Name = "Pending Delete")]
    PendingDelete = 3
}