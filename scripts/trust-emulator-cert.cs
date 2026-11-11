// Dev only: trust (or --remove) the entra-local emulator cert in .NET's CurrentUser\Root store.
// Usage: docker compose cp entra-local:/app/data/tls/cert.pem .entra-local-cert.pem
//        dotnet run scripts/trust-emulator-cert.cs -- .entra-local-cert.pem [--remove]
// While trusted, anyone who can read the emulator's docker volume (its private key) can impersonate
// localhost to .NET apps of this user — run with --remove when you're done testing.
using System.Security.Cryptography.X509Certificates;

var path = args.FirstOrDefault(a => !a.StartsWith("--"));
var remove = args.Contains("--remove");
if (path is null) { Console.Error.WriteLine("usage: <cert.pem> [--remove]"); return 1; }

X509Certificate2 cert;
try { cert = X509Certificate2.CreateFromPem(File.ReadAllText(path)); }
catch (Exception e) { Console.Error.WriteLine($"Cannot read certificate '{path}': {e.Message}"); return 1; }

// Only a self-signed, non-CA, unexpired leaf cert valid for localhost — never something able to sign other certs.
var isCa = cert.Extensions.OfType<X509BasicConstraintsExtension>().Any(b => b.CertificateAuthority);
var coversLocalhost = cert.MatchesHostname("localhost");
if (cert.Subject != cert.Issuer || isCa || !coversLocalhost || cert.NotAfter < DateTime.Now)
{
    Console.Error.WriteLine($"Refusing: not the expected emulator leaf cert ({cert.Subject}, CA={isCa}, localhost={coversLocalhost}, expires {cert.NotAfter:d}).");
    return 1;
}

using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadWrite);
if (remove) store.Remove(cert); else store.Add(cert);
Console.WriteLine($"{(remove ? "Removed" : "Trusted")} {cert.Subject} (SHA-1 {cert.Thumbprint})");
return 0;
