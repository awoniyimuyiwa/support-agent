using Microsoft.Extensions.Options;
using System.Net;

namespace Web.Options;

public class ConfigureForwardedHeadersOptions(IConfiguration configuration) 
    : IConfigureOptions<ForwardedHeadersOptions>, IValidateOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        var configSection = configuration.GetSection("ForwardedHeaderOptions");
        if (!configSection.Exists()) return;

        configSection.Bind(options);

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var ipStrings = configSection.GetSection("TrustedProxyIPs").Get<string[]>();
        if (ipStrings is not null)
        {
            foreach (var ipString in ipStrings)
            {
                if (IPAddress.TryParse(ipString, out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
            }
        }

        var proxyNetworks = configSection.GetSection("TrustedProxyNetworks").Get<string[]>();
        if (proxyNetworks is not null)
        {
            foreach (var netStr in proxyNetworks)
            {
                if (IPNetwork.TryParse(netStr, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
            }
        }
    }

    // Strict validation check executed on startup
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersOptions options)
    {
        var configSection = configuration.GetSection("ForwardedHeaderOptions");
        if (!configSection.Exists()) return ValidateOptionsResult.Success;

        // Verify individual IPs are valid syntactically
        var ipStrings = configSection.GetSection("TrustedProxyIPs").Get<string[]>();
        if (ipStrings is not null)
        {
            foreach (var ipString in ipStrings)
            {
                if (!IPAddress.TryParse(ipString, out _))
                {
                    return ValidateOptionsResult.Fail($"Invalid IP format found in TrustedProxyIPs: '{ipString}'");
                }
            }
        }

        // Verify CIDR blocks are valid syntactically
        var proxyNetworks = configSection.GetSection("TrustedProxyNetworks").Get<string[]>();
        if (proxyNetworks is not null)
        {
            foreach (var netStr in proxyNetworks)
            {
                if (!IPNetwork.TryParse(netStr, out _))
                {
                    return ValidateOptionsResult.Fail($"Invalid CIDR network format found in TrustedProxyNetworks: '{netStr}'");
                }
            }
        }

        return ValidateOptionsResult.Success;
    }
}

