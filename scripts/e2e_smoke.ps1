param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = ("demo+" + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + "@example.com"),
    [string]$Password = "DemoPassword123!",
    [string]$VerificationCode = "",
    [switch]$ReadVerificationCodeFromDockerLogs,
    [string]$DockerBackendContainer = "",
    [string]$ReceiptImagePath = "",
    [int]$ReceiptTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message"
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null,
        [string]$IdempotencyKey = $null,
        [string]$IfMatch = $null
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }
    if ($IfMatch) { $headers["If-Match"] = $IfMatch }
    $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json; charset=utf-8"
        $json = $Body | ConvertTo-Json -Depth 10
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes($json)
    }
    Invoke-RestMethod @params
}

function Assert-LogoutRevokesRefreshToken([string]$RefreshToken) {
    Write-Step "Logout and reject the revoked refresh token"
    Invoke-Api -Method Post -Path "/api/auth/logout" -Body @{
        refreshToken = $RefreshToken
    } | Out-Null

    try {
        Invoke-Api -Method Post -Path "/api/auth/refresh" -Body @{
            refreshToken = $RefreshToken
        } | Out-Null
        throw "The revoked refresh token was accepted."
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response -or [int]$response.StatusCode -ne 401) {
            throw
        }
    }
    Write-Host "PASS logout revoked the refresh token"
}

Write-Step "Health check"
Invoke-RestMethod "$BaseUrl/health" | Out-Null
Write-Host "PASS health"

Write-Step "Register user"
Invoke-Api -Method Post -Path "/api/auth/register" -Body @{
    name = "Demo User"; email = $Email; password = $Password
} | Out-Null
Write-Host "PASS registration request $Email"

if (-not $VerificationCode) {
    if ($ReadVerificationCodeFromDockerLogs) {
        if (-not $DockerBackendContainer) {
            $composeFile = Join-Path $PSScriptRoot "..\docker-compose.yml"
            $DockerBackendContainer = (& docker compose -f $composeFile ps -q backend | Select-Object -First 1).Trim()
        }
        if (-not $DockerBackendContainer) {
            throw "Cannot find the Docker backend container."
        }

        $containerLogs = (& docker logs --since 2m $DockerBackendContainer 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0) {
            throw "Cannot read backend Docker logs."
        }
        $pattern = [regex]::Escape($Email) + '\s*:\s*(?<code>[0-9]{6})'
        $matches = [regex]::Matches($containerLogs, $pattern)
        if ($matches.Count -eq 0) {
            throw "No development verification code was found in backend logs for $Email."
        }
        $VerificationCode = $matches[$matches.Count - 1].Groups['code'].Value
        Write-Host "PASS development verification code read from Docker logs"
    } else {
        Write-Host "Enter the six-digit registration code sent to $Email."
        $VerificationCode = Read-Host "Verification code"
    }
}
if ($VerificationCode -notmatch '^[0-9]{6}$') {
    throw "VerificationCode must contain exactly six digits."
}

Write-Step "Confirm registration"
$auth = Invoke-Api -Method Post -Path "/api/auth/confirm-registration" -Body @{
    email = $Email; code = $VerificationCode
}
$token = $auth.accessToken
if (-not $token -or -not $auth.refreshToken) {
    throw "Registration confirmation did not return the access/refresh token pair."
}
Write-Host "PASS registration confirmation"

Write-Step "Login with the confirmed account"
$login = Invoke-Api -Method Post -Path "/api/auth/login" -Body @{
    email = $Email; password = $Password
}
if (-not $login.accessToken -or -not $login.refreshToken) {
    throw "Login did not return the access/refresh token pair."
}
$token = $login.accessToken
$refreshToken = $login.refreshToken
Write-Host "PASS login"

Write-Step "Load default categories"
$categories = Invoke-Api -Method Get -Path "/api/categories" -Token $token
$expenseCategory = $categories | Where-Object type -eq "EXPENSE" | Select-Object -First 1
if (-not $expenseCategory) { throw "No EXPENSE category returned." }
Write-Host "PASS categories count=$(@($categories).Count)"

Write-Step "Create and query transaction"
$today = (Get-Date).ToString("yyyy-MM-dd")
$transaction = Invoke-Api -Method Post -Path "/api/transactions" -Token $token `
    -IdempotencyKey ([Guid]::NewGuid().ToString()) -Body @{
        amount = 45000; type = "EXPENSE"; transactionDate = $today
        categoryId = $expenseCategory.id; note = "Smoke transaction"; storeName = "Smoke store"
    }
if (-not $transaction.id) { throw "Create transaction did not return id." }
$page = Invoke-Api -Method Get -Path "/api/transactions?page=1&pageSize=10" -Token $token
if ($page.totalCount -lt 1) { throw "Expected at least one transaction." }
Write-Host "PASS transaction id=$($transaction.id), total=$($page.totalCount)"

Write-Step "Create monthly budget"
$monthYear = (Get-Date).ToString("yyyy-MM")
$budget = Invoke-Api -Method Post -Path "/api/budgets" -Token $token -Body @{
    categoryId = $expenseCategory.id; amount = 2000000; monthYear = $monthYear
}
if (-not $budget.id -or $budget.amount -ne 2000000) {
    throw "Budget use case returned an unexpected response."
}
Write-Host "PASS budget id=$($budget.id)"

Write-Step "Create goal and add funds idempotently"
$goal = Invoke-Api -Method Post -Path "/api/goals" -Token $token -Body @{
    name = "Emergency fund"; targetAmount = 5000000; currentAmount = 0
}
if (-not $goal.id) { throw "Goal creation did not return id." }
$fundsKey = [Guid]::NewGuid().ToString()
$fundedGoal = Invoke-Api -Method Post -Path "/api/goals/$($goal.id)/funds" `
    -Token $token -IdempotencyKey $fundsKey -IfMatch ('"' + $goal.version + '"') -Body @{
        amount = 500000
    }
