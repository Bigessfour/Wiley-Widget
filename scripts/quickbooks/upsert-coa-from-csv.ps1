#!/usr/bin/env pwsh
<#
.SYNOPSIS
Upserts Chart of Accounts rows in QuickBooks Online using AccountNumber (AcctNum).

.DESCRIPTION
Reads a CSV with columns AccountNumber, Name, QboAccountType and upserts each row:
- If AcctNum exists in QBO, updates name/type using sparse update.
- If AcctNum does not exist, creates a new account.

.PARAMETER CsvPath
Path to extracted COA CSV.

.PARAMETER AccessToken
#!/usr/bin/env pwsh
<#
.SYNOPSIS
Upserts chart-of-accounts rows into QuickBooks Online by account number.

.DESCRIPTION
Reads a CSV with columns AccountNumber, Name, and QboAccountType.

Behavior:
- If an account already exists by AcctNum, it performs a sparse update.
- If no account exists by AcctNum, it optionally checks by Name.
- If no matching account exists, it creates a new one.
- If QuickBooks rejects a create because the name already exists, it retries with
  "Name (AccountNumber)".
- If QuickBooks reports that the sandbox account limit has been reached, the script
  stops attempting new creates but still continues to update accounts that already exist.

The script is written to be safe under Set-StrictMode and compatible with PowerShell 7.5.x.

.PARAMETER CsvPath
Path to the chart-of-accounts CSV file.

.PARAMETER AccessToken
QuickBooks OAuth access token. Defaults to QBO_ACCESS_TOKEN.

.PARAMETER RealmId
QuickBooks company realm ID. Defaults to QBO_REALM_ID.

.PARAMETER Environment
QuickBooks environment: sandbox or production.

.PARAMETER RequestDelayMilliseconds
Delay between network operations. Defaults to 250ms.

.PARAMETER SkipNameMatch
Do not query QuickBooks by Name when AcctNum is not found.

.PARAMETER ValidateOnly
Validate the CSV rows and QuickBooks type/subtype mapping without making API calls.

.PARAMETER FailOnRowError
Exit with code 1 if any row fails.

.EXAMPLE
./upsert-coa-from-csv.ps1 -CsvPath .\sql\coa.csv -Environment sandbox

.EXAMPLE
./upsert-coa-from-csv.ps1 -CsvPath .\sql\coa.csv -ValidateOnly
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CsvPath,

    [Parameter(Mandatory = $false)]
    [string]$AccessToken = $env:QBO_ACCESS_TOKEN,

    [Parameter(Mandatory = $false)]
    [string]$RealmId = $env:QBO_REALM_ID,

    [Parameter(Mandatory = $false)]
    [ValidateSet('sandbox', 'production')]
    [string]$Environment = 'sandbox',

    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 5000)]
    [int]$RequestDelayMilliseconds = 250,

    [Parameter(Mandatory = $false)]
    [switch]$SkipNameMatch,

    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly,

    [Parameter(Mandatory = $false)]
    [switch]$FailOnRowError
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-QboBaseUrl {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('sandbox', 'production')]
        [string]$TargetEnvironment
    )

    if ($TargetEnvironment -eq 'sandbox') {
        return 'https://sandbox-quickbooks.api.intuit.com'
    }

    return 'https://quickbooks.api.intuit.com'
}

function Get-ResolvedFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if (-not (Test-Path -LiteralPath $PathValue)) {
        throw "File not found: $PathValue"
    }

    $resolvedItem = Resolve-Path -LiteralPath $PathValue
    return $resolvedItem.Path
}

function Assert-RequiredColumns {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rows,

        [Parameter(Mandatory = $true)]
        [string[]]$RequiredColumns
    )

    if ($Rows.Count -eq 0) {
        throw 'CSV contains no rows.'
    }

    $propertyNames = @($Rows[0].PSObject.Properties.Name)
    foreach ($column in $RequiredColumns) {
        if ($propertyNames -notcontains $column) {
            throw "CSV is missing required column '$column'."
        }
    }
}

