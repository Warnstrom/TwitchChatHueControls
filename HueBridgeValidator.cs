using System.Security.Cryptography.X509Certificates;

public class BridgeValidator
{
    private readonly HttpClient _httpClient;

    public BridgeValidator()
    {
        // Set up the HttpClient with custom DNS resolution and certificate handling
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (errors == System.Net.Security.SslPolicyErrors.None)
                return true;

            // Load the custom CA certificate
            var customCaCert = new X509Certificate2("huebridge_cacert.pem");

            // Check if the certificate chain is signed by the custom CA certificate
            return chain.ChainElements.Cast<X509ChainElement>()
                .Any(x => x.Certificate.Equals(customCaCert));
        };

        // Use a custom DNS resolver
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
        {
            return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None || certificate.Issuer == "CN=root-bridge";
        };

        _httpClient = new HttpClient(handler);

        // Add the custom DNS resolver
        _httpClient.DefaultRequestHeaders.Host = "bridgeId";
    }

    public async Task<bool> ValidateBridgeIpAsync(string bridgeId, string bridgeIp, string appKey)
    {
        string url = $"https://{bridgeId}/api/{appKey}/config";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var validatedUri) || (validatedUri.Scheme != Uri.UriSchemeHttps))
        {
            Console.WriteLine("Invalid URI: The hostname could not be parsed.");
            return false;
        }

        // Add the Host header to resolve bridgeId to bridgeIp
        _httpClient.DefaultRequestHeaders.Host = bridgeId;

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine(content);
            return true;
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"Request error: {httpEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
        return false;
    }
}
