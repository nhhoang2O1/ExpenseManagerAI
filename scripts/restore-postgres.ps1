[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)] [string]$DumpFile,
    [string]$ComposeService = "postgres",
    [string]$Database = "expense_manager_restore_test",
    [string]$User = "expense_manager",
    [switch]$DropAndCreate
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dump = [IO.Path]::GetFullPath((Join-Path $root $DumpFile))
if (-not (Test-Path -LiteralPath $dump)) { throw "Backup file not found: $dump" }
$checksum = "$dump.sha256"
if (Test-Path -LiteralPath $checksum) {
    $expected = (Get-Content -Raw -LiteralPath $checksum).Trim().ToLowerInvariant()
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $dump).Hash.ToLowerInvariant()
    if ($expected -ne $actual) { throw "Backup checksum mismatch." }
}

if ($DropAndCreate) {
    Write-Host "Recreating test database $Database"
    docker compose --project-directory $root exec -T $ComposeService `
        psql -U $User -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$Database\";"
    docker compose --project-directory $root exec -T $ComposeService `
        psql -U $User -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"$Database\";"
}

Write-Host "Restoring into $Database (test database only)."
$containerDump = "/tmp/expense-manager-restore-$([Guid]::NewGuid().ToString('N')).dump"
try {
    docker compose --project-directory $root cp `
        $dump "${ComposeService}:$containerDump"
    if ($LASTEXITCODE -ne 0) { throw "Could not copy backup into PostgreSQL container." }
    docker compose --project-directory $root exec -T $ComposeService `
        pg_restore --clean --if-exists --no-owner --exit-on-error `
            -U $User -d $Database $containerDump
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed." }
}
finally {
    docker compose --project-directory $root exec -T $ComposeService `
        rm -f $containerDump 2>$null
}

Write-Host "Restore completed. Verify receipt_images byte hashes before promoting."
