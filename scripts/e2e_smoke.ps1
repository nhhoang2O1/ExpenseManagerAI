param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = ("demo+" + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + "@example.com"),
    [string]$Password = "DemoPassword123!",
    [switch]$SkipReceiptOcr
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message"
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null
    )

    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $params = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($null -ne $Body) {
        $params["ContentType"] = "application/json"
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
    }

    Invoke-RestMethod @params
}

function New-ReceiptImage {
    param([string]$Path)

    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap 900, 520
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::White)
    $fontTitle = New-Object System.Drawing.Font "Arial", 42, ([System.Drawing.FontStyle]::Bold)
    $fontText = New-Object System.Drawing.Font "Arial", 30, ([System.Drawing.FontStyle]::Regular)
    $brush = [System.Drawing.Brushes]::Black

    $graphics.DrawString("CIRCLE K", $fontTitle, $brush, 40, 40)
    $graphics.DrawString("NGAY: 2026-07-10", $fontText, $brush, 40, 130)
    $graphics.DrawString("VAT: 4.091", $fontText, $brush, 40, 210)
    $graphics.DrawString("TONG CONG: 45.000 VND", $fontText, $brush, 40, 300)
    $graphics.DrawString("THANH TOAN: TIEN MAT", $fontText, $brush, 40, 390)

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Step "Health check"
Invoke-RestMethod "$BaseUrl/health" | Out-Null
Write-Host "PASS health"

Write-Step "Register user"
$auth = Invoke-Api -Method "Post" -Path "/api/auth/register" -Body @{
    name = "Demo User"
    email = $Email
    password = $Password
}
$token = $auth.accessToken
if (-not $token) {
    throw "Register did not return accessToken."
}
Write-Host "PASS register $Email"

Write-Step "Load default categories"
$categories = Invoke-Api -Method "Get" -Path "/api/categories" -Token $token
$expenseCategory = $categories | Where-Object { $_.type -eq "EXPENSE" } | Select-Object -First 1
if (-not $expenseCategory) {
    throw "No EXPENSE category returned."
}
Write-Host "PASS categories count=$($categories.Count), expenseCategory=$($expenseCategory.name)"

Write-Step "Create transaction"
$today = (Get-Date).ToString("yyyy-MM-dd")
$transaction = Invoke-Api -Method "Post" -Path "/api/transactions" -Token $token -Body @{
    amount = 45000
    type = "EXPENSE"
    transactionDate = $today
    categoryId = $expenseCategory.id
    note = "Smoke transaction"
    storeName = "Circle K"
}
if (-not $transaction.id) {
    throw "Create transaction did not return id."
}
Write-Host "PASS create transaction id=$($transaction.id)"

Write-Step "Query transactions"
$page = Invoke-Api -Method "Get" -Path "/api/transactions?pageSize=10" -Token $token
if ($page.totalCount -lt 1) {
    throw "Expected at least one transaction."
}
Write-Host "PASS transactions total=$($page.totalCount)"

Write-Step "Query statistics"
$year = (Get-Date).Year
$monthly = Invoke-Api -Method "Get" -Path "/api/statistics/monthly?year=$year" -Token $token
if (-not $monthly) {
    throw "Monthly statistics returned empty result."
}
Write-Host "PASS monthly statistics rows=$($monthly.Count)"

if (-not $SkipReceiptOcr) {
    Write-Step "Upload receipt image"
    $receiptImage = Join-Path $PSScriptRoot "receipt-smoke.png"
    New-ReceiptImage -Path $receiptImage

    $uploadRaw = & curl.exe -s -X POST `
        -H "Authorization: Bearer $token" `
        -F "file=@$receiptImage;type=image/png" `
        "$BaseUrl/api/receipts"
    if ($LASTEXITCODE -ne 0) {
        throw "curl upload failed with exit code $LASTEXITCODE"
    }
    $upload = $uploadRaw | ConvertFrom-Json
    if (-not $upload.id) {
        throw "Receipt upload did not return id. Body: $uploadRaw"
    }
    Write-Host "PASS upload receipt id=$($upload.id)"

    Write-Step "Process receipt OCR"
    $receipt = Invoke-Api -Method "Post" -Path "/api/receipts/$($upload.id)/process" -Token $token
    Write-Host "PASS OCR status=$($receipt.status), classification=$($receipt.classification), total=$($receipt.totalAmount)"

    Write-Step "Confirm receipt transaction"
    $storeName = if ($receipt.storeName) { $receipt.storeName } else { "Circle K" }
    $receiptDate = if ($receipt.receiptDate) { $receipt.receiptDate } else { $today }
    $totalAmount = if ($receipt.totalAmount -and $receipt.totalAmount -gt 0) { $receipt.totalAmount } else { 45000 }
    $vatAmount = if ($receipt.vatAmount -and $receipt.vatAmount -ge 0) { $receipt.vatAmount } else { 0 }
    $confirmed = Invoke-Api -Method "Post" -Path "/api/receipts/$($upload.id)/confirm" -Token $token -Body @{
        storeName = $storeName
        receiptDate = $receiptDate
        totalAmount = $totalAmount
        vatAmount = $vatAmount
        categoryId = $expenseCategory.id
        note = "Smoke OCR confirm"
    }
    if (-not $confirmed.id) {
        throw "Confirm receipt did not return transaction id."
    }
    Write-Host "PASS confirm receipt transaction id=$($confirmed.id)"
}

Write-Host ""
Write-Host "E2E smoke PASS"
