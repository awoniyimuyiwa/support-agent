namespace Api;

public class Constants
{
    public const string AntiforgeryCookieName = "__Host-Antiforgery";

    public const string AntiforgeryValidationErrorMessage = "Antiforgery token validation failed";

    public const string ConcurrentLoginErrorMessage = "Concurrent logins are prohibited. Please log out from other devices before logging in again.";

    public const string DistributedLockResiliencePolicyName = "distributed_lock_resilience_policy";

    public const string GlobalRateLimitPolicyName = "global_rate_limit_policy";

    public const string LlmHttpClientName = "LlmHttpClient";

    public const string SecretProtectorPurpose = "VaultedSecrets";

    public const string TraceIdHeaderName = "Trace-ID";

    public const string UserRateLimitPolicyName = "user_rate_limit_policy";
}