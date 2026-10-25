namespace Api.Filters;

/// <summary>
/// Marker attribute used to flag endpoints that generate file upload URLs.
/// The <see cref="IdempotencyFilter"/> detects this attribute via endpoint metadata 
/// to dynamically override the standard idempotency record TTL with the configured 
/// upload URL lifetime from app settings.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CreatesUploadUrlAttribute : Attribute { }