function Get-StringOrDefault {
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object]$Value,

        [Parameter(Mandatory = $false)]
        [string]$Default = ''
    )

    if ($null -eq $Value) {
        return $Default
    }

    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $Default
    }

    return $text.Trim()
}

function Normalize-AccountType {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RawAccountType,

        [Parameter(Mandatory = $false)]
        [string]$RawSourceType
    )

    $candidate = Get-StringOrDefault -Value $RawAccountType
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = Get-StringOrDefault -Value $RawSourceType
    }

    $normalized = $candidate.Replace(' ', '').Replace('/', '').Replace('-', '')

    switch -Regex ($normalized) {
        '^Bank$' { return 'Bank' }
        '^Expense$' { return 'Expense' }
        '^Income$' { return 'Income' }
        '^Equity$' { return 'Equity' }
        '^AccountsReceivable$' { return 'AccountsReceivable' }
        '^AccountsPayable$' { return 'AccountsPayable' }
        '^OtherCurrentAsset$' { return 'OtherCurrentAsset' }
        '^OtherCurrentLiability$' { return 'OtherCurrentLiability' }
        '^LongTermLiability$' { return 'LongTermLiability' }
        '^FixedAsset$' { return 'FixedAsset' }
        '^Asset$' { return 'OtherCurrentAsset' }
        default { return $null }
    }
}

function Get-AccountSubType {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AccountType,

        [Parameter(Mandatory = $true)]
        [string]$AccountName
    )

    switch ($AccountType) {
        'AccountsPayable' {
            return 'AccountsPayable'
        }
        'AccountsReceivable' {
            return 'AccountsReceivable'
        }
        'OtherCurrentLiability' {
            if ($AccountName -match 'PAYROLL|W/H|WITHHOLD|UNEMP|FICA|IRA|DIRECT DEPOSIT|GARNISHMENT') {
                return 'PayrollTaxPayable'
            }

            return 'OtherCurrentLiabilities'
        }
        'LongTermLiability' {
            return 'NotesPayable'
        }
        'OtherCurrentAsset' {
            if ($AccountName -match 'PREPAID') {
                return 'PrepaidExpenses'
            }

            if ($AccountName -match 'INVENTORY') {
                return 'Inventory'
            }

            if ($AccountName -match 'ALLOWANCE') {
                return 'AllowanceForBadDebts'
            }

            return 'OtherCurrentAssets'
        }
        'Equity' {
            if ($AccountName -match 'OPENING BAL') {
                return 'OpeningBalanceEquity'
            }

            if ($AccountName -match 'RETAINED|FUND BALANCE|NET ASSETS|UNRESTRICTED NET ASSETS') {
                return 'RetainedEarnings'
            }

            return 'OwnersEquity'
        }
        default {
            return $null
        }
    }
}

function New-AccountSpecification {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Row,

        [Parameter(Mandatory = $true)]
        [int]$RowNumber
    )

    $accountNumber = Get-StringOrDefault -Value $Row.AccountNumber
    $name = Get-StringOrDefault -Value $Row.Name
    $rawAccountType = Get-StringOrDefault -Value $Row.QboAccountType
    $rawSourceType = Get-StringOrDefault -Value $Row.RawType

    $normalizedAccountType = Normalize-AccountType -RawAccountType $rawAccountType -RawSourceType $rawSourceType
    $accountSubType = $null
    if (-not [string]::IsNullOrWhiteSpace($normalizedAccountType) -and -not [string]::IsNullOrWhiteSpace($name)) {
        $accountSubType = Get-AccountSubType -AccountType $normalizedAccountType -AccountName $name
    }

    $validationErrors = [System.Collections.Generic.List[string]]::new()

    if ([string]::IsNullOrWhiteSpace($accountNumber)) {
        $validationErrors.Add('AccountNumber is missing.') | Out-Null
    }

    if ([string]::IsNullOrWhiteSpace($name)) {
        $validationErrors.Add('Name is missing.') | Out-Null
    }

    if ([string]::IsNullOrWhiteSpace($normalizedAccountType)) {
        $validationErrors.Add("QboAccountType '$rawAccountType' could not be normalized to a supported QuickBooks account type.") | Out-Null
    }

    return [pscustomobject]@{
        RowNumber      = $RowNumber
        AccountNumber  = $accountNumber
        Name           = $name
        AccountType    = $normalizedAccountType
        AccountSubType = $accountSubType
        RawAccountType = $rawAccountType
        RawSourceType  = $rawSourceType
        IsValid        = ($validationErrors.Count -eq 0)
        Validation     = @($validationErrors)
    }
}

