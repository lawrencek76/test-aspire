using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace test_aspire.AppConsole;

public static class CertificateBuilder
{
    public static readonly Oid AnyExtendedKeyUsage = new("2.5.29.37.0");
    public static readonly Oid ServerAuth = new("1.3.6.1.5.5.7.3.1");
    public static readonly Oid ClientAuth = new("1.3.6.1.5.5.7.3.2");
    public static readonly Oid OcspSigning = new("1.3.6.1.5.5.7.3.9");

    public static X509Certificate2 CreateRootCaCertificate(string subjectName, TimeSpan? validTimeSpan = null)
    {
        return CreateRootCaCertificate(new X500DistinguishedName($"CN={subjectName}"), validTimeSpan);
    }

    public static X509Certificate2 CreateRootCaCertificate(X500DistinguishedName distinguishedName, TimeSpan? validTimeSpan = null)
    {
        ArgumentNullException.ThrowIfNull(distinguishedName);
        validTimeSpan ??= TimeSpan.FromDays(365.25 * 20);
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest(
            distinguishedName,
            ecdsa,
            HashAlgorithmName.SHA256);

        // set basic certificate contraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 3, true));
        // key usage: Digital Signature and Key Encipherment
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        // add this subject key identifier
        var key = new X509SubjectKeyIdentifierExtension(request.PublicKey, false);
        request.CertificateExtensions.Add(key);
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromSubjectKeyIdentifier(key));

        var notbefore = DateTime.UtcNow.Date;
        var notafter = DateTime.UtcNow.Date.Add(validTimeSpan.Value);

