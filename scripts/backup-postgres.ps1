[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\backups\postgres"),
    [string]$ComposeService = "postgres",
    [string]$Database = "",
    [string]$User = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dump = Join-Path $output "expense-manager-$stamp.dump"
$checksum = "$dump.sha256"
$dbArg = if ($Database) { $Database } else { "expense_manager" }
$userArg = if ($User) { $User } else { "expense_manager" }

Write-Host "Creating PostgreSQL custom-format backup: $dump"
$containerDump = "/tmp/expense-manager-$stamp.dump"
try {
    docker compose --project-directory $root exec -T $ComposeService `
        pg_dump -Fc -U $userArg -d $dbArg -f $containerDump
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }
    docker compose --project-directory $root cp `
        "${ComposeService}:$containerDump" $dump
    if ($LASTEXITCODE -ne 0) { throw "Could not copy backup from PostgreSQL container." }
}
finally {
    docker compose --project-directory $root exec -T $ComposeService `
        rm -f $containerDump 2>$null
}

if (-not (Test-Path -LiteralPath $dump) -or (Get-Item -LiteralPath $dump).Length -eq 0) {
    throw "pg_dump returned an empty backup."
}
(Get-FileHash -Algorithm SHA256 -LiteralPath $dump).Hash | Set-Content -Encoding ascii -Path $checksum

# Keep the latest fourteen daily artifacts; never delete the live database or
# receipt data. Operators can override retention by moving older dumps first.
$cutoff = (Get-Date).AddDays(-14)
Get-ChildItem -LiteralPath $output -Filter "*.dump" -File |
    Where-Object LastWriteTime -lt $cutoff |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
Get-ChildItem -LiteralPath $output -Filter "*.dump.sha256" -File |
    Where-Object LastWriteTime -lt $cutoff |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

Write-Host "SHA-256: $((Get-Content -Raw -LiteralPath $checksum).Trim())"