function New-ResultRecord {
    param(
        [Parameter(Mandatory = $true)]
        [int]$RowNumber,

        [Parameter(Mandatory = $true)]
        [string]$AccountNumber,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Action,

        [Parameter(Mandatory = $true)]
        [string]$Status,

        [Parameter(Mandatory = $true)]
        [string]$Detail
    )

    return [pscustomobject]@{
        RowNumber     = $RowNumber
        AccountNumber = $AccountNumber
        Name          = $Name
        Action        = $Action
        Status        = $Status
        Detail        = $Detail
    }
}

function ConvertTo-JsonBody {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$BodyMap
    )

    return ($BodyMap | ConvertTo-Json -Depth 12 -Compress)
}

function ConvertFrom-JsonSafely {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$JsonText
    )

    if ([string]::IsNullOrWhiteSpace($JsonText)) {
        return $null
    }

    try {
        return $JsonText | ConvertFrom-Json -ErrorAction Stop
    } catch {
        return $null
    }
}

function Get-HttpErrorInfo {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $rawBody = $null
    $statusCode = $null

    if ($null -ne $ErrorRecord.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        $rawBody = $ErrorRecord.ErrorDetails.Message.Trim()
    }

    $responseProperty = $ErrorRecord.Exception.PSObject.Properties['Response']
    if (($null -eq $rawBody) -and $null -ne $responseProperty -and $null -ne $responseProperty.Value) {
        $response = $responseProperty.Value

        $statusCodeProperty = $response.PSObject.Properties['StatusCode']
        if ($null -ne $statusCodeProperty -and $null -ne $statusCodeProperty.Value) {
            $statusCode = [int]$statusCodeProperty.Value
        }

        try {
            $contentProperty = $response.PSObject.Properties['Content']
            if ($null -ne $contentProperty -and $null -ne $contentProperty.Value) {
                $rawBody = $contentProperty.Value.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        } catch {
        }
    }

    $parsedBody = ConvertFrom-JsonSafely -JsonText (Get-StringOrDefault -Value $rawBody)
    $errorCode = $null
    $message = $ErrorRecord.Exception.Message
    $detail = $null

    if ($null -ne $parsedBody) {
        $faultProperty = $parsedBody.PSObject.Properties['Fault']
        if ($null -ne $faultProperty -and $null -ne $faultProperty.Value) {
            $errorsProperty = $faultProperty.Value.PSObject.Properties['Error']
            if ($null -ne $errorsProperty -and $null -ne $errorsProperty.Value) {
                $firstError = @($errorsProperty.Value)[0]
                if ($null -ne $firstError) {
                    $messageProperty = $firstError.PSObject.Properties['Message']
                    if ($null -ne $messageProperty -and -not [string]::IsNullOrWhiteSpace([string]$messageProperty.Value)) {
                        $message = [string]$messageProperty.Value
                    }

                    $detailProperty = $firstError.PSObject.Properties['Detail']
                    if ($null -ne $detailProperty -and -not [string]::IsNullOrWhiteSpace([string]$detailProperty.Value)) {
                        $detail = [string]$detailProperty.Value
                    }

                    $codeProperty = $firstError.PSObject.Properties['code']
                    if ($null -ne $codeProperty -and -not [string]::IsNullOrWhiteSpace([string]$codeProperty.Value)) {
                        $errorCode = [string]$codeProperty.Value
                    }
                }
            }
        }
    }

    return [pscustomobject]@{
        StatusCode = $statusCode
        ErrorCode  = $errorCode
        Message    = $message
        Detail     = $detail
        RawBody    = $rawBody
    }
}

function Get-FriendlyErrorMessage {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ErrorInfo
    )

    if (-not [string]::IsNullOrWhiteSpace($ErrorInfo.Detail)) {
        return "$($ErrorInfo.Message) $($ErrorInfo.Detail)"
    }

    if (-not [string]::IsNullOrWhiteSpace($ErrorInfo.Message)) {
        return $ErrorInfo.Message
    }

    return 'Unknown QuickBooks error.'
}