        return request.CreateSelfSigned(notbefore, notafter);
    }

    public static X509Certificate2 CreateSigningCertificate(string subjectName, X509Certificate2 signingCertificate, TimeSpan? validTimeSpan = null)
    {
        return CreateSigningCertificate(new X500DistinguishedName($"CN={subjectName}"), signingCertificate, validTimeSpan);
    }

    public static X509Certificate2 CreateSigningCertificate(X500DistinguishedName distinguishedName, X509Certificate2 signingCertificate, TimeSpan? validTimeSpan = null)
    {
        ArgumentNullException.ThrowIfNull(distinguishedName);
        ArgumentNullException.ThrowIfNull(signingCertificate);
        validTimeSpan = validTimeSpan ?? TimeSpan.FromDays(365.25 * 20);
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest(
            distinguishedName,
            ecdsa,
            HashAlgorithmName.SHA256);

        // set basic certificate contraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        // key usage: Digital Signature and Key Encipherment
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        // add this subject key identifier
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(signingCertificate, true, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [
                            ClientAuth,
                            ServerAuth,
                            OcspSigning
                ],
                false));

        // Unless the signing cert's validity is less. It's not possible
        // to create a cert with longer validity than the signing cert.
        var notbefore = DateTime.UtcNow.Date;
        var notafter = DateTime.UtcNow.Date.Add(validTimeSpan.Value);

        if (notbefore < signingCertificate.NotBefore)
        {
            notbefore = signingCertificate.NotBefore;
        }

        if (notafter > signingCertificate.NotAfter)
        {
            notafter = signingCertificate.NotAfter;
        }

        // cert serial is the epoch/unix timestamp
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var unixTime = Convert.ToInt64((DateTime.UtcNow - epoch).TotalSeconds);
        var serial = BitConverter.GetBytes(unixTime);

        return request.Create(signingCertificate, notbefore, notafter, serial).CopyWithPrivateKey(ecdsa);
    }

    public static X509Certificate2 CreateSigningRSACertificate(X500DistinguishedName distinguishedName, X509Certificate2 signingCertificate)
    {
        using var rsa = RSA.Create();
        rsa.KeySize = 2048;
        var request = new CertificateRequest(
            distinguishedName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
            );

        // set basic certificate contraints
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        // key usage: Digital Signature and Key Encipherment
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        // add this subject key identifier
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(signingCertificate, true, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [
                            ClientAuth,
                            ServerAuth,
                            OcspSigning
                ],
                false));

        // certificate expiry: Valid from Yesterday to Now+10 years
        // Unless the signing cert's validity is less. It's not possible
        // to create a cert with longer validity than the signing cert.
        var notbefore = DateTime.UtcNow.Date.AddDays(-1);
        var notafter = DateTime.UtcNow.Date.AddYears(10);

        if (notbefore < signingCertificate.NotBefore)
        {
            notbefore = signingCertificate.NotBefore;
        }

        if (notafter > signingCertificate.NotAfter)
        {
            notafter = signingCertificate.NotAfter;
        }

        // cert serial is the epoch/unix timestamp
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var unixTime = Convert.ToInt64((DateTime.UtcNow - epoch).TotalSeconds);
        var serial = BitConverter.GetBytes(unixTime);

        var signature = X509SignatureGenerator.CreateForECDsa(signingCertificate.GetECDsaPrivateKey()!);

        return request.Create(signingCertificate.SubjectName, signature, notbefore, notafter, serial).CopyWithPrivateKey(rsa);
    }


    /// <summary>
    /// Import a PFX file. The first certificate with a private key is 
    /// returned in the <c>certificate</c> value of the return tuple, 
    /// and remaining certificates are returned in the <c>collection</c>
    /// value.
    /// </summary>
    /// <param name="pfxFileName">.pfx file to import/read from</param>
    /// <param name="password">Password to the PFX file.</param>
    /// <returns>a Tuple of <see cref="X509Certificate2"/> and 
    /// <see cref="X509Certificate2Collection"/>
    /// with the contents of the PFX file.</returns>
    public static (X509Certificate2 certificate, X509Certificate2Collection? collection)
        LoadCertificateAndCollectionFromPfx(string pfxFileName, string? password)
    {
        if (string.IsNullOrEmpty(pfxFileName))
        {
            throw new ArgumentException($"{nameof(pfxFileName)} must be a valid filename.", nameof(pfxFileName));
        }
        if (!File.Exists(pfxFileName))
        {
            throw new FileNotFoundException($"{pfxFileName} does not exist. Cannot load certificate from non-existing file.", pfxFileName);
        }

        var certificateCollection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
            pfxFileName,
            password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);

        X509Certificate2? certificate = null;
        var outcollection = new X509Certificate2Collection();
        foreach (X509Certificate2 element in certificateCollection)
        {
            if (certificate == null && element.HasPrivateKey)
            {
                certificate = element;
            }
            else
            {
                outcollection.Add(element);
            }
        }

        if (certificate == null)
        {
            throw new InvalidOperationException($"{pfxFileName} does contain a certificate");
        }
        else
        {
            return (certificate, outcollection);
        }

    }

    /// <summary>
    /// Creates a new certificate with the given subject name and signs it with the 
    /// certificate contained in the <paramref name="signingCertificate"/> parameter.
    /// </summary>
    /// <remarks>The generated certificate has the same attributes as the ones created
    /// by the IoT Hub Device Provisioning samples</remarks>
    /// <param name="subjectName">Subject name to give the new certificate</param>
    /// <param name="signingCertificate">Certificate to sign the new certificate with</param>
    /// <returns>A signed <see cref="X509Certificate2"/>.</returns>
    public static X509Certificate2 CreateServerCertificate(string subjectName, X509Certificate2 signingCertificate, int validDays = 365, params string[] alternateNames)
    {
        if (signingCertificate is not null && !signingCertificate.HasPrivateKey)
        {
            throw new Exception("Signing cert must have private key");
        }
        if (string.IsNullOrEmpty(subjectName))
        {
            throw new ArgumentException($"{nameof(subjectName)} must be a valid DNS name", nameof(subjectName));
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.KeySize = 256;
        var request = new CertificateRequest(
            $"CN={subjectName}",
            ecdsa,
            HashAlgorithmName.SHA256);

        // set basic certificate contraints
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        // key usage: Digital Signature and Key Encipherment
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));

        // DPS samples create certs with the device name as a SAN name 
        // in addition to the subject name
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(subjectName);
        foreach (var name in alternateNames)
        {
            if (System.Net.IPAddress.TryParse(name, out var ip))
            {
                sanBuilder.AddIpAddress(ip);
            }
            else
            {
                sanBuilder.AddDnsName(name);
            }
        }
        var sanExtension = sanBuilder.Build();
        request.CertificateExtensions.Add(sanExtension);

        // Enhanced key usages
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [
                            ClientAuth,
                            ServerAuth,
                            OcspSigning
                ],
                false));

        // add this subject key identifier
        var key = new X509SubjectKeyIdentifierExtension(request.PublicKey, false);
        request.CertificateExtensions.Add(key);

        // certificate expiry: Valid from Yesterday to Now+365 days
        // Unless the signing cert's validity is less. It's not possible
        // to create a cert with longer validity than the signing cert.
        var notbefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notafter = DateTimeOffset.UtcNow.AddDays(validDays);

        if (signingCertificate is not null && signingCertificate.HasPrivateKey)
        {
            request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(signingCertificate, true, true));
            if (notbefore < signingCertificate.NotBefore)
            {
                notbefore = new DateTimeOffset(signingCertificate.NotBefore);
            }

            if (notafter > signingCertificate.NotAfter)
            {
                notafter = new DateTimeOffset(signingCertificate.NotAfter);
            }

            // cert serial is the epoch/unix timestamp
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var unixTime = Convert.ToInt64((DateTime.UtcNow - epoch).TotalSeconds);
            var serial = BitConverter.GetBytes(unixTime);

            return request.Create(
                signingCertificate,
                notbefore,
                notafter,
                serial).CopyWithPrivateKey(ecdsa);
        }
        else
        {
            request.CertificateExtensions.Add(new X509AuthorityKeyIdentifierExtension(key.RawData, false));
            return request.CreateSelfSigned(
                notbefore,
                notafter).CopyWithPrivateKey(ecdsa);
        }
    }
    public static void InstallCert(X509Certificate2 cert, StoreName storeName, StoreLocation storeLocation = StoreLocation.CurrentUser)
    {
        var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.MaxAllowed);
        var existing = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, cert.SubjectName.Name, false);
        foreach (var ex in existing)
        {
            store.Remove(ex);
        }
        store.Add(cert);
        store.Close();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                string allusers = Environment.GetEnvironmentVariable("ALLUSERSPROFILE")!;
                var keyPath = new DirectoryInfo(@$"{allusers}\Microsoft\Crypto\Keys").GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (keyPath == null) return;
                var fileSecurity = new FileSecurity(keyPath.FullName, AccessControlSections.Access);
                fileSecurity.AddAccessRule(new FileSystemAccessRule(@".\Users", FileSystemRights.Read, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
                fileSecurity.SetAccessRuleProtection(true, false);
            }
            catch { }
        }
    }
}