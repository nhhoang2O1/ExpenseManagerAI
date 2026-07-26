param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = ("demo+" + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + "@example.com"),
    [string]$Password = "DemoPassword123!",
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
        [string]$IdempotencyKey = $null
    )

    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }
    $params = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $headers }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
    }
    Invoke-RestMethod @params
}

Write-Step "Health check"
Invoke-RestMethod "$BaseUrl/health" | Out-Null
Write-Host "PASS health"

Write-Step "Register user"
$auth = Invoke-Api -Method Post -Path "/api/auth/register" -Body @{
    name = "Demo User"; email = $Email; password = $Password
}
$token = $auth.accessToken
if (-not $token) { throw "Register did not return accessToken." }
Write-Host "PASS register $Email"

Write-Step "Load default categories"
$categories = Invoke-Api -Method Get -Path "/api/categories" -Token $token
$expenseCategory = $categories | Where-Object type -eq "EXPENSE" | Select-Object -First 1
if (-not $expenseCategory) { throw "No EXPENSE category returned." }
Write-Host "PASS categories count=$($categories.Count)"

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

Write-Step "Query statistics"
$monthly = Invoke-Api -Method Get -Path "/api/statistics/monthly?year=$((Get-Date).Year)" -Token $token
if ($null -eq $monthly) { throw "Monthly statistics returned null." }
Write-Host "PASS monthly statistics"

if (-not $ReceiptImagePath) {
    Write-Host "SKIP receipt OCR: pass -ReceiptImagePath with a real fixture to test OCR."
    Write-Host "E2E smoke PASS (API core)"
    exit 0
}

$image = (Resolve-Path -LiteralPath $ReceiptImagePath).Path
Write-Step "Upload real receipt fixture"
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
    throw "OCR failed for the supplied real fixture: $($receipt.lastError)"
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
Write-Host "PASS confirm receipt transaction id=$($confirmed.id)"
Write-Host "E2E smoke PASS"