function Invoke-QboRequest {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('GET', 'POST')]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [Parameter(Mandatory = $true)]
        [hashtable]$Headers,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [string]$Body
    )

    try {
        if ([string]::IsNullOrWhiteSpace($Body)) {
            $response = Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Method $Method -Uri $Uri -Headers $Headers -Body $Body -ErrorAction Stop
        }

        return [pscustomobject]@{
            Success   = $true
            Data      = $response
            ErrorInfo = $null
        }
    } catch {
        return [pscustomobject]@{
            Success   = $false
            Data      = $null
            ErrorInfo = (Get-HttpErrorInfo -ErrorRecord $_)
        }
    }
}

function Get-FirstExistingAccount {
    param(
        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object]$Response
    )

    if ($null -eq $Response) {
        return $null
    }

    $queryResponseProperty = $Response.PSObject.Properties['QueryResponse']
    if ($null -eq $queryResponseProperty -or $null -eq $queryResponseProperty.Value) {
        return $null
    }

    $accountProperty = $queryResponseProperty.Value.PSObject.Properties['Account']
    if ($null -eq $accountProperty -or $null -eq $accountProperty.Value) {
        return $null
    }

    return @($accountProperty.Value)[0]
}

function Find-ExistingAccount {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('AcctNum', 'Name')]
        [string]$Field,

        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$CurrentRealmId,

        [Parameter(Mandatory = $true)]
        [hashtable]$Headers
    )

    $escapedValue = $Value.Replace("'", "\'")
    $query = "select * from Account where $Field = '$escapedValue'"
    $encodedQuery = [uri]::EscapeDataString($query)
    $uri = "$BaseUrl/v3/company/$CurrentRealmId/query?query=$encodedQuery"

    $result = Invoke-QboRequest -Method 'GET' -Uri $uri -Headers $Headers
    if (-not $result.Success) {
        return [pscustomobject]@{
            Success   = $false
            Account   = $null
            ErrorInfo = $result.ErrorInfo
        }
    }

    return [pscustomobject]@{
        Success   = $true
        Account   = (Get-FirstExistingAccount -Response $result.Data)
        ErrorInfo = $null
    }
}

function Test-IsDuplicateNameError {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ErrorInfo
    )

    if ($ErrorInfo.ErrorCode -eq '6240') {
        return $true
    }

    $raw = Get-StringOrDefault -Value $ErrorInfo.RawBody
    return $raw -match 'Duplicate Name Exists Error|Duplicate Name|name already exists'
}

function Test-IsAccountLimitError {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ErrorInfo
    )

    if ($ErrorInfo.ErrorCode -eq '-11283') {
        return $true
    }

    $raw = Get-StringOrDefault -Value $ErrorInfo.RawBody
    return $raw -match 'usage limits|manage your usage limits'
}

function Get-AlternateAccountName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$AccountNumber
    )

    return "$Name ($AccountNumber)"
}

function New-CreateBodyMap {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Specification,

        [Parameter(Mandatory = $false)]
        [string]$OverrideName
    )

    $bodyMap = @{
        Name        = $(if ([string]::IsNullOrWhiteSpace($OverrideName)) { $Specification.Name } else { $OverrideName })
        AcctNum     = $Specification.AccountNumber
        AccountType = $Specification.AccountType
        Active      = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($Specification.AccountSubType)) {
        $bodyMap['AccountSubType'] = $Specification.AccountSubType
    }

    return $bodyMap
}

function New-UpdateBodyMap {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Specification,

        [Parameter(Mandatory = $true)]
        [object]$ExistingAccount
    )

    return @{
        Id        = $ExistingAccount.Id
        SyncToken = $ExistingAccount.SyncToken
        sparse    = $true
        Name      = $Specification.Name
        AcctNum   = $Specification.AccountNumber
        Active    = $true
    }
}

