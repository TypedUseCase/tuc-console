dotnet tool restore
dotnet tool run paket restore

dotnet run --project ./build/build.fsproj -- %*
