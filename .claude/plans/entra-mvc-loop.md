# Loop runbook: .NET 10 MVC + Entra ID sign-in

Pattern: sequential · Mode: safe

## Steps (each must pass before the next)
1. Scaffold `dotnet new mvc --auth SingleOrg` (Microsoft.Identity.Web, `AzureAd` config section).
2. Secrets: ClientSecret only via `dotnet user-secrets` / env vars; never in appsettings or git.
3. Integration test: anonymous GET `/` challenges to `login.microsoftonline.com/{TenantId}` with the configured `client_id` and `redirect_uri` ending in `CallbackPath`.
4. Gate: `dotnet build` clean, `dotnet test` green.
5. Review: ecc:csharp-reviewer + security pass on the code.

## Stop condition
All gates green and review has no CRITICAL/HIGH findings, or 3 failed iterations on the same gate → stop and report.

## Monitor
`dotnet test` output; `dotnet run --project src/EntraMvc` then sign in manually.