$resolvedCsvPath = Get-ResolvedFilePath -PathValue $CsvPath
$rows = @(Import-Csv -LiteralPath $resolvedCsvPath)
Assert-RequiredColumns -Rows $rows -RequiredColumns @('AccountNumber', 'Name', 'QboAccountType')

if (-not $ValidateOnly) {
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw 'AccessToken is required (parameter or QBO_ACCESS_TOKEN env var).'
    }

    if ([string]::IsNullOrWhiteSpace($RealmId)) {
        throw 'RealmId is required (parameter or QBO_REALM_ID env var).'
    }
}

$baseUrl = Get-QboBaseUrl -TargetEnvironment $Environment
$accountUrl = if ($ValidateOnly) { '' } else { "$baseUrl/v3/company/$RealmId/account" }

$headers = @{}
if (-not $ValidateOnly) {
    $headers = @{
        'Authorization' = "Bearer $AccessToken"
        'Accept'        = 'application/json'
        'Content-Type'  = 'application/json'
    }
}

$results = [System.Collections.Generic.List[object]]::new()
$creationLimitReached = $false
$accountLimitWarningWritten = $false

for ($index = 0; $index -lt $rows.Count; $index++) {
    $rowNumber = $index + 2
    $spec = New-AccountSpecification -Row $rows[$index] -RowNumber $rowNumber

    if (-not $spec.IsValid) {
        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Validate' -Status 'Failed' -Detail ($spec.Validation -join ' '))) | Out-Null
        continue
    }

    if ($ValidateOnly) {
        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Validate' -Status 'Validated' -Detail "AccountType=$($spec.AccountType); AccountSubType=$($spec.AccountSubType)")) | Out-Null
        continue
    }

    $lookupByNumber = Find-ExistingAccount -Field 'AcctNum' -Value $spec.AccountNumber -BaseUrl $baseUrl -CurrentRealmId $RealmId -Headers $headers
    if (-not $lookupByNumber.Success) {
        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Lookup' -Status 'Failed' -Detail (Get-FriendlyErrorMessage -ErrorInfo $lookupByNumber.ErrorInfo))) | Out-Null
        continue
    }

    $existingAccount = $lookupByNumber.Account
    if (($null -eq $existingAccount) -and (-not $SkipNameMatch)) {
        $lookupByName = Find-ExistingAccount -Field 'Name' -Value $spec.Name -BaseUrl $baseUrl -CurrentRealmId $RealmId -Headers $headers
        if (-not $lookupByName.Success) {
            $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Lookup' -Status 'Failed' -Detail (Get-FriendlyErrorMessage -ErrorInfo $lookupByName.ErrorInfo))) | Out-Null
            continue
        }

        $existingAccount = $lookupByName.Account
    }

    if ($null -ne $existingAccount) {
        $targetDescription = "$($spec.AccountNumber) - $($spec.Name)"
        if (-not $PSCmdlet.ShouldProcess($targetDescription, 'Update QuickBooks account')) {
            $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Update' -Status 'WhatIf' -Detail 'Update skipped due to ShouldProcess.')) | Out-Null
            continue
        }

        $updateBody = ConvertTo-JsonBody -BodyMap (New-UpdateBodyMap -Specification $spec -ExistingAccount $existingAccount)
        $updateResult = Invoke-QboRequest -Method 'POST' -Uri ("{0}?operation=update" -f $accountUrl) -Headers $headers -Body $updateBody
        if ($updateResult.Success) {
            $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Update' -Status 'Updated' -Detail 'Updated existing account.')) | Out-Null
        } else {
            $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Update' -Status 'Failed' -Detail (Get-FriendlyErrorMessage -ErrorInfo $updateResult.ErrorInfo))) | Out-Null
        }

        if ($RequestDelayMilliseconds -gt 0) {
            Start-Sleep -Milliseconds $RequestDelayMilliseconds
        }

        continue
    }

    if ($creationLimitReached) {
        if (-not $accountLimitWarningWritten) {
            Write-Warning 'QuickBooks sandbox account limit has been reached; remaining unmatched rows will be skipped.'
            $accountLimitWarningWritten = $true
        }

        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'Skipped' -Detail 'Skipped because QuickBooks sandbox account limit has been reached.')) | Out-Null
        continue
    }

    $createTarget = "$($spec.AccountNumber) - $($spec.Name)"
    if (-not $PSCmdlet.ShouldProcess($createTarget, 'Create QuickBooks account')) {
        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'WhatIf' -Detail 'Create skipped due to ShouldProcess.')) | Out-Null
        continue
    }

    $createBody = ConvertTo-JsonBody -BodyMap (New-CreateBodyMap -Specification $spec)
    $createResult = Invoke-QboRequest -Method 'POST' -Uri $accountUrl -Headers $headers -Body $createBody

    if ($createResult.Success) {
        $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'Created' -Detail 'Created new account.')) | Out-Null
    } else {
        if (Test-IsDuplicateNameError -ErrorInfo $createResult.ErrorInfo) {
            $alternateName = Get-AlternateAccountName -Name $spec.Name -AccountNumber $spec.AccountNumber
            $alternateBody = ConvertTo-JsonBody -BodyMap (New-CreateBodyMap -Specification $spec -OverrideName $alternateName)
            $alternateResult = Invoke-QboRequest -Method 'POST' -Uri $accountUrl -Headers $headers -Body $alternateBody

            if ($alternateResult.Success) {
                $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'Created' -Detail "Created with alternate name '$alternateName'.")) | Out-Null
            } else {
                if (Test-IsAccountLimitError -ErrorInfo $alternateResult.ErrorInfo) {
                    $creationLimitReached = $true
                }

                $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'Failed' -Detail (Get-FriendlyErrorMessage -ErrorInfo $alternateResult.ErrorInfo))) | Out-Null
            }
        } else {
            if (Test-IsAccountLimitError -ErrorInfo $createResult.ErrorInfo) {
                $creationLimitReached = $true
            }

            $results.Add((New-ResultRecord -RowNumber $spec.RowNumber -AccountNumber $spec.AccountNumber -Name $spec.Name -Action 'Create' -Status 'Failed' -Detail (Get-FriendlyErrorMessage -ErrorInfo $createResult.ErrorInfo))) | Out-Null
        }
    }

    if ($RequestDelayMilliseconds -gt 0) {
        Start-Sleep -Milliseconds $RequestDelayMilliseconds
    }
}

