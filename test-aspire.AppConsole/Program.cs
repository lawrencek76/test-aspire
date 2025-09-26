using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;

using test_aspire.AppConsole;

var certsDir = Path.Combine(Path.GetTempPath(), "development", "certificates");
if (!Directory.Exists(certsDir)) Directory.CreateDirectory(certsDir);

var rootPath = Path.Combine(certsDir, "test-root.pfx");
if (!File.Exists(rootPath))
{
    VerifyAdmin();
    Console.WriteLine($"Creating root certificate {rootPath}");
    var rootCert = CertificateBuilder.CreateRootCaCertificate("test-development-root");
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        rootCert.FriendlyName = "Local Development Root Certificate";
    }
    File.WriteAllBytes(rootPath, rootCert.Export(X509ContentType.Pkcs12));
    File.WriteAllText(Path.ChangeExtension(rootPath, "crt"), rootCert.ExportCertificatePem());
    CertificateBuilder.InstallCert(rootCert, StoreName.Root);
}

var intermediatePath = Path.Combine(certsDir, "test-intermediate.pfx");
if (!File.Exists(intermediatePath))
{
    VerifyAdmin();
    Console.WriteLine($"Creating intermediate certificate {intermediatePath}");
    var (certificate, _) = CertificateBuilder.LoadCertificateAndCollectionFromPfx(rootPath, null);
    var intermediateCert = CertificateBuilder.CreateSigningCertificate("test-intermediate-signing", certificate);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        intermediateCert.FriendlyName = "Local Development Intermediate Certificate";
    }
    File.WriteAllBytes(intermediatePath, intermediateCert.Export(X509ContentType.Pkcs12));
    File.WriteAllText(Path.ChangeExtension(intermediatePath, "crt"), intermediateCert.ExportCertificatePem());
    CertificateBuilder.InstallCert(intermediateCert, StoreName.CertificateAuthority);
}

var testPath = Path.Combine(certsDir, "test-wildcard.pfx");
if (!File.Exists(testPath))
{
    VerifyAdmin();
    Console.WriteLine($"Creating test wildcard certificate {testPath}");
    var (certificate, _) = CertificateBuilder.LoadCertificateAndCollectionFromPfx(intermediatePath, null);
    var testCert = CertificateBuilder.CreateServerCertificate("*.site.test", certificate);
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        testCert.FriendlyName = "Local Development Wildcard Certificate";
    }
    var cert12 = testCert.Export(X509ContentType.Pkcs12);
    File.WriteAllBytes(testPath, cert12);
    File.WriteAllText(Path.ChangeExtension(testPath, "crt"), testCert.ExportCertificatePem());
    File.WriteAllText(Path.ChangeExtension(testPath, "key"), testCert.GetECDsaPrivateKey()?.ExportPkcs8PrivateKeyPem());
    var eportableKey = X509CertificateLoader.LoadPkcs12(cert12, null, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    CertificateBuilder.InstallCert(eportableKey, StoreName.My);
}

var hostFilePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts")
    : "/etc/hosts";

var hosts = File.ReadAllText(hostFilePath).Trim();

var developmentHosts = """
    127.0.0.2 site.test
    127.0.0.2 www.site.test
    127.0.0.3 api.site.test
    127.0.0.2 sub.site.test
    127.0.0.2 sub2.site.test
    """;
const string markerStart = "# BEGIN site.test";
const string markerEnd = "# END site.test";
string replacement = $@"{markerStart}{Environment.NewLine}{developmentHosts}{Environment.NewLine}{markerEnd}";

var regex = new Regex($@"({markerStart})(.*)({markerEnd})", RegexOptions.Singleline);
if (regex.IsMatch(hosts))
{
    hosts = regex.Replace(hosts, replacement);
}
else
{
    hosts += $"{Environment.NewLine}{replacement}{Environment.NewLine}";
}
Console.WriteLine($"Updating hosts file at {hostFilePath}");
Console.Write(replacement);
File.WriteAllText(hostFilePath, hosts);

static bool VerifyAdmin()
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;

    try
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            var current = Process.GetCurrentProcess();
            Process.Start(new ProcessStartInfo
            {
                FileName = current.MainModule!.FileName,
                Arguments = string.Join(" ", Environment.GetCommandLineArgs()[1..]),
                UseShellExecute = true,
                Verb = "runas"
            });
            Environment.Exit(0);
        }
    }
    catch
    {
        Console.WriteLine("Must be and admin to install cert");
        Environment.Exit(1);
    }
    return true;
}