#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ─────────────────────────────────────────────────────────────────────────────
#  Helper functions
# ─────────────────────────────────────────────────────────────────────────────

function Write-Rule {
    param([string]$Title)
    Write-Host ("`n--- $Title ---") -ForegroundColor Yellow
}

function Get-Median {
    param([double[]]$sorted)
    $n = $sorted.Count
    if ($n -eq 0) { return 0.0 }
    if ($n % 2 -eq 1) { return $sorted[($n - 1) / 2] }
    return ($sorted[($n / 2) - 1] + $sorted[$n / 2]) / 2.0
}

function Get-Percentile {
    param([double[]]$sorted, [double]$p)
    $n = $sorted.Count
    if ($n -eq 0) { return 0.0 }
    $idx = $p / 100.0 * ($n - 1)
    $lo  = [int][math]::Floor($idx)
    $hi  = [int][math]::Ceiling($idx)
    if ($lo -eq $hi) { return $sorted[$lo] }
    return $sorted[$lo] + ($idx - $lo) * ($sorted[$hi] - $sorted[$lo])
}

function Get-StdDev {
    param([double[]]$arr)
    $n = $arr.Count
    if ($n -lt 2) { return 0.0 }
    $mean = ($arr | Measure-Object -Average).Average
    $variance = ($arr | ForEach-Object { ($_ - $mean) * ($_ - $mean) } | Measure-Object -Sum).Sum / ($n - 1)
    return [math]::Sqrt($variance)
}

function Get-Skewness {
    param([double[]]$arr)
    $n = $arr.Count
    if ($n -lt 3) { return 0.0 }
    $mean = ($arr | Measure-Object -Average).Average
    $s    = Get-StdDev $arr
    if ($s -eq 0) { return 0.0 }
    $sum3 = ($arr | ForEach-Object { [math]::Pow(($_ - $mean) / $s, 3) } | Measure-Object -Sum).Sum
    return ($n / (($n - 1) * ($n - 2))) * $sum3
}

function Get-PearsonR {
    param([double[]]$x, [double[]]$y)
    $n = [math]::Min($x.Count, $y.Count)
    if ($n -lt 2) { return 0.0 }
    $mx = ($x | Measure-Object -Average).Average
    $my = ($y | Measure-Object -Average).Average
    $num = 0.0; $dx2 = 0.0; $dy2 = 0.0
    for ($i = 0; $i -lt $n; $i++) {
        $dx = $x[$i] - $mx; $dy = $y[$i] - $my
        $num += $dx * $dy; $dx2 += $dx * $dx; $dy2 += $dy * $dy
    }
    $denom = [math]::Sqrt($dx2 * $dy2)
    if ($denom -eq 0) { return 0.0 }
    return [math]::Round($num / $denom, 4)
}

function Get-ColStats {
    param([string]$Name, [object[]]$raw)
    $total   = $raw.Count
    $missing = @($raw | Where-Object { $null -eq $_ -or $_ -eq '' }).Count
    $vals    = [double[]]@($raw | Where-Object { $null -ne $_ -and $_ -ne '' } | ForEach-Object { [double]$_ })
    $n       = $vals.Count

    if ($n -eq 0) {
        return [PSCustomObject]@{
            Column=$Name; Count=$total; Missing=$missing
            Min=0.0; Q1=0.0; Median=0.0; Mean=0.0; Q3=0.0; Max=0.0
            StdDev=0.0; Skewness=0.0; Outliers=@()
        }
    }

    $sorted   = [double[]]($vals | Sort-Object)
    $mean     = ($vals | Measure-Object -Average).Average
    $minV     = $sorted[0]
    $maxV     = $sorted[-1]
    $q1       = Get-Percentile -sorted $sorted -p 25
    $q3       = Get-Percentile -sorted $sorted -p 75
    $med      = Get-Median -sorted $sorted
    $std      = Get-StdDev -arr $vals
    $skew     = Get-Skewness -arr $vals
    $iqr      = $q3 - $q1
    $loFence  = $q1 - 1.5 * $iqr
    $hiFence  = $q3 + 1.5 * $iqr
    $outliers = @($vals | Where-Object { $_ -lt $loFence -or $_ -gt $hiFence })

    return [PSCustomObject]@{
        Column=$Name; Count=$total; Missing=$missing
        Min=[math]::Round($minV,4); Q1=[math]::Round($q1,4); Median=[math]::Round($med,4)
        Mean=[math]::Round($mean,4); Q3=[math]::Round($q3,4); Max=[math]::Round($maxV,4)
        StdDev=[math]::Round($std,4); Skewness=[math]::Round($skew,4)
        Outliers=$outliers
    }
}

