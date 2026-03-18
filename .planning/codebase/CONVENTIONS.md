# Conventions

## C# and Backend Code Style
- Modern C# features are enabled (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`) in `BitNest/BitNest.csproj`.
- Classes and methods use PascalCase throughout `BitNest/`.
- Private fields in `StorageController` and `StorageService` use lower camel case (for example `storageService`, `uploadsPath`).
- Dependency injection is constructor-based in `BitNest/Controllers/StorageController.cs` and `BitNest/Services/StorageService.cs`.

## API Patterns
- Controller endpoints are thin and delegate to service methods in `BitNest/Controllers/StorageController.cs`.
- Route style uses attribute routing with explicit HTTP verbs.
- Pagination logic is in service layer (`Skip`/`Take`) and serialized into DTO JSON in `BitNest/Services/StorageService.cs`.
- Soft deletion convention uses `IsDeleted` marker on `FileMetadata` rather than hard delete.

## Persistence Conventions
- EF Core entities in `BitNest/Models/` map directly to table concepts.
- Keys are explicitly declared in `BitNest/Data/AppDbContext.cs`.
- Join entity `FileChunk` models many-to-many-like relation with ordering.
- Migrations are committed under `BitNest/Migrations/` and auto-applied on startup.

## Error Handling and Logging
- Service catches `IOException` and `OperationCanceledException` around uploads in `BitNest/Services/StorageService.cs`.
- Logging convention uses structured Serilog placeholders (`{progress}`, `{message}`).
- Controller translates missing records to `NotFound()` in `BitNest/Controllers/StorageController.cs`.
- Exception handling in controllers is broad (`catch (Exception)`), indicating pragmatic MVP error boundaries.

## Frontend Conventions
- Plain functions and file-level state in `FrontEnd/main.js` (no modules or classes).
- DOM is generated imperatively; CSS classes reflect semantic UI blocks (`file-info`, `file-controls`).
- API URL derivation is environment-implicit (`origin` replace) in `FrontEnd/main.js`.

## Consistency Gaps
- Typo in model property `Extention` in `BitNest/Models/FileMetadata.cs` suggests naming cleanup needed.
- Some response generation manually serializes JSON strings in service instead of returning typed objects.
- Mixed use of nullable assumptions in frontend and backend without explicit validation layer.
