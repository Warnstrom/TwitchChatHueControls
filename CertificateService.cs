using System.Diagnostics;

class CertificateService
{
    public static async Task ConfigureCertificate(string[] args)
    {
        string serverAddress = args[0];
        string port = args[1];
        string outputFilePath = args[2];

        try
        {
            string certificate = await FetchCertificateAsync(serverAddress, port);

            if (!string.IsNullOrEmpty(certificate))
            {
                await SaveCertificateToFileAsync(outputFilePath, certificate);
                Console.WriteLine($"Certificate saved to {outputFilePath}");
            }
            else
            {
                Console.WriteLine("No certificate found in the output.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static async Task<string?> FetchCertificateAsync(string serverAddress, string port)
    {
        Process process = new Process();
        process.StartInfo.FileName = "openssl";
        process.StartInfo.Arguments = $"s_client -showcerts -connect {serverAddress}:{port}";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return ExtractCertificate(output);
    }

    private static string? ExtractCertificate(string output)
    {
        const string beginCert = "-----BEGIN CERTIFICATE-----";
        const string endCert = "-----END CERTIFICATE-----";

        int startIndex = output.IndexOf(beginCert);
        int endIndex = output.IndexOf(endCert);

        if (startIndex != -1 && endIndex != -1)
        {
            endIndex += endCert.Length;
            return output[startIndex..endIndex];
        }

        return null;
    }

    private static async Task SaveCertificateToFileAsync(string outputFilePath, string certificate)
    {
        await File.WriteAllTextAsync(outputFilePath, certificate);
    }
}
