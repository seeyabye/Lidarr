#! /bin/bash
msBuildVersion='15.0'
outputFolder='./_output'
outputFolderWindows='./_output_windows'
outputFolderLinux='./_output_linux'
outputFolderMacOS='./_output_macos'
testPackageFolder='./_tests/'
sourceFolder='./src'
slnFile=$sourceFolder/Lidarr.sln
consoleApp=$sourceFolder/NzbDrone.Console/Lidarr.Console.csproj
updateApp=$sourceFolder/NzbDrone.Update/Lidarr.Update.csproj
updateFolder=$outputFolder/Lidarr.Update
updateFolderMono=$outputFolderLinux/Lidarr.Update

#Artifact variables
artifactsFolder="./_artifacts";
artifactsFolderWindows=$artifactsFolder/windows
artifactsFolderLinux=$artifactsFolder/linux
artifactsFolderMacOS=$artifactsFolder/macos

vswhere='tools/vswhere/vswhere.exe';

CheckExitCode()
{
    "$@"
    local status=$?
    if [ $status -ne 0 ]; then
        echo "error with $1" >&2
        exit 1
    fi
    return $status
}

ProgressStart()
{
    echo "Start '$1'"
}

ProgressEnd()
{
    echo "Finish '$1'"
}

BuildAndPublish(){
    rm -rf $outputFolderWindows
    rm -rf $outputFolderLinux
    rm -rf $outputFolderMacOS

    CheckExitCode dotnet restore $slnFile
    CheckExitCode dotnet publish $consoleApp -c Release --no-restore -r win-x64 -o $outputFolderWindows --self-contained false
    CheckExitCode dotnet publish $updateApp -c Release --no-restore -r win-x64 -o $outputFolderWindows/Lidarr.Update --self-contained false
    CheckExitCode dotnet publish $consoleApp -c Release --no-restore -r linux-x64 -o $outputFolderLinux --self-contained false
    CheckExitCode dotnet publish $updateApp -c Release --no-restore -r linux-x64 -o $outputFolderLinux/Lidarr.Update --self-contained false
    CheckExitCode dotnet publish $consoleApp -c Release --no-restore -r osx-x64 -o $outputFolderMacOS --self-contained false
    CheckExitCode dotnet publish $updateApp -c Release --no-restore -r osx-x64 -o $outputFolderMacOS/Lidarr.Update --self-contained false
}

PackageWindows()
{
    ProgressStart 'Cleaning Windows Package'

    echo "Removing Lidarr.Mono"
    rm -f $outputFolder/Lidarr.Mono.*

    echo "Adding Lidarr.Windows to UpdatePackage"
    cp $outputFolder/Lidarr.Windows.* $updateFolder

    echo "Removing MacOS fpcalc"
    rm $outputFolder/fpcalc

    echo "Adding UI"
    cp -r $outputFolder/UI $outputFolderWindows

    ProgressEnd 'Cleaning Windows Package'
}

PackageLinux()
{
    ProgressStart 'Creating Linux Package'

    echo "Removing Service helpers"
    rm -f $outputFolderLinux/ServiceUninstall.*
    rm -f $outputFolderLinux/ServiceInstall.*

    echo "Removing native windows binaries Sqlite, fpcalc"
    rm -f $outputFolderLinux/sqlite3.*
    rm -f $outputFolderLinux/fpcalc*

    echo "Adding CurlSharp.dll.config (for dllmap)"
    cp $sourceFolder/NzbDrone.Common/CurlSharp.dll.config $outputFolderLinux

    echo "Renaming Lidarr.Console to Lidarr"
    for file in $outputFolderLinux/Lidarr.Console*; do
        mv "$file" "${file//.Console/}"
    done

    echo "Removing Lidarr.Windows"
    rm $outputFolderLinux/Lidarr.Windows.*

    echo "Adding Lidarr.Mono to UpdatePackage"
    cp $outputFolderLinux/Lidarr.Mono.* $updateFolderMono

    echo "Adding UI"
    cp -r $outputFolder/UI $outputFolderLinux

    ProgressEnd 'Creating Linux Package'
}

PackageMacOS()
{
    ProgressStart 'Creating MacOS Package'

    echo "Renaming Lidarr.Console to Lidarr"
    for file in $outputFolderLinux/Lidarr.Console*; do
        mv "$file" "${file//.Console/}"
    done

    echo "Adding sqlite dylibs"
    cp $sourceFolder/Libraries/Sqlite/*.dylib $outputFolderMacOS

    echo "Adding UI"
    cp -r $outputFolder/UI $outputFolderMacOS

    ProgressEnd 'Creating MacOS Package'
}

PackageArtifacts()
{
    echo "Creating Artifact Directories"
    
    rm -rf $artifactsFolder
    mkdir $artifactsFolder
    
    mkdir $artifactsFolderWindows
    mkdir $artifactsFolderMacOS
    mkdir $artifactsFolderLinux
    mkdir $artifactsFolderWindows/Lidarr
    mkdir $artifactsFolderMacOS/Lidarr
    mkdir $artifactsFolderLinux/Lidarr

    cp -r $outputFolderWindows/* $artifactsFolderWindows/Lidarr
    cp -r $outputFolderMacOS/* $artifactsFolderMacOS/Lidarr
    cp -r $outputFolderLinux/* $artifactsFolderLinux/Lidarr
}

BuildAndPublish
PackageWindows
PackageLinux
PackageMacOS
PackageArtifacts