function Write-Bar {
    param([string]$Label, [double]$Value, [double]$MaxVal, [int]$Width = 40, [string]$Color = 'Cyan')
    $len = if ($MaxVal -gt 0) { [int]($Value / $MaxVal * $Width) } else { 0 }
    $bar = '#' * $len
    Write-Host ("  {0,-24} " -f $Label) -NoNewline
    Write-Host ("{0,-44}" -f $bar) -NoNewline -ForegroundColor $Color
    Write-Host (" {0:F2}" -f $Value)
}

function Write-BarChart {
    param([string]$Title, [hashtable]$Data, [string]$Color = 'Cyan')
    Write-Host ("`n  $Title") -ForegroundColor White
    Write-Host ('  ' + ('-' * 72))
    $maxVal = ($Data.Values | Measure-Object -Maximum).Maximum
    foreach ($entry in ($Data.GetEnumerator() | Sort-Object Key)) {
        Write-Bar -Label $entry.Key -Value $entry.Value -MaxVal $maxVal -Color $Color
    }
}

function Write-Histogram {
    param([string]$Title, [double[]]$Values, [int]$Bins = 10)
    if ($Values.Count -eq 0) { return }
    $minV = ($Values | Measure-Object -Minimum).Minimum
    $maxV = ($Values | Measure-Object -Maximum).Maximum
    $w    = if ($maxV -gt $minV) { ($maxV - $minV) / $Bins } else { 1.0 }
    $counts = New-Object int[] $Bins
    foreach ($v in $Values) {
        $idx = [int][math]::Floor(($v - $minV) / $w)
        if ($idx -ge $Bins) { $idx = $Bins - 1 }
        $counts[$idx] += 1
    }
    Write-Host ("`n  $Title -- Histogram") -ForegroundColor White
    Write-Host ('  ' + ('-' * 72))
    $maxC = ($counts | Measure-Object -Maximum).Maximum
    for ($i = 0; $i -lt $Bins; $i++) {
        $lo    = [math]::Round($minV + $i * $w, 1)
        $hi    = [math]::Round($lo + $w, 1)
        $label = "$lo - $hi"
        Write-Bar -Label $label -Value $counts[$i] -MaxVal $maxC -Color DarkCyan
    }
}

function Write-FunnelChart {
    param([string]$Title, $Steps)
    Write-Host ("`n  $Title") -ForegroundColor White
    Write-Host ('  ' + ('-' * 72))
    $first = $null
    foreach ($entry in $Steps.GetEnumerator()) {
        if ($null -eq $first) { $first = [double]$entry.Value }
        $ratio = if ($first -gt 0) { [double]$entry.Value / $first } else { 0.0 }
        $len   = [int]($ratio * 50)
        $bar   = '#' * $len
        $pct   = "{0:F1}%" -f ($ratio * 100)
        Write-Host ("  {0,-22} " -f $entry.Key) -NoNewline
        Write-Host ("{0,-54}" -f $bar) -NoNewline -ForegroundColor Green
        Write-Host ("  {0,5}  ({1})" -f $entry.Value, $pct) -ForegroundColor Gray
    }
    Write-Host ('  ' + ('-' * 72))
}

# ─────────────────────────────────────────────────────────────────────────────
#  Load data
# ─────────────────────────────────────────────────────────────────────────────

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csvPath   = Join-Path $scriptDir 'Data\sample_ecommerce.csv'

Write-Host "`n  ===== FUNNEL EDA =====" -ForegroundColor Cyan
Write-Host "  Exploratory Data Analysis | E-Commerce Funnel Dataset`n" -ForegroundColor White

if (-not (Test-Path $csvPath)) {
    Write-Error "CSV not found: $csvPath"
    exit 1
}

$rows = Import-Csv $csvPath
Write-Host ("  Loaded {0} rows from {1}" -f $rows.Count, $csvPath) -ForegroundColor Green

$numColNames = @('age','session_duration_sec','pages_viewed','added_to_cart','purchased','order_value')

# ─────────────────────────────────────────────────────────────────────────────
#  1. Descriptive Statistics
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '1. Descriptive Statistics'

$allStats = New-Object System.Collections.Generic.List[PSObject]
foreach ($col in $numColNames) {
    $raw  = @($rows | ForEach-Object { $_.$col })
    $stat = Get-ColStats -Name $col -raw $raw
    $allStats.Add($stat)
}

