# EntraMvc

.NET 10 MVC app that signs users in with Microsoft Entra ID (Microsoft.Identity.Web, authorization-code flow with a client secret). Locally it runs against the [entra-local](https://github.com/cmaneu/entra-local) emulator, so no Azure tenant is needed.

## Prerequisites

- .NET 10 SDK (`dotnet --version` → `10.x`). If installed with `dotnet-install.sh` into `~/.dotnet`, add `export PATH=$HOME/.dotnet:$PATH` to `~/.bashrc`.
- Docker (for the emulator).

## How to use

1. Start the emulator and the app (commands in [Run locally](#run-locally-against-the-emulator) below).
2. Open **https://localhost:7126**. Every page requires sign-in, so you're redirected to the emulator's sign-in page.
3. First time only: the browser warns about the certificate of `localhost:8443` (the emulator's self-signed cert) — choose *Advanced → Proceed*.
4. Pick an account: **alice@entralocal.dev** or **bob@entralocal.dev** (password `Password1!` if asked).
5. You land back on the app; the home page shows `Hello <user>!` and the top-right shows the user with a **Sign out** link.
6. **Sign out** clears the app session and shows the emulator's "Signed out" page; open https://localhost:7126 again to sign in as someone else.

The emulator's portal at **https://localhost:8443/** lets you view and add users, groups and app registrations.

Stop everything: `Ctrl+C` in the `dotnet run` terminal, then `docker compose down` (add `-v` to also wipe the emulator's data — you'd then need to re-create the app, see [Troubleshooting](#troubleshooting)).

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

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| App error `IDX20803 … UntrustedRoot` | .NET doesn't trust the emulator cert yet — run the trust step in [Run locally](#run-locally-against-the-emulator). |
| App error `Unable to retrieve document … 8443` (connection refused) | Emulator not running — `docker compose up -d`, check `curl -k https://localhost:8443/health`. |
| Emulator says redirect URI / client is invalid | The emulator's data was reset (`docker compose down -v`). Re-create the app registration below. |
| `https://localhost:7126` won't open from a Windows browser | WSL localhost forwarding is off — enable `localhostForwarding=true` in `%UserProfile%\.wslconfig` and restart WSL. |
| Real tenant: `AADSTS50011` (redirect URI mismatch) | Add `https://localhost:7126<CallbackPath>` as a **Web** redirect URI on your app registration. |

**Re-create the emulator app registration** (after a data reset):

```bash
APP=$(curl -sk -X POST https://localhost:8443/admin/api/apps -H 'content-type: application/json' \
  -d '{"displayName":"EntraMvc (local)","isConfidential":true,"redirectUris":[{"uri":"https://localhost:7126/signin-oidc","type":"web"}]}' | jq -r .id)
curl -sk -X POST "https://localhost:8443/admin/api/apps/$APP/secrets" -H 'content-type: application/json' -d '{}' \
  | jq '{"AzureAd:ClientSecret": .secretText}' | dotnet user-secrets set --project src/EntraMvc
echo "New ClientId: $APP"   # put it in src/EntraMvc/appsettings.Development.json and ClientId in tests/EntraMvc.Tests/SignInChallengeTests.cs
```
