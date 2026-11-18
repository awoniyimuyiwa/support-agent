using Api.Contracts.Attributes;
using Domain.Shared;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Contracts.Models.V1;

public record DocumentVersionSyncRequest(
    [Required] 
    [UniqueMax(maxLength: Constants.MaxLengthOfList, UniquePropertyName = nameof(DocumentVersionSyncRequestItem.Id))] List<DocumentVersionSyncRequestItem> Items,
    [EnumDataType(typeof(SyncBehavior))][property: JsonConverter(typeof(JsonStringEnumConverter))] SyncBehavior Behavior = SyncBehavior.Default)
{
    public List<DocumentVersionSyncRequestItem> Items { get; init; } = Items ?? [];
}

public record DocumentVersionSyncRequestItem(
     [RequiredNotDefault] Guid Id,
     [RequiredNotDefault] DateTimeOffset UpdatedAt,
     [MaxLength(Domain.Shared.Constants.StringMaxLength)] string? ConcurrencyStamp,
     bool IsDeleted) : ISyncableModel;

