#
# Requires Invoke-Build
#   Install-Module InvokeBuild -Scope AllUsers
#

$configuration = 'Release'
$publishFolder = './publish'

task Clean {
    Remove-BuildItem */dist, $publishFolder,
        .\src\cs\*\bin, 
        .\src\cs\*\obj, 
        .\src\ts\deucalion-ui\bin,
        .\src\ts\deucalion-ui\obj,
        .\src\ts\deucalion-ui\dist

}

task Clean-Full Clean, {
    Remove-BuildItem .\src\ts\deucalion-ui\node_modules
}

task Build {
    exec { dotnet publish './src/cs/Deucalion.Service/Deucalion.Service.csproj' -c $configuration -o $publishFolder -p:DebugType=None --self-contained }

    exec { npm --prefix './src/ts/deucalion-ui' ci }

    exec { npm --prefix './src/ts/deucalion-ui' run build -- --outDir "../../../$publishFolder/wwwroot" }
}

task Test Build, {
    # Build and run a production service

    Set-Location './publish'
    Start-Process 'http://localhost:5000'

    exec { ./Deucalion.Service.exe /Deucalion:ConfigurationFile=..\deucalion-sample.yaml }
}

task . Clean, Build
