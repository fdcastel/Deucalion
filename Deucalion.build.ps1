#
# Requires Invoke-Build
#   Install-Module InvokeBuild -Scope AllUsers
#

$configuration = 'Release'
$publishFolder = './publish'

# synopsis: Remove build artifacts.
task Clear {
    remove */dist,
        $publishFolder,
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
task Build Clear, {
    exec { dotnet publish './src/cs/Deucalion.Service/Deucalion.Service.csproj' -c $configuration -o $publishFolder -p:DebugType=None --self-contained }

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

# synopsis: Run test suite.
task Test {
    exec { dotnet test }
}

task . Build
