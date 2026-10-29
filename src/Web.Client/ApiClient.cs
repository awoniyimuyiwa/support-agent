using Api.Contracts;
using Api.Contracts.Models.V1;
using System.Net.Http.Json;
using Web.Client.Models;

namespace Web.Client;

public class ApiClient(HttpClient httpClient)
{
    public HttpClient HttpClient => httpClient;

    /// <summary>
    /// Creates docuemnt versions
    /// </summary>
    public Task<HttpResponseMessage> CreateDocumentVersions(
        CreateDocumentVersionRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/document-versionss",
            batch,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Requests pre-signed cloud storage upload URLs for a batch of document versions.
    /// </summary>
    public Task<HttpResponseMessage> CreateDocumentVersionUploadUrls(
        CreateUploadUrlRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/document-versions/upload-urls",
            batch,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Calls server to set the anti-forgery token cookie
    /// </summary>
    /// <param name="cancellationToken"></param>
    public Task<HttpResponseMessage> GetAntiforgeryToken(
        CancellationToken cancellationToken = default) => httpClient.GetAsync(
            "v1/auth/antiforgery-token",
            cancellationToken);
   
    /// <summary>
    /// Fetches the current user authentication context and permission maps
    /// </summary>
    public Task<UserInfoRespone?> GetUserInfo(
        CancellationToken cancellationToken = default) => httpClient.GetFromJsonAsync<UserInfoRespone>(
            "v1/account/info",
            cancellationToken);

    public Task<HttpResponseMessage> GetUserInfoResponse(
        CancellationToken cancellationToken = default) => httpClient.GetAsync( 
            "v1/account/info", 
            cancellationToken);

    public Task<HttpResponseMessage> Login(
        LoginModel loginModel,
        CancellationToken cancellationToken = default) => httpClient.PostAsJsonAsync(
            "v1/auth/login?useCookies=true",
            loginModel,
            cancellationToken);

    public Task<HttpResponseMessage> Logout(
        CancellationToken cancellationToken = default) =>  httpClient.PostAsync(
            "v1/auth/logout",
            null,
            cancellationToken);
    
    /// <summary>
    /// Fetches document updates
    /// </summary>
    public Task<HttpResponseMessage> StreamDocumentDeltas(
        string? next, 
        CancellationToken token = default) => StreamDeltas(
            "v1/admin/documents", 
            next,
            token);

    /// <summary>
    /// Fetches document category updates
    /// </summary>
    public Task<HttpResponseMessage> StreamDocumentCategoryDeltas(
        string? next,
        CancellationToken token = default) => StreamDeltas(
            "v1/admin/document-categories",
            next,
            token);

    /// <summary>
    /// Fetches document version updates
    /// </summary>
    public Task<HttpResponseMessage> StreamDocumentVersionDeltas(
        string? next,
        CancellationToken token = default) => StreamDeltas(
            "v1/admin/document-versions", 
            next, 
            token);

    /// <summary>
    /// Fetches updates of tickets that belong to the user
    /// </summary>
    public Task<HttpResponseMessage> StreamTicketDeltas(
        string? next,
        CancellationToken token = default) => StreamDeltas(
            "v1/tickets",
            next,
            token);

    /// <summary>
    /// Fetches all ticket updates
    /// </summary>
    public Task<HttpResponseMessage> StreamAllTicketDeltas(
        string? next,
        CancellationToken token = default) => StreamDeltas(
            "v1/admin/tickets",
            next,
            token);

    /// <summary>
    /// Syncs documents
    /// </summary>
    public Task<HttpResponseMessage> SyncDocuments(
        DocumentSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/documents",
            batch,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Syncs document categories
    /// </summary>
    public Task<HttpResponseMessage> SyncDocumentCategories(
        DocumentCategorySyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/document-categories",
            batch,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Syncs document versions
    /// </summary>
    public Task<HttpResponseMessage> SyncDocumentVersions(
        DocumentVersionSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/document-versions",
            batch,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Syncs tickets that belong to only the user
    /// </summary>
    public Task<HttpResponseMessage> SyncTickets(
        TicketSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/tickets",
            batch, 
            idempotencyKey, 
            cancellationToken);

    /// <summary>
    /// Syncs all tickets
    /// </summary>
    public Task<HttpResponseMessage> SyncAllTickets(
        FullTicketSyncRequest batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) => PostBatch(
            "v1/admin/tickets",
            batch,
            idempotencyKey,
            cancellationToken);

    private Task<HttpResponseMessage> PostBatch<TBody>(
        string endpoint,
        TBody batch,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(batch)
        };

        request.Headers.Add(
            Constants.IdempotencyKeyHeaderName,
            idempotencyKey.ToString());

        return httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> StreamDeltas(
        string path,
        string? next,
        CancellationToken token)
    {
        var url = $"{path}/stream-deltas?pageSize={Domain.Shared.Constants.MaxPageSize}";

        if (!string.IsNullOrWhiteSpace(next))
        {
            // Escape the encrypted string safely to prevent special base64 characters (+, /, =) from breaking URL parsing
            url += $"&next={Uri.EscapeDataString(next)}";
        }

        return await httpClient.GetAsync(url, token);
    }
}
