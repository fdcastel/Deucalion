#
# Requires Invoke-Build
#   Install-Module InvokeBuild -Scope AllUsers
#

$configuration = 'Release'
$publishFolder = './publish'
$BuildVersion = '0.0.0-dev'
$InformationalVersion = '0.0.0-dev'

# Floor for `dotnet test`. The xunit.v3 project runs on Microsoft.Testing.Platform
# (see global.json); if that opt-in is ever lost, `dotnet test` finds no tests
# and exits 0. The floor turns that into a failure (exit code 9). Keep it around
# 80% of the tests that run without DEUCALION_TESTS_NETWORK. CI runs this same
# task (.github/workflows/build.yml), so this is the only place to change it.
$MinimumExpectedTests = 101

# synopsis: Determine version using GitVersion (falls back to 0.0.0-dev).
# GitVersion comes from the repo-local tool manifest (.config/dotnet-tools.json),
# so `dotnet tool restore` is the only prerequisite. When the tool cannot be
# restored, or it cannot compute a version (shallow clone, git worktree, tarball
# export...), the build still goes through with the 0.0.0-dev placeholder.
task Version {
    $versionJson = $null

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "'dotnet tool restore' failed (exit code $LASTEXITCODE). Using fallback version $script:BuildVersion."
    }
    else {
        # Capture stdout only. GitVersion prints its own diagnostics on stderr,
        # which must reach the console rather than be parsed as JSON.
        $output = dotnet gitversion /output json
        if ($LASTEXITCODE -eq 0) {
            try {
                $versionJson = ($output | Out-String) | ConvertFrom-Json
            }
            catch {
                Write-Warning "Could not parse GitVersion output: $_"
            }
        }
        else {
            Write-Warning "'dotnet gitversion' failed (exit code $LASTEXITCODE); see the output above."
        }
    }

    if ($versionJson) {
        $script:BuildVersion = $versionJson.SemVer
        $script:InformationalVersion = $versionJson.InformationalVersion
    }
    else {
        Write-Warning "GitVersion unavailable. Using fallback version $script:BuildVersion."
    }

    Write-Output "Build Version: $script:BuildVersion"
    Write-Output "Informational Version: $script:InformationalVersion"
}

# synopsis: Remove build artifacts.
task Clear {
    remove $publishFolder,
        ./src/cs/*/bin,
        ./src/cs/*/obj,
        ./src/ts/deucalion-ui/bin,
        ./src/ts/deucalion-ui/obj,
        ./src/ts/deucalion-ui/dist
}

# synopsis: Remove build artifacts and node_modules.
task Clear-Npm Clear, {
    remove ./src/ts/deucalion-ui/node_modules
}

# synopsis: Clear and build projects.
task Build Version, Clear, {
    exec { dotnet publish './src/cs/Deucalion.Service/Deucalion.Service.csproj' -c $configuration -o $publishFolder -p:DebugType=None -p:Version=$BuildVersion -p:InformationalVersion=$InformationalVersion }

    exec { npm --prefix './src/ts/deucalion-ui' ci }

    # Quote '--' https://stackoverflow.com/a/72260631/332443
    exec { npm --prefix './src/ts/deucalion-ui' run build '--' --outDir "../../../$publishFolder/wwwroot" }
}

# synopsis: Start a development environment.
task Dev {
    # Start the frontend dev server in a new window and get its process
    $npmDevProcess = Start-Process powershell -ArgumentList "-Command", "npm --prefix './src/ts/deucalion-ui' run dev" -PassThru
    try {
        # Start the backend watcher. Waits for it to finish.
        exec { dotnet watch --project './src/cs/Deucalion.Api/Deucalion.Api.csproj' }
    }
    finally {
        # This block runs when dotnet watch exits (e.g., Ctrl+C)

        # Kill the process tree.
        taskkill /PID $npmDevProcess.Id /T /F > $null
    }
}

# synopsis: Run a production service.
task Prod {
    Set-Location './publish'
    Start-Process 'http://localhost:5000'

    exec { ./Deucalion.Service.exe --Deucalion:ConfigurationFile=../deucalion-sample.yaml }
}

# synopsis: Run the unit test suites and the frontend linter.
# End-to-end tests are separate -- they boot both servers and take ~30s:
#   npm --prefix ./src/ts/deucalion-ui run test:e2e
task Test {
    # -c Release so the trim/AOT analyzers run (src/cs/Directory.Build.props
    # gates them on Release) and local runs see the same warnings as CI.
    # `--` hands the option to the test host; without it `dotnet test` silently
    # ignores --minimum-expected-tests.
    exec { dotnet test -c Release -- --minimum-expected-tests $MinimumExpectedTests }
    exec { npm --prefix './src/ts/deucalion-ui' run test }
    exec { npm --prefix './src/ts/deucalion-ui' run lint }
}

task . Build
