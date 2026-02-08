dotnet ef migrations add ChunkHashIndex \
--project BitNest.csproj \
--startup-project BitNest.csproj \
--context BitNest.Data.AppDbContext \
--configuration Debug \
--output-dir Migrations
