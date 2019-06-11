if exist out rmdir /S /Q out
dotnet publish -r win-x64 -o out
sharpfuzz out\PresentationCore.dll
sharpfuzz out\PresentationFramework.dll
out\Wpf.Fuzz.exe
