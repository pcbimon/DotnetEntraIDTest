# EntraMvc

.NET 10 MVC app that signs users in with Microsoft Entra ID (Microsoft.Identity.Web, authorization-code flow with a client secret). Locally it runs against the [entra-local](https://github.com/cmaneu/entra-local) emulator, so no Azure tenant is needed.

## Run locally against the emulator

```bash
docker compose up -d                                   # emulator on https://localhost:8443 (localhost only)

# one-time per machine: trust the emulator's self-signed cert for .NET (your user only, no sudo)
docker compose cp entra-local:/app/data/tls/cert.pem .entra-local-cert.pem
dotnet run scripts/trust-emulator-cert.cs -- .entra-local-cert.pem      # undo when done: add --remove

dotnet run --project src/EntraMvc --launch-profile https                # https://localhost:7126
```

Sign in as the emulator's public demo user `alice@entralocal.dev` (password `Password1!` if asked).
Your browser will warn about the emulator's certificate the first time; accept it for `localhost:8443` only.

The app registration in the emulator (`ClientId` in `appsettings.Development.json`) has redirect URI `https://localhost:7126/signin-oidc`. Its client secret is stored only in `dotnet user-secrets`. If you reset the emulator's volume, re-create the app and secret in the portal at https://localhost:8443/ and update both.

## Tests

```bash
dotnet test EntraMvc.slnx     # needs the emulator running and its cert trusted
```

`SignInChallengeTests` is an integration test against the emulator: it pins the emulator config itself, so your user-secrets (e.g. real-tenant values) don't affect it.

## Switch to your real Entra tenant

Your app registration's redirect URI must be `https://localhost:7126` + your `CallbackPath`. Then (values from your credentials, never committed):

```bash
cd src/EntraMvc
dotnet user-secrets set "AzureAd:Instance"     "https://login.microsoftonline.com/"
dotnet user-secrets set "AzureAd:TenantId"     "<tenantid>"
dotnet user-secrets set "AzureAd:ClientId"     "<clientid>"
dotnet user-secrets set "AzureAd:CallbackPath" "<callbackpath>"
# client secret: typed hidden, so it stays out of shell history and process args
read -rsp 'Client secret: ' S && printf '{"AzureAd:ClientSecret":"%s"}' "$S" | dotnet user-secrets set; unset S
```

User-secrets override `appsettings.Development.json`. To return to the emulator, remove those keys with `dotnet user-secrets remove` (and restore the emulator `ClientSecret`). Outside Development, supply the same keys as environment variables (`AzureAd__ClientSecret`, …) or a secret store.