Write-Host ("  {0,-22} {1,6} {2,8} {3,8} {4,8} {5,8} {6,8} {7,8} {8,8} {9,8} {10,8} {11,8}" -f `
    'Column','Count','Missing','Min','Q1','Median','Mean','Q3','Max','StdDev','Skew','Outliers') -ForegroundColor White
Write-Host ('  ' + ('-' * 110))

foreach ($s in $allStats) {
    $mColor = if ($s.Missing -gt 0) { 'Red' } else { 'Gray' }
    $oColor = if ($s.Outliers.Count -gt 0) { 'DarkYellow' } else { 'Gray' }
    Write-Host ("  {0,-22} {1,6} " -f $s.Column, $s.Count) -NoNewline
    Write-Host ("{0,8} " -f $s.Missing) -NoNewline -ForegroundColor $mColor
    Write-Host ("{0,8:F2} {1,8:F2} {2,8:F2} {3,8:F2} {4,8:F2} {5,8:F2} {6,8:F2} {7,8:F2} " -f `
        $s.Min, $s.Q1, $s.Median, $s.Mean, $s.Q3, $s.Max, $s.StdDev, $s.Skewness) -NoNewline
    Write-Host ("{0,8}" -f $s.Outliers.Count) -ForegroundColor $oColor
}

# ─────────────────────────────────────────────────────────────────────────────
#  2. Missing Values
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '2. Missing Values'

$missingCols = @($allStats | Where-Object { $_.Missing -gt 0 })
if ($missingCols.Count -eq 0) {
    Write-Host '  No missing values found.' -ForegroundColor Green
} else {
    $mData = @{}
    foreach ($s in $missingCols) { $mData[$s.Column] = [double]$s.Missing }
    Write-BarChart -Title 'Missing Values per Column' -Data $mData -Color Red
    foreach ($s in $missingCols) {
        $pct = [math]::Round($s.Missing / $s.Count * 100, 1)
        Write-Host ("  {0,-25}  {1} missing ({2}%)" -f $s.Column, $s.Missing, $pct) -ForegroundColor DarkYellow
    }
}

# ─────────────────────────────────────────────────────────────────────────────
#  3. Outlier Detection
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '3. Outlier Detection (IQR Method)'

$foundOutliers = $false
foreach ($s in $allStats) {
    if ($s.Outliers.Count -gt 0) {
        $foundOutliers = $true
        $vals = ($s.Outliers | Select-Object -First 5 | ForEach-Object { "{0:F1}" -f $_ }) -join ', '
        if ($s.Outliers.Count -gt 5) { $vals += ' ...' }
        Write-Host ("  {0,-25} {1,3} outlier(s)  {2}" -f $s.Column, $s.Outliers.Count, $vals) -ForegroundColor DarkYellow
    }
}
if (-not $foundOutliers) { Write-Host '  No outliers detected.' -ForegroundColor Green }

# ─────────────────────────────────────────────────────────────────────────────
#  4. Correlation Analysis (Pearson r)
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '4. Correlation Analysis (Pearson r)'

$completeRows = @($rows | Where-Object {
    $r = $_
    $ok = $true
    foreach ($col in $numColNames) {
        if ($r.$col -eq '' -or $null -eq $r.$col) { $ok = $false; break }
    }
    $ok
})
Write-Host ("  Using {0} complete rows (all numeric columns populated)." -f $completeRows.Count)

$arrays = @{}
foreach ($col in $numColNames) {
    $arrays[$col] = [double[]]@($completeRows | ForEach-Object { [double]$_.$col })
}

$corrPairs = New-Object System.Collections.Generic.List[PSObject]
for ($ci = 0; $ci -lt $numColNames.Count; $ci++) {
    for ($cj = $ci + 1; $cj -lt $numColNames.Count; $cj++) {
        $r = Get-PearsonR -x $arrays[$numColNames[$ci]] -y $arrays[$numColNames[$cj]]
        $corrPairs.Add([PSCustomObject]@{
            ColumnA  = $numColNames[$ci]
            ColumnB  = $numColNames[$cj]
            PearsonR = $r
        })
    }
}
$corrSorted = @($corrPairs | Sort-Object { [math]::Abs($_.PearsonR) } -Descending)

