namespace Api.Contracts.Models.V1;

using System;
using System.Collections.Generic;

public record UserInfoModel(
    string? Email,
    string FirstName,
    Guid Id,
    bool IsEmailConfirmed,
    string LastName,
    DateTimeOffset? LockoutEnd,
    string? UserName,
    List<string>? Roles = null);
