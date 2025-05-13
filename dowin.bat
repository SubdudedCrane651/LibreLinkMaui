rem msbuild KIG2NETRIDE.sln /restore /t:build /p:TargetFramework=net8.0-windows10.0.19041.0 /p:configuration=release /p:WindowsAppSDKSelfContained=true /p:Platform="Any CPU" /p:Configuration=Debug /p:WindowsPackageType=None /p:RuntimeIdentifier=win10-x64

msbuild /restore /t:Publish /p:TargetFramework=net8.0-windows10.0.19041.0 /p:configuration=release /p:WindowsAppSDKSelfContained=true /p:Platform="Any CPU" /p:PublishSingleFile=true /p:Configuration=Release /p:WindowsPackageType=None /p:RuntimeIdentifier=win10-x64

rem dotnet publish -c Release -r win10-x64 --self-contained true -o n:\Visual Studio 2025\Projects

C:\Users\Public\Python\text2speech2.exe "The EXE file is completed, now you can test it"

pause