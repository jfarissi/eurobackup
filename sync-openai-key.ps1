# Synchronise OpenAI, Gemini et la config IA documentaire (Ollama / profils) depuis appsettings.json vers python-service/.env
# Usage: .\sync-openai-key.ps1

$ErrorActionPreference = "Stop"

$appsettingsPath = "Backup.Web.Api.Server\appsettings.json"
$envPath = "python-service\.env"

if (-not (Test-Path $appsettingsPath)) {
    Write-Host "Erreur: $appsettingsPath introuvable" -ForegroundColor Red
    exit 1
}

try {
    $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    $pe = $appsettings.PythonExtractor
    if (-not $pe) {
        Write-Host "Erreur: section PythonExtractor absente" -ForegroundColor Red
        exit 1
    }

    $openAiKey = $pe.OpenAiApiKey
    $geminiKey = $pe.GeminiApiKey

    $envContent = @()
    $openAiKeyFound = $false
    $geminiKeyFound = $false

    $managedPrefixes = @(
        '^DOCUMENT_AI_',
        '^OLLAMA_ACTIVE_PROFILE=',
        '^OLLAMA_PROFILE_',
        '^OLLAMA_FALLBACK_PROFILES=',
        '^OLLAMA_HOST='
    )

    function Test-ManagedLine([string]$line) {
        foreach ($p in $managedPrefixes) {
            if ($line -match $p) { return $true }
        }
        return $false
    }

    if (Test-Path $envPath) {
        $envLines = Get-Content $envPath
        foreach ($line in $envLines) {
            if ($line -match "^OPENAI_API_KEY=") {
                if (-not [string]::IsNullOrWhiteSpace($openAiKey)) {
                    $envContent += "OPENAI_API_KEY=$openAiKey"
                    $openAiKeyFound = $true
                }
            }
            elseif ($line -match "^GEMINI_API_KEY=") {
                if (-not [string]::IsNullOrWhiteSpace($geminiKey)) {
                    $envContent += "GEMINI_API_KEY=$geminiKey"
                    $geminiKeyFound = $true
                }
            }
            elseif (Test-ManagedLine $line) {
                continue
            }
            else {
                $envContent += $line
            }
        }
    }

    if (-not $openAiKeyFound -and -not [string]::IsNullOrWhiteSpace($openAiKey)) {
        $envContent += "OPENAI_API_KEY=$openAiKey"
    }
    if (-not $geminiKeyFound -and -not [string]::IsNullOrWhiteSpace($geminiKey)) {
        $envContent += "GEMINI_API_KEY=$geminiKey"
    }

    if (-not ($envContent -match "^USE_OPENAI=")) {
        $envContent += "USE_OPENAI=true"
    }
    if (-not ($envContent -match "^OPENAI_MODEL=")) {
        $envContent += "OPENAI_MODEL=gpt-4o"
    }
    if (-not ($envContent -match "^GEMINI_MODEL=")) {
        $envContent += "GEMINI_MODEL=gemini-1.5-flash"
    }

    # --- Document AI (Python /parse + Ollama) ---
    $useAi = $false
    if ($null -ne $pe.UseAiForPythonParse) { $useAi = [bool]$pe.UseAiForPythonParse }
    $envContent += "DOCUMENT_AI_USE_AI=$(if ($useAi) { 'true' } else { 'false' })"

    $defProv = if ([string]::IsNullOrWhiteSpace($pe.DefaultAiProvider)) { "openai" } else { $pe.DefaultAiProvider.Trim() }
    $envContent += "DOCUMENT_AI_DEFAULT_PROVIDER=$defProv"

    $da = $pe.DocumentAi
    if ($da) {
        $hostVal = $da.OllamaHost
        if ([string]::IsNullOrWhiteSpace($hostVal) -and $appsettings.Ollama -and $appsettings.Ollama.Host) {
            $hostVal = $appsettings.Ollama.Host
        }
        if (-not [string]::IsNullOrWhiteSpace($hostVal)) {
            $envContent += "OLLAMA_HOST=$($hostVal.Trim().TrimEnd('/'))"
        }

        if (-not [string]::IsNullOrWhiteSpace($da.ActiveProfile)) {
            $envContent += "OLLAMA_ACTIVE_PROFILE=$($da.ActiveProfile.Trim())"
        }

        if ($da.Profiles -and $da.Profiles.PSObject.Properties) {
            foreach ($prop in $da.Profiles.PSObject.Properties) {
                $profileName = $prop.Name
                $val = $prop.Value
                if ($null -eq $val) { continue }
                $model = $val.Model
                if ([string]::IsNullOrWhiteSpace($model)) { continue }
                $suffix = ($profileName -replace '[^a-zA-Z0-9]', '_').ToUpperInvariant().Trim('_')
                $envContent += "OLLAMA_PROFILE_$suffix=$model"
            }
        }

        if ($da.FallbackProfiles -and @($da.FallbackProfiles).Count -gt 0) {
            $envContent += "OLLAMA_FALLBACK_PROFILES=$(@($da.FallbackProfiles) -join ',')"
        }

        $active = $da.ActiveProfile
        if (-not [string]::IsNullOrWhiteSpace($active) -and $da.Profiles -and $da.Profiles.PSObject.Properties) {
            $profProp = $da.Profiles.PSObject.Properties | Where-Object { $_.Name -eq $active } | Select-Object -First 1
            if ($profProp -and $profProp.Value -and $profProp.Value.Model) {
                $envContent += "OLLAMA_MODEL=$($profProp.Value.Model.Trim())"
            }
        }
    }

    $envContent | Set-Content $envPath -Encoding UTF8

    Write-Host "OK: $envPath mis a jour (cles API + DOCUMENT_AI_* + Ollama profils)." -ForegroundColor Green
    Write-Host "Redemarrez le service Python pour appliquer." -ForegroundColor Yellow
}
catch {
    Write-Host "Erreur: $_" -ForegroundColor Red
    exit 1
}
