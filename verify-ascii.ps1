$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = $PSScriptRoot
$textExtensions = @('.cs', '.ps1', '.md', '.yml', '.yaml', '.json', '.xml', '.config', '.sln', '.csproj')
$textNames = @('.editorconfig', '.gitignore', '.gitattributes', 'LICENSE')
$failures = New-Object System.Collections.Generic.List[string]

$files = Get-ChildItem -LiteralPath $projectRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](\.git|dist|obj)[\\/]' -and
    ($textExtensions -contains $_.Extension.ToLowerInvariant() -or $textNames -contains $_.Name)
}

foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    for ($index = 0; $index -lt $bytes.Length; $index++) {
        if ($bytes[$index] -gt 0x7F) {
            $relativePath = $file.FullName.Substring($projectRoot.Length).TrimStart('\', '/')
            $failures.Add("$relativePath contains a non-ASCII byte at offset $index.")
            break
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "ASCII validation failed for $($failures.Count) file(s)."
}

Write-Host "ASCII validation passed for $($files.Count) text file(s)."
