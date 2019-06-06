if exist out rmdir /S /Q out
dotnet publish -r win-x64 -o out
set SHARPFUZZ_ENABLE_ON_BRANCH_CALLBACK=true
sharpfuzz out\PresentationFramework.dll
out\Wpf.Fuzz.exe