$createdCount = @($results | Where-Object { $_.Status -eq 'Created' }).Count
$updatedCount = @($results | Where-Object { $_.Status -eq 'Updated' }).Count
$validatedCount = @($results | Where-Object { $_.Status -eq 'Validated' }).Count
$skippedCount = @($results | Where-Object { $_.Status -eq 'Skipped' }).Count
$whatIfCount = @($results | Where-Object { $_.Status -eq 'WhatIf' }).Count
$failedCount = @($results | Where-Object { $_.Status -eq 'Failed' }).Count

Write-Output ''
if ($ValidateOnly) {
    Write-Output ("QuickBooks COA validation complete. Validated={0} Failed={1} Total={2}" -f $validatedCount, $failedCount, $results.Count)
} else {
    Write-Output ("QuickBooks COA upsert complete. Created={0} Updated={1} Skipped={2} WhatIf={3} Failed={4} Total={5}" -f $createdCount, $updatedCount, $skippedCount, $whatIfCount, $failedCount, $results.Count)
}

if ($failedCount -gt 0) {
    Write-Output ''
    Write-Output 'First failures:'
    foreach ($failure in ($results | Where-Object { $_.Status -eq 'Failed' } | Select-Object -First 10)) {
        Write-Output ("  [{0}] {1} - {2}: {3}" -f $failure.RowNumber, $failure.AccountNumber, $failure.Name, $failure.Detail)
    }
}

if ($FailOnRowError -and $failedCount -gt 0) {
    exit 1
}
