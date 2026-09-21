$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$textExtensions = @('.cs', '.ps1', '.md', '.yml', '.yaml', '.json', '.xml', '.config', '.sln', '.csproj')
$textNames = @('.editorconfig', '.gitignore', '.gitattributes', 'LICENSE')
$localizedUtf8Names = @('README.zh-CN.md')
$failures = New-Object System.Collections.Generic.List[string]

$files = Get-ChildItem -LiteralPath $projectRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](\.git|dist|obj)[\\/]' -and
    ($textExtensions -contains $_.Extension.ToLowerInvariant() -or $textNames -contains $_.Name)
}
$asciiFiles = @($files | Where-Object { $localizedUtf8Names -notcontains $_.Name })
$localizedUtf8Files = @($files | Where-Object { $localizedUtf8Names -contains $_.Name })

foreach ($file in $asciiFiles) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    for ($index = 0; $index -lt $bytes.Length; $index++) {
        if ($bytes[$index] -gt 0x7F) {
            $relativePath = $file.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
            $failures.Add("$relativePath contains a non-ASCII byte at offset $index.")
            break
        }
    }
}

$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
foreach ($file in $localizedUtf8Files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $relativePath = $file.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $failures.Add("$relativePath contains a UTF-8 byte-order mark.")
        continue
    }

    try {
        $null = $strictUtf8.GetString($bytes)
    }
    catch {
        $failures.Add("$relativePath is not valid UTF-8.")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Encoding validation failed for $($failures.Count) file(s)."
}

Write-Host "Encoding validation passed for $($asciiFiles.Count) ASCII file(s) and $($localizedUtf8Files.Count) UTF-8 localized file(s)."
