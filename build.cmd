echo Restoring dotnet tools...
dotnet tool restore

dotnet paket install

dotnet fake build -t %*
