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
    exec { npm --prefix './src/ts/deucalion-ui' run build -- --outDir "../../../$publishFolder/wwwroot" }
}

# synopsis: Start a development environment.
task Dev {
    Start-Process powershell { npm --prefix './src/ts/deucalion-ui' run dev }
    exec { dotnet watch --project './src/cs/Deucalion.Api/Deucalion.Api.csproj' }
}

# synopsis: Run a production service.
task Prod {
    Set-Location './publish'
    Start-Process 'http://localhost:5000'

    exec { ./Deucalion.Service.exe /Deucalion:ConfigurationFile=../deucalion-sample.yaml }
}

# synopsis: Run test suite.
task Test {
    exec { dotnet test }
}

task . Build