if ($fundedGoal.currentAmount -ne 500000) {
    throw "Goal funds were not applied exactly once."
}
$replayedGoal = Invoke-Api -Method Post -Path "/api/goals/$($goal.id)/funds" `
    -Token $token -IdempotencyKey $fundsKey -IfMatch ('"' + $goal.version + '"') -Body @{
        amount = 500000
    }
if ($replayedGoal.currentAmount -ne 500000) {
    throw "Replaying goal funds applied the amount more than once."
}
Write-Host "PASS goal id=$($goal.id), currentAmount=$($fundedGoal.currentAmount)"

Write-Step "Create and query reminder"
$reminder = Invoke-Api -Method Post -Path "/api/reminders" -Token $token `
    -IdempotencyKey ([Guid]::NewGuid().ToString()) -Body @{
        content = "Pay electricity bill"; dayOfMonth = 20
        hour = 8; minute = 15; isActive = $true
    }
$reminders = Invoke-Api -Method Get -Path "/api/reminders" -Token $token
$storedReminder = $reminders | Where-Object id -eq $reminder.id | Select-Object -First 1
if (-not $reminder.id -or -not $storedReminder) {
    throw "Reminder was not returned by the authenticated user's list."
}
Write-Host "PASS reminder id=$($reminder.id)"

Write-Step "Query statistics"
$monthly = Invoke-Api -Method Get -Path "/api/statistics/monthly?year=$((Get-Date).Year)" -Token $token
if ($null -eq $monthly) { throw "Monthly statistics returned null." }
Write-Host "PASS monthly statistics"

if (-not $ReceiptImagePath) {
    Write-Host "SKIP receipt OCR: pass -ReceiptImagePath with a receipt fixture to test the pipeline."
    Assert-LogoutRevokesRefreshToken -RefreshToken $refreshToken
    Write-Host "E2E use-case smoke PASS (auth, transaction, budget, goal, reminder, statistics)"
    exit 0
}

$image = (Resolve-Path -LiteralPath $ReceiptImagePath).Path
Write-Step "Upload receipt fixture"
$receiptKey = [Guid]::NewGuid().ToString()
$uploadRaw = & curl.exe -sS -X POST `
    -H "Authorization: Bearer $token" `
    -H "Idempotency-Key: $receiptKey" `
    -F "file=@$image" `
    "$BaseUrl/api/receipts"
if ($LASTEXITCODE -ne 0) { throw "curl upload failed with exit code $LASTEXITCODE" }
$upload = $uploadRaw | ConvertFrom-Json
if (-not $upload.id) { throw "Receipt upload did not return id. Body: $uploadRaw" }
Write-Host "PASS upload receipt id=$($upload.id)"

Write-Step "Queue and poll OCR"
Invoke-Api -Method Post -Path "/api/receipts/$($upload.id)/process" -Token $token | Out-Null
$deadline = (Get-Date).AddSeconds($ReceiptTimeoutSeconds)
do {
    Start-Sleep -Seconds 2
    $receipt = Invoke-Api -Method Get -Path "/api/receipts/$($upload.id)" -Token $token
    Write-Host "status=$($receipt.status), attempts=$($receipt.processingAttempts)"
} while ($receipt.status -in @("UPLOADED", "QUEUED", "PROCESSING") -and (Get-Date) -lt $deadline)

if ($receipt.status -in @("UPLOADED", "QUEUED", "PROCESSING")) {
    throw "Receipt did not reach a terminal review state within $ReceiptTimeoutSeconds seconds."
}
if ($receipt.status -eq "OCR_FAILED") {
    throw "OCR failed for the supplied fixture: $($receipt.lastError)"
}
if ($receipt.status -ne "REVIEW_REQUIRED") {
    throw "Unexpected receipt status: $($receipt.status)"
}

# Never invent OCR fallback values. The supplied fixture must produce a complete
# review payload before smoke confirmation is allowed.
if (-not $receipt.storeName -or -not $receipt.receiptDate -or
    -not $receipt.totalAmount -or $receipt.totalAmount -le 0) {
    throw "OCR fixture did not produce storeName, receiptDate and positive totalAmount."
}

Write-Step "Confirm OCR result without fallback data"
$confirmed = Invoke-Api -Method Post -Path "/api/receipts/$($upload.id)/confirm" `
    -Token $token -Body @{
        storeName = $receipt.storeName
        receiptDate = $receipt.receiptDate
        totalAmount = $receipt.totalAmount
        vatAmount = $receipt.vatAmount
        categoryId = $expenseCategory.id
        note = "Smoke OCR confirm"
    }
if (-not $confirmed.id) { throw "Confirm receipt did not return transaction id." }
$transactionsAfterOcr = Invoke-Api -Method Get -Path "/api/transactions?page=1&pageSize=100" -Token $token
$storedOcrTransaction = $transactionsAfterOcr.items |
    Where-Object id -eq $confirmed.id |
    Select-Object -First 1
if (-not $storedOcrTransaction) {
    throw "The confirmed OCR transaction was not returned by the transaction list."
}
Write-Host "PASS confirm receipt transaction id=$($confirmed.id)"
Assert-LogoutRevokesRefreshToken -RefreshToken $refreshToken
Write-Host "E2E smoke PASS"