Write-Host ("`n  {0,-25} {1,-25} {2,10}  {3}" -f 'Column A','Column B','Pearson r','Strength') -ForegroundColor White
Write-Host ('  ' + ('-' * 72))
foreach ($c in ($corrSorted | Select-Object -First 15)) {
    $abs      = [math]::Abs($c.PearsonR)
    $strength = if ($abs -ge 0.7) { 'Strong' } elseif ($abs -ge 0.4) { 'Moderate' } elseif ($abs -ge 0.2) { 'Weak' } else { 'Negligible' }
    $rColor   = if ($c.PearsonR -ge 0) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-25} {1,-25} " -f $c.ColumnA, $c.ColumnB) -NoNewline
    Write-Host ("{0,10}  " -f $c.PearsonR) -NoNewline -ForegroundColor $rColor
    Write-Host $strength
}

# ─────────────────────────────────────────────────────────────────────────────
#  5. Distributions
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '5. Distributions'

$ageVals = [double[]]@($rows | Where-Object { $_.age -ne '' } | ForEach-Object { [double]$_.age })
$sesVals = [double[]]@($rows | ForEach-Object { [double]$_.session_duration_sec })
$ordVals = [double[]]@($rows | Where-Object { $_.order_value -ne '' } | ForEach-Object { [double]$_.order_value })

Write-Histogram -Title 'Age' -Values $ageVals
Write-Histogram -Title 'Session Duration (sec)' -Values $sesVals
Write-Histogram -Title 'Order Value ($)' -Values $ordVals

# ─────────────────────────────────────────────────────────────────────────────
#  6. Categorical Breakdown
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '6. Categorical Breakdown'

$deviceData = @{}
$rows | Group-Object device | ForEach-Object { $deviceData[$_.Name] = [double]$_.Count }
Write-BarChart -Title 'Users by Device' -Data $deviceData -Color Cyan

$regionData = @{}
$rows | Group-Object region | ForEach-Object { $regionData[$_.Name] = [double]$_.Count }
Write-BarChart -Title 'Users by Region' -Data $regionData -Color Magenta

# ─────────────────────────────────────────────────────────────────────────────
#  7. Conversion Funnel
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '7. Conversion Funnel'

$funnel = [ordered]@{
    'Visited'         = $rows.Count
    'Viewed 3+ pages' = @($rows | Where-Object { [int]$_.pages_viewed -ge 3 }).Count
    'Added to Cart'   = @($rows | Where-Object { [int]$_.added_to_cart -eq 1 }).Count
    'Purchased'       = @($rows | Where-Object { [int]$_.purchased -eq 1 }).Count
}
Write-FunnelChart -Title 'E-Commerce Conversion Funnel' -Steps $funnel

# ─────────────────────────────────────────────────────────────────────────────
#  8. Average Order Value by Region
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '8. Average Order Value by Region'

$aovData = @{}
$rows | Where-Object { $_.order_value -ne '' } | Group-Object region | ForEach-Object {
    $grp = $_
    $avg = ($grp.Group | ForEach-Object { [double]$_.order_value } | Measure-Object -Average).Average
    $aovData[$grp.Name] = [math]::Round($avg, 2)
}
Write-BarChart -Title 'Average Order Value by Region ($)' -Data $aovData -Color Yellow

# ─────────────────────────────────────────────────────────────────────────────
#  9. Export JSON Report
# ─────────────────────────────────────────────────────────────────────────────

Write-Rule '9. Export JSON Report'

$report = [ordered]@{
    generated_at = (Get-Date -Format 'o')
    row_count    = $rows.Count
    column_stats = @($allStats | ForEach-Object {
        $s = $_
        [ordered]@{
            column        = $s.Column
            count         = $s.Count
            missing       = $s.Missing
            missing_pct   = [math]::Round($s.Missing / $s.Count * 100, 2)
            min           = $s.Min
            q1            = $s.Q1
            median        = $s.Median
            mean          = $s.Mean
            q3            = $s.Q3
            max           = $s.Max
            std_dev       = $s.StdDev
            skewness      = $s.Skewness
            outlier_count = $s.Outliers.Count
        }
    })
    correlations = @($corrSorted | ForEach-Object {
        $c = $_
        [ordered]@{ column_a = $c.ColumnA; column_b = $c.ColumnB; pearson_r = $c.PearsonR }
    })
    funnel = $funnel
}

$reportPath = Join-Path $scriptDir 'eda_report.json'
$report | ConvertTo-Json -Depth 5 | Set-Content -Path $reportPath -Encoding UTF8

Write-Host ("  Report saved: {0}" -f $reportPath) -ForegroundColor Green
Write-Host "`n  EDA complete!" -ForegroundColor Green
