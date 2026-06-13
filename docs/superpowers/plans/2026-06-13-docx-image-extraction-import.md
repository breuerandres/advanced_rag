# DOCX Image Extraction on Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a user imports a `.docx`, extract its embedded images, normalize them with SkiaSharp, store them in MinIO, create the draft document immediately with the filename as its title, and reference the images through the existing stable `/api/document-images/{imageId}/content` URLs.

**Architecture:** Importing a DOCX becomes a draft-creating operation (not just editor prefill). A new App-layer orchestrator (`DocumentImportService`) generates the `documentId`, asks the Infrastructure extraction service for text + final stable-URL HTML + normalized image bytes (Mammoth `ImageConverter` + SkiaSharp normalization), persists a minimal draft (filename title, **no access rules yet**) directly through `IDocumentRepository`, then writes each image to MinIO + `app.document_images`. The import-created draft is **role-gated only** (controller `[Authorize]`) — it deliberately bypasses the rule-based `CanManageDraftAsync` check, which returns `false` for empty rules. The user fills access rules afterward and saves normally. PDF import is unchanged (text-only prefill); the frontend sets its title from the filename client-side.

**Tech Stack:** .NET 8 (Mammoth 1.11.0, SkiaSharp 2.88.x, AWSSDK.S3 3.7.x, Ganss.Xss), EF Core, React 18 + TypeScript (manage-web), xUnit + FluentAssertions, `WebApplicationFactory<Program>` integration tests.

---

## Scope & Non-Goals

**In scope (Phase 1):** DOCX-only image extraction → MinIO; draft created at import time; filename → document title (DOCX server-side + PDF client-side); SkiaSharp normalization (decode, downscale cap, re-encode non-web-safe to WebP, skip undecodable/tiny).

**Out of scope (documented follow-ups):**
- **MinIO/DB cleanup of orphaned images** (on `<img>` removed from editor, on archive, or on a future hard-delete). Today image objects are immortal; this plan does **not** change that. Tracked as tech debt.
- **PDF image extraction** (PdfPig best-effort) — future Phase 3.
- **Cross-occurrence dedup** by SHA-256 within a single import — future Phase 1.1. Each surviving `<img>` gets its own `imageId`/object/row.
- **Transactional image persistence** — Phase 1 persists images best-effort after the draft row exists (object store is not transactional anyway).

## Key Facts (verified in codebase, do not re-derive)

- `app.document_images` table already exists (migration `20260531170000_AddDocumentImages`), FK `document_id → app.documents ON DELETE CASCADE`, unique index on `object_key`. **No new migration is required.**
- `IDocumentImageObjectStorage` (`PutAsync`/`GetAsync`) and `IDocumentImageRepository` (`AddAsync`/`FindAsync`) already exist and are DI-registered in `Program.cs`. The MinIO bucket policy already grants `PutObject`.
- Stable image URL shape enforced everywhere: `^/api/document-images/{guid}/content$` (`DocumentLifecycleService.StableDocumentImageSourcePattern`, FastAPI `STABLE_IMAGE_SRC_PATTERN`). Build exactly this.
- Object key shape (from `DocumentImageService.UploadAsync`): `documents/{documentId:D}/images/{imageId:D}/{sha256}{extension}`.
- `DocumentAccessPolicy.CanManageDraftAsync` returns **`false`** when `rules.Count == 0`. The import-created draft must therefore **not** be gated by it.
- `IDocumentRepository.SaveAsync` persists a draft with zero access rules without error (it deletes+re-adds permission rows; zero is valid).
- Existing `IDocumentImportExtractionService.ExtractAsync` (text-only, strips `<img>`) stays unchanged and is still used by `POST /api/documents/imports/extract` (PDF + fallback).

## File Structure

**Create:**
- `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageObjectKey.cs` — shared object-key + content-type→extension helper (DRY across `DocumentImageService` and import).
- `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/ImportImageNormalizer.cs` — SkiaSharp decode/downscale/re-encode; returns `null` to signal "skip".
- `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportService.cs` — orchestrator (`IDocumentImportService`).
- `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/ImportImageNormalizerTests.cs`
- `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImportServiceTests.cs`
- `services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImportEndpointTests.cs`

**Modify:**
- `services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj` — add SkiaSharp packages.
- `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportTypes.cs` — new types + extend extraction interface + import-service interface.
- `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs` — use the shared object-key helper.
- `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/DocumentImportExtractionService.cs` — implement `ExtractDocxWithImagesAsync`.
- `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentsController.cs` — add `POST imports/docx`.
- `services/dotnet-api/src/AdvancedRag.Api/Program.cs` — register `ImportImageNormalizer` + `IDocumentImportService`.
- `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/DocumentImportExtractionTests.cs` — fix service construction + add image tests.
- `apps/manage-web/src/api/documents.ts` — add `importDocx`.
- `apps/manage-web/src/features/documents/DocumentsPage.tsx` — branch `runImport` by mime; set title from filename.
- `apps/manage-web/src/i18n/*` — new strings (es-AR default + en-US).
- `context/design-decisions.md`, `context/code-standards.md`, `context/progress-tracker.md` — record the decision.

---

## Task 1: Shared object-key helper (DRY)

**Files:**
- Create: `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageObjectKey.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs` (use helper in `DocumentImageService.UploadAsync`)
- Test: `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImageObjectKeyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImageObjectKeyTests.cs`:

```csharp
using AdvancedRag.App.DocumentImages;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentImageObjectKeyTests
{
    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/gif", ".gif")]
    public void ExtensionFor_KnownTypes_ReturnsExtension(string contentType, string expected)
    {
        DocumentImageObjectKey.TryGetExtension(contentType, out string? extension).Should().BeTrue();
        extension.Should().Be(expected);
    }

    [Fact]
    public void ExtensionFor_UnknownType_ReturnsFalse()
    {
        DocumentImageObjectKey.TryGetExtension("image/tiff", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_ComposesStableKey()
    {
        Guid documentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid imageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        string key = DocumentImageObjectKey.Build(documentId, imageId, "abc123", ".png");

        key.Should().Be(
            "documents/11111111-1111-1111-1111-111111111111/images/22222222-2222-2222-2222-222222222222/abc123.png");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImageObjectKeyTests"`
Expected: FAIL — `DocumentImageObjectKey` does not exist.

- [ ] **Step 3: Create the helper**

Create `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageObjectKey.cs`:

```csharp
namespace AdvancedRag.App.DocumentImages;

public static class DocumentImageObjectKey
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionsByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

    public static bool TryGetExtension(string contentType, out string? extension)
    {
        return ExtensionsByContentType.TryGetValue(NormalizeContentType(contentType), out extension);
    }

    public static string Build(Guid documentId, Guid imageId, string sha256, string extension)
    {
        return $"documents/{documentId:D}/images/{imageId:D}/{sha256}{extension}";
    }

    public static string NormalizeContentType(string contentType)
    {
        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Refactor `DocumentImageService.UploadAsync` to use the helper**

In `services/dotnet-api/src/AdvancedRag.App/DocumentImages/DocumentImageTypes.cs`, inside `DocumentImageService`:

- Delete the private `ExtensionsByContentType` dictionary field and the private `NormalizeContentType` method.
- Replace the content-type/extension block in `UploadAsync` with:

```csharp
        string contentType = DocumentImageObjectKey.NormalizeContentType(command.ContentType);
        if (!DocumentImageObjectKey.TryGetExtension(contentType, out string? extension))
        {
            throw new DocumentImageException(
                "DOCUMENT_IMAGE_TYPE_UNSUPPORTED",
                415,
                "Document image type is unsupported.",
                new Dictionary<string, object?> { ["contentType"] = command.ContentType });
        }
```

- Replace the object-key line with:

```csharp
        string objectKey = DocumentImageObjectKey.Build(command.DocumentId, imageId, sha256, extension!);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImageObjectKeyTests|FullyQualifiedName~DocumentImageServiceTests"`
Expected: PASS (both the new helper tests and the existing `DocumentImageService` tests).

- [ ] **Step 6: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.App/DocumentImages services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImageObjectKeyTests.cs
git commit -m "refactor: extract shared DocumentImageObjectKey helper"
```

---

## Task 2: SkiaSharp image normalizer

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj`
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/ImportImageNormalizer.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/ImportImageNormalizerTests.cs`

- [ ] **Step 1: Add SkiaSharp packages**

In `services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj`, add to the package `ItemGroup` (use the latest stable **2.88.x**; do NOT use 3.x preview):

```xml
    <PackageReference Include="SkiaSharp" Version="2.88.9" />
    <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="2.88.9" />
```

> `NoDependencies` ships a self-contained `libSkiaSharp` so the Linux container needs no `libfontconfig`/`libfreetype` (we only decode/encode raster images, never render text/SVG). Verify the exact latest 2.88.x with `dotnet list package --outdated` after adding.

- [ ] **Step 2: Write the failing test**

Create `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/ImportImageNormalizerTests.cs`:

```csharp
using AdvancedRag.Infrastructure.Documents;
using FluentAssertions;
using SkiaSharp;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class ImportImageNormalizerTests
{
    [Fact]
    public void Normalize_WebSafePngWithinCap_PassesThroughUnchanged()
    {
        byte[] png = CreatePng(64, 64);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        NormalizedImage? result = normalizer.Normalize(png, "image/png");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/png");
        result.Content.Should().Equal(png);
    }

    [Fact]
    public void Normalize_OversizedImage_DownscalesAndReencodesToWebp()
    {
        byte[] png = CreatePng(4000, 100);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        NormalizedImage? result = normalizer.Normalize(png, "image/png");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/webp");
        using SKBitmap decoded = SKBitmap.Decode(result.Content);
        decoded.Width.Should().Be(2000);
        decoded.Height.Should().Be(50);
    }

    [Fact]
    public void Normalize_TinyDecoration_IsSkipped()
    {
        byte[] png = CreatePng(16, 16);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        normalizer.Normalize(png, "image/png").Should().BeNull();
    }

    [Fact]
    public void Normalize_UndecodableBytes_IsSkipped()
    {
        byte[] garbage = [0x01, 0x02, 0x03, 0x04, 0x05];
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        normalizer.Normalize(garbage, "image/x-emf").Should().BeNull();
    }

    private static byte[] CreatePng(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~ImportImageNormalizerTests"`
Expected: FAIL — `ImportImageNormalizer` / `NormalizedImage` not defined.

- [ ] **Step 4: Implement the normalizer**

Create `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/ImportImageNormalizer.cs`:

```csharp
using SkiaSharp;

namespace AdvancedRag.Infrastructure.Documents;

public sealed record NormalizedImage(byte[] Content, string ContentType);

/// <summary>
/// Decodes an imported raster image, drops icons/decorations below the minimum
/// size, keeps web-safe formats within the dimension cap unchanged, and otherwise
/// downscales + re-encodes to WebP. Returns <c>null</c> when the image must be
/// skipped (undecodable vector/metafile, or too small).
/// </summary>
public sealed class ImportImageNormalizer
{
    private static readonly HashSet<string> WebSafeContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp" };

    private readonly int _maxDimension;
    private readonly int _minDimension;
    private readonly int _webpQuality;

    public ImportImageNormalizer(int maxDimension = 2000, int minDimension = 32, int webpQuality = 80)
    {
        _maxDimension = maxDimension;
        _minDimension = minDimension;
        _webpQuality = webpQuality;
    }

    public NormalizedImage? Normalize(byte[] source, string sourceContentType)
    {
        using SKBitmap? bitmap = SKBitmap.Decode(source);
        if (bitmap is null)
        {
            return null; // EMF/WMF/TIFF/unsupported -> caller drops the <img>
        }

        if (bitmap.Width < _minDimension && bitmap.Height < _minDimension)
        {
            return null; // bullets/icons
        }

        string contentType = NormalizeContentType(sourceContentType);
        bool withinCap = bitmap.Width <= _maxDimension && bitmap.Height <= _maxDimension;
        if (withinCap && WebSafeContentTypes.Contains(contentType))
        {
            return new NormalizedImage(source, contentType == "image/jpg" ? "image/jpeg" : contentType);
        }

        using SKBitmap scaled = Downscale(bitmap);
        using SKData data = scaled.Encode(SKEncodedImageFormat.Webp, _webpQuality);
        if (data is null || data.Size == 0)
        {
            return null;
        }

        return new NormalizedImage(data.ToArray(), "image/webp");
    }

    private SKBitmap Downscale(SKBitmap bitmap)
    {
        if (bitmap.Width <= _maxDimension && bitmap.Height <= _maxDimension)
        {
            return bitmap.Copy();
        }

        double scale = (double)_maxDimension / Math.Max(bitmap.Width, bitmap.Height);
        int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        SKBitmap target = new(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType));
        bitmap.ScalePixels(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        return target;
    }

    private static string NormalizeContentType(string contentType)
    {
        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~ImportImageNormalizerTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.Infrastructure/AdvancedRag.Infrastructure.csproj services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/ImportImageNormalizer.cs services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/ImportImageNormalizerTests.cs
git commit -m "feat: add SkiaSharp import image normalizer"
```

---

## Task 3: App contracts for image-aware DOCX import

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportTypes.cs`

- [ ] **Step 1: Add the new types and interfaces**

Append to `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportTypes.cs`:

```csharp
public sealed record ImportImageContent(
    Guid ImageId,
    string ObjectKey,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string AltText,
    byte[] Content);

public sealed record DocxImportExtractionResult(
    string Text,
    string ContentHtml,
    ImportExtractionMetadata Metadata,
    IReadOnlyList<ImportImageContent> Images);

public sealed record ImportDocxCommand(
    string OriginalFilename,
    string MimeType,
    byte[] FileBytes,
    Guid ActorUserId,
    string RequestId);

public interface IDocumentImportService
{
    Task<DocumentAggregate> ImportDocxAsync(ImportDocxCommand command, CancellationToken ct);
}
```

- [ ] **Step 2: Extend the extraction interface**

In the same file, add a method to `IDocumentImportExtractionService`:

```csharp
public interface IDocumentImportExtractionService
{
    Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct);

    Task<DocxImportExtractionResult> ExtractDocxWithImagesAsync(
        ImportExtractionCommand command,
        Guid documentId,
        CancellationToken ct);
}
```

- [ ] **Step 3: Verify it compiles (interface only — implementation lands in Task 4)**

Run: `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`
Expected: FAIL — `DocumentImportExtractionService` does not implement `ExtractDocxWithImagesAsync` yet. (This is expected; Task 4 implements it. Do not commit a broken build — proceed to Task 4 before committing.)

---

## Task 4: Implement image extraction in the Infrastructure service

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/DocumentImportExtractionService.cs`
- Modify: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/DocumentImportExtractionTests.cs`

- [ ] **Step 1: Write the failing tests**

In `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/DocumentImportExtractionTests.cs`:

(a) Add a private factory and replace every `new DocumentImportExtractionService()` call with `CreateService()`:

```csharp
    private static DocumentImportExtractionService CreateService()
    {
        return new DocumentImportExtractionService(
            new AdvancedRag.Infrastructure.Documents.GanssDocumentHtmlSanitizer(),
            new ImportImageNormalizer());
    }
```

(b) Add new tests for the image-aware method (reuses the existing `CreateStructuredDocx()`, which embeds a real 1×1 PNG):

```csharp
    private static readonly Guid DocumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task ExtractDocxWithImagesAsync_EmitsStableImageUrlAndReturnsImageContent()
    {
        var service = CreateService();

        var result = await service.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand("policy.docx", DocxMime, CreateStructuredDocx(), ActorId),
            DocumentId,
            CancellationToken.None);

        result.Images.Should().HaveCount(1);
        ImportImageContent image = result.Images[0];
        result.ContentHtml.Should().Contain($"/api/document-images/{image.ImageId:D}/content");
        result.ContentHtml.ToLowerInvariant().Should().NotContain("data:image");
        image.ObjectKey.Should().StartWith($"documents/{DocumentId:D}/images/{image.ImageId:D}/");
        image.Sha256Hash.Should().HaveLength(64);
        image.Content.Should().NotBeEmpty();
        result.Text.Should().Contain("Safety policy");
    }

    [Fact]
    public async Task ExtractDocxWithImagesAsync_RejectsNonDocx()
    {
        var service = CreateService();

        var act = () => service.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand("policy.pdf", "application/pdf", new byte[10], ActorId),
            DocumentId,
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentImportException>()
            .Where(error => error.Code == "VALIDATION_FAILED");
    }
```

> Note: the existing `ExtractAsync_DocxReturnsTextHtmlAndSafeMetadata` test (which asserts the **text-only** `ExtractAsync` output contains no `<img>`) stays unchanged and valid — `ExtractAsync` keeps stripping images.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportExtractionTests"`
Expected: FAIL — `ExtractDocxWithImagesAsync` not implemented / constructor signature mismatch.

- [ ] **Step 3: Implement the image-aware extraction**

Edit `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/DocumentImportExtractionService.cs`:

(a) Add `using` directives:

```csharp
using AdvancedRag.App.DocumentImages;
```

(b) Replace the field declarations + add a constructor (the class currently has only `private readonly HtmlSanitizer _htmlSanitizer = new();`). Keep `_htmlSanitizer` for the legacy `ExtractDocx`, and add the two injected collaborators:

```csharp
    private readonly HtmlSanitizer _htmlSanitizer = new();
    private readonly IDocumentHtmlSanitizer _documentHtmlSanitizer;
    private readonly ImportImageNormalizer _imageNormalizer;

    public DocumentImportExtractionService(
        IDocumentHtmlSanitizer documentHtmlSanitizer,
        ImportImageNormalizer imageNormalizer)
    {
        _documentHtmlSanitizer = documentHtmlSanitizer;
        _imageNormalizer = imageNormalizer;
    }
```

(c) Add the new method (place after `ExtractAsync`):

```csharp
    public Task<DocxImportExtractionResult> ExtractDocxWithImagesAsync(
        ImportExtractionCommand command,
        Guid documentId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (command.FileBytes.LongLength > MaxImportBytes)
        {
            throw new DocumentImportException(
                "IMPORT_FILE_TOO_LARGE",
                413,
                "Uploaded import file exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = MaxImportBytes });
        }

        if (!string.Equals(command.MimeType, DocxMimeType, StringComparison.Ordinal))
        {
            throw new DocumentImportException(
                "VALIDATION_FAILED",
                400,
                "Image-aware import supports DOCX files only.",
                new Dictionary<string, object?> { ["field"] = "mimeType" });
        }

        List<ImportImageContent> images = new();
        var converter = new DocumentConverter().ImageConverter(image =>
        {
            byte[] raw;
            using (Stream imageStream = image.GetStream())
            using (MemoryStream buffer = new())
            {
                imageStream.CopyTo(buffer);
                raw = buffer.ToArray();
            }

            NormalizedImage? normalized = _imageNormalizer.Normalize(raw, image.ContentType);
            if (normalized is null)
            {
                return new Dictionary<string, string>(); // drop the <img>
            }

            if (!DocumentImageObjectKey.TryGetExtension(normalized.ContentType, out string? extension))
            {
                return new Dictionary<string, string>();
            }

            Guid imageId = Guid.NewGuid();
            string sha256 = Convert.ToHexString(SHA256.HashData(normalized.Content)).ToLowerInvariant();
            string objectKey = DocumentImageObjectKey.Build(documentId, imageId, sha256, extension!);
            string altText = (image.AltText ?? string.Empty).Trim();
            images.Add(new ImportImageContent(
                imageId,
                objectKey,
                normalized.ContentType,
                normalized.Content.LongLength,
                sha256,
                altText,
                normalized.Content));

            string url = $"/api/document-images/{imageId:D}/content";
            return new Dictionary<string, string> { ["src"] = url };
        });

        using var htmlStream = new MemoryStream(command.FileBytes);
        var htmlResult = converter.ConvertToHtml(htmlStream);

        using var textStream = new MemoryStream(command.FileBytes);
        var textResult = converter.ExtractRawText(textStream);

        string normalizedText = NormalizeExtractedText(textResult.Value);
        if (normalizedText.Length == 0)
        {
            throw new DocumentImportException(
                "IMPORT_TEXT_NOT_EXTRACTABLE",
                422,
                "Uploaded file has no extractable text.");
        }

        string sanitizedHtml = _documentHtmlSanitizer.Sanitize(htmlResult.Value);

        var metadata = new ImportExtractionMetadata(
            null,
            Path.GetFileName(command.OriginalFilename),
            command.MimeType,
            command.FileBytes.LongLength,
            Convert.ToHexString(SHA256.HashData(command.FileBytes)).ToLowerInvariant(),
            "Extracted");

        return Task.FromResult(new DocxImportExtractionResult(
            normalizedText,
            sanitizedHtml,
            metadata,
            images));
    }
```

> `image.AltText` is Mammoth's `IImage` alt text; if the installed `Mammoth` build names it differently the compiler flags it immediately — replace with the correct member. The `_documentHtmlSanitizer` (Ganss with default config) preserves `<img>` + relative `src`/`alt`, matching the manual-image flow.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportExtractionTests"`
Expected: PASS (existing text-only tests + 2 new image tests).

- [ ] **Step 5: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportTypes.cs services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/DocumentImportExtractionService.cs services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/DocumentImportExtractionTests.cs
git commit -m "feat: extract and normalize DOCX images to stable URLs"
```

---

## Task 5: Import orchestrator (`DocumentImportService`)

**Files:**
- Create: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportService.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImportServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImportServiceTests.cs`:

```csharp
using AdvancedRag.App.DocumentImages;
using AdvancedRag.App.Documents;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentImportServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string DocxMime =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task ImportDocxAsync_CreatesDraftWithFilenameTitleAndPersistsImages()
    {
        var repository = new RecordingDocumentRepository();
        var storage = new RecordingObjectStorage();
        var images = new RecordingImageRepository();
        var extraction = new StubExtractionService();
        var service = new DocumentImportService(extraction, repository, storage, images);

        DocumentAggregate result = await service.ImportDocxAsync(
            new ImportDocxCommand("Quarterly Report.docx", DocxMime, new byte[] { 1, 2, 3 }, ActorId, "req-1"),
            CancellationToken.None);

        result.Title.Should().Be("Quarterly Report");
        result.State.Should().Be(DocumentState.Draft);
        result.AccessRules.Should().BeEmpty();
        result.CurrentDraftVersion!.ContentHtml.Should().Contain("/api/document-images/");
        repository.Saved.Should().NotBeNull();
        storage.Puts.Should().HaveCount(1);
        images.Added.Should().HaveCount(1);
        images.Added[0].DocumentId.Should().Be(result.Id);
        images.Added[0].UploadedByUserId.Should().Be(ActorId);
    }

    [Fact]
    public async Task ImportDocxAsync_BlankFilename_FallsBackToDefaultTitle()
    {
        var service = new DocumentImportService(
            new StubExtractionService(), new RecordingDocumentRepository(),
            new RecordingObjectStorage(), new RecordingImageRepository());

        DocumentAggregate result = await service.ImportDocxAsync(
            new ImportDocxCommand(".docx", DocxMime, new byte[] { 1 }, ActorId, "req-2"),
            CancellationToken.None);

        result.Title.Should().Be("Imported document");
    }

    private sealed class StubExtractionService : IDocumentImportExtractionService
    {
        public Task<ImportExtractionResult> ExtractAsync(ImportExtractionCommand command, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<DocxImportExtractionResult> ExtractDocxWithImagesAsync(
            ImportExtractionCommand command, Guid documentId, CancellationToken ct)
        {
            Guid imageId = Guid.NewGuid();
            var image = new ImportImageContent(
                imageId,
                DocumentImageObjectKey.Build(documentId, imageId, "abc", ".png"),
                "image/png", 3, "abc", "diagram", new byte[] { 9, 9, 9 });
            return Task.FromResult(new DocxImportExtractionResult(
                "Body text",
                $"<p>Body text</p><img src=\"/api/document-images/{imageId:D}/content\" alt=\"diagram\">",
                new ImportExtractionMetadata(null, command.OriginalFilename, command.MimeType, 3, "hash", "Extracted"),
                new[] { image }));
        }
    }

    private sealed class RecordingDocumentRepository : IDocumentRepository
    {
        public DocumentAggregate? Saved { get; private set; }
        public Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentSummary>>(Array.Empty<DocumentSummary>());
        public Task<DocumentAggregate?> FindAsync(Guid documentId, CancellationToken ct) =>
            Task.FromResult<DocumentAggregate?>(null);
        public Task SaveAsync(
            DocumentAggregate document,
            IReadOnlyList<ReviewCommentRecord> comments,
            IReadOnlyList<DocumentAuditEvent> auditEvents,
            CancellationToken ct)
        {
            Saved = document;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObjectStorage : IDocumentImageObjectStorage
    {
        public List<string> Puts { get; } = new();
        public Task PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
        {
            Puts.Add(objectKey);
            return Task.CompletedTask;
        }
        public Task<Stream> GetAsync(string objectKey, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class RecordingImageRepository : IDocumentImageRepository
    {
        public List<DocumentImageRecord> Added { get; } = new();
        public Task AddAsync(DocumentImageRecord image, CancellationToken ct)
        {
            Added.Add(image);
            return Task.CompletedTask;
        }
        public Task<DocumentImageRecord?> FindAsync(Guid imageId, CancellationToken ct) =>
            Task.FromResult<DocumentImageRecord?>(null);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportServiceTests"`
Expected: FAIL — `DocumentImportService` not defined.

- [ ] **Step 3: Implement the orchestrator**

Create `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportService.cs`:

```csharp
using AdvancedRag.App.DocumentImages;

namespace AdvancedRag.App.Documents;

public sealed class DocumentImportService : IDocumentImportService
{
    private const string DefaultTitle = "Imported document";

    private readonly IDocumentImportExtractionService _extraction;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentImageObjectStorage _storage;
    private readonly IDocumentImageRepository _images;
    private readonly TimeProvider _timeProvider;

    public DocumentImportService(
        IDocumentImportExtractionService extraction,
        IDocumentRepository documents,
        IDocumentImageObjectStorage storage,
        IDocumentImageRepository images,
        TimeProvider? timeProvider = null)
    {
        _extraction = extraction;
        _documents = documents;
        _storage = storage;
        _images = images;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DocumentAggregate> ImportDocxAsync(ImportDocxCommand command, CancellationToken ct)
    {
        Guid documentId = Guid.NewGuid();
        DocxImportExtractionResult extracted = await _extraction.ExtractDocxWithImagesAsync(
            new ImportExtractionCommand(command.OriginalFilename, command.MimeType, command.FileBytes, command.ActorUserId),
            documentId,
            ct);

        string title = TitleFromFilename(command.OriginalFilename);
        DocumentAggregate draft = DocumentAggregate.NewDraft(
            documentId,
            Guid.NewGuid(),
            title,
            string.Empty,
            string.Empty,
            extracted.ContentHtml,
            Array.Empty<DocumentAccessRuleRecord>(),
            command.ActorUserId);

        await _documents.SaveAsync(
            draft,
            Array.Empty<ReviewCommentRecord>(),
            new[]
            {
                new DocumentAuditEvent(
                    command.ActorUserId,
                    "document.imported",
                    documentId,
                    command.RequestId,
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = documentId,
                        ["originalFilename"] = Path.GetFileName(command.OriginalFilename),
                        ["imageCount"] = extracted.Images.Count,
                    }),
            },
            ct);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach (ImportImageContent image in extracted.Images)
        {
            await using MemoryStream stream = new(image.Content);
            await _storage.PutAsync(image.ObjectKey, image.ContentType, stream, ct);
            await _images.AddAsync(
                new DocumentImageRecord(
                    image.ImageId,
                    documentId,
                    image.ObjectKey,
                    Path.GetFileName(command.OriginalFilename),
                    image.ContentType,
                    image.SizeBytes,
                    image.Sha256Hash,
                    image.AltText,
                    command.ActorUserId,
                    now),
                ct);
        }

        return draft;
    }

    private static string TitleFromFilename(string filename)
    {
        string title = Path.GetFileNameWithoutExtension(filename).Trim();
        return title.Length == 0 ? DefaultTitle : title;
    }
}
```

> Image persistence runs **after** the draft row exists (FK requires it). Phase 1 keeps this best-effort and non-deduplicated, per Scope. Hardening (per-image try/catch + orphan sweep) is a documented follow-up.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportServiceTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.App/Documents/DocumentImportService.cs services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentImportServiceTests.cs
git commit -m "feat: add DOCX import orchestrator creating draft with images"
```

---

## Task 6: Controller endpoint + DI registration

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Program.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentsController.cs`
- Test: `services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImportEndpointTests.cs`

- [ ] **Step 1: Write the failing integration test**

Create `services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImportEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AdvancedRag.Api.Tests;

public sealed class DocumentImportEndpointTests : IClassFixture<DocumentImportWebApplicationFactory>
{
    private readonly DocumentImportWebApplicationFactory _factory;

    public DocumentImportEndpointTests(DocumentImportWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImportDocx_AsEditor_CreatesDraftAndReturnsDetail()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImportFakeAuthService.EditorEmail, "manage.localhost");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([0x50, 0x4b, 0x03, 0x04]); // ZIP magic
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(file, "file", "Quarterly Report.docx");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/documents/imports/docx") { Content = content };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        DocumentDetailResponse? body = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();
        body!.Title.Should().Be("Quarterly Report");
        _factory.Imports.LastCommand!.OriginalFilename.Should().Be("Quarterly Report.docx");
        _factory.Imports.LastCommand.ActorUserId.Should().Be(DocumentImportFakeAuthService.EditorUserId);
    }

    [Fact]
    public async Task ImportDocx_AsViewer_IsForbidden()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        LoginSession session = await LoginAsync(client, DocumentImportFakeAuthService.ViewerEmail, "manage.localhost");

        using MultipartFormDataContent content = new();
        ByteArrayContent file = new([0x50, 0x4b, 0x03, 0x04]);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(file, "file", "x.docx");

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/documents/imports/docx") { Content = content };
        request.Headers.Host = "manage.localhost";
        request.Headers.Add("X-CSRF-Token", session.Csrf.Token);
        request.Headers.Add("Cookie", $"{session.Csrf.Cookie}; {session.SessionCookie}");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- auth helpers (same pattern as DocumentImageEndpointTests) ---
    private static async Task<LoginSession> LoginAsync(HttpClient client, string email, string host)
    {
        CsrfState csrf = await GetCsrfAsync(client, host);
        HttpRequestMessage login = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = DocumentImportFakeAuthService.ValidPassword }),
        };
        login.Headers.Host = host;
        login.Headers.Add("X-CSRF-Token", csrf.Token);
        login.Headers.Add("Cookie", csrf.Cookie);
        using HttpResponseMessage response = await client.SendAsync(login);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return new LoginSession(csrf, CookiePair(GetSetCookie(response, "__Host-session")));
    }

    private static async Task<CsrfState> GetCsrfAsync(HttpClient client, string host)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/csrf");
        request.Headers.Host = host;
        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokens).Should().BeTrue();
        return new CsrfState(tokens!.Single(), CookiePair(GetSetCookie(response, "__Host-CSRF")));
    }

    private static string GetSetCookie(HttpResponseMessage response, string cookieName)
    {
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values).Should().BeTrue();
        return values!.Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));
    }

    private static string CookiePair(string setCookie) => setCookie.Split(';', 2)[0];

    private sealed record CsrfState(string Token, string Cookie);
    private sealed record LoginSession(CsrfState Csrf, string SessionCookie);
    private sealed record DocumentDetailResponse(Guid Id, string Title, string State);
}

public sealed class DocumentImportWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DocumentImportFakeAuthService _auth = new();
    public FakeDocumentImportService Imports { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AppDatabase"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Csrf:SigningKey"] = "local-test-csrf-signing-key-with-enough-entropy",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.RemoveAll<IDocumentImportService>();
            services.AddSingleton<IAuthService>(_auth);
            services.AddSingleton<IDocumentImportService>(Imports);
        });
    }
}

public sealed class FakeDocumentImportService : IDocumentImportService
{
    public ImportDocxCommand? LastCommand { get; private set; }

    public Task<DocumentAggregate> ImportDocxAsync(ImportDocxCommand command, CancellationToken ct)
    {
        LastCommand = command;
        DocumentAggregate draft = DocumentAggregate.NewDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Path.GetFileNameWithoutExtension(command.OriginalFilename),
            string.Empty,
            string.Empty,
            "<p>imported</p>",
            Array.Empty<DocumentAccessRuleRecord>(),
            command.ActorUserId);
        return Task.FromResult(draft);
    }
}

public sealed class DocumentImportFakeAuthService : IAuthService
{
    public static readonly Guid EditorUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid ViewerUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string EditorEmail = "editor@example.com";
    public const string ViewerEmail = "viewer@example.com";
    public const string ValidPassword = "password";

    private readonly Dictionary<Guid, AuthenticatedUser> _users = new()
    {
        [EditorUserId] = new(EditorUserId, EditorEmail, "Editor User", ["DocumentEditor"], []),
        [ViewerUserId] = new(ViewerUserId, ViewerEmail, "Viewer User", ["Viewer"], []),
    };

    public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        AuthenticatedUser? user = _users.Values.SingleOrDefault(item =>
            item.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(password == ValidPassword ? user : null);
    }

    public Task<AuthenticatedUser?> GetActiveUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(_users.GetValueOrDefault(userId));
}
```

> If `ErrorEnvelope`/`DocumentDetailResponse` helper records collide with those declared in `DocumentImageEndpointTests.cs` (same test assembly/namespace), reuse the existing ones instead of redeclaring — keep only the records this file uniquely needs.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportEndpointTests"`
Expected: FAIL — route `imports/docx` not found (404) / `IDocumentImportService` not registered.

- [ ] **Step 3: Register services in `Program.cs`**

In `services/dotnet-api/src/AdvancedRag.Api/Program.cs`, immediately after the existing line `builder.Services.AddScoped<IDocumentImportExtractionService, DocumentImportExtractionService>();`:

```csharp
builder.Services.AddSingleton<AdvancedRag.Infrastructure.Documents.ImportImageNormalizer>();
builder.Services.AddScoped<IDocumentImportService, DocumentImportService>();
```

> `IDocumentImportExtractionService` now resolves with its two constructor dependencies: `IDocumentHtmlSanitizer` (already registered as `GanssDocumentHtmlSanitizer` singleton) and `ImportImageNormalizer` (registered above).

- [ ] **Step 4: Add the controller action**

In `services/dotnet-api/src/AdvancedRag.Api/Controllers/DocumentsController.cs`:

(a) Add `IDocumentImportService` to the constructor:

```csharp
    private readonly IDocumentLifecycleService _documents;
    private readonly IDocumentImportExtractionService _imports;
    private readonly IDocumentImportService _importService;

    public DocumentsController(
        IDocumentLifecycleService documents,
        IDocumentImportExtractionService imports,
        IDocumentImportService importService)
    {
        _documents = documents;
        _imports = imports;
        _importService = importService;
    }
```

(b) Add the action after `ExtractImportAsync`:

```csharp
    [HttpPost("imports/docx")]
    [RequestSizeLimit(DocumentImportExtractionServiceMaxSize)]
    public async Task<IActionResult> ImportDocxAsync(IFormFile file, CancellationToken ct)
    {
        const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (!string.Equals(file.ContentType, docxMime, StringComparison.Ordinal))
        {
            return Error(
                400,
                "VALIDATION_FAILED",
                "Image-aware import supports DOCX files only.",
                new Dictionary<string, object?> { ["field"] = "mimeType" });
        }

        if (file.Length > DocumentImportExtractionServiceMaxSize)
        {
            return Error(
                413,
                "IMPORT_FILE_TOO_LARGE",
                "Uploaded import file exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = DocumentImportExtractionServiceMaxSize });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);

        try
        {
            DocumentAggregate document = await _importService.ImportDocxAsync(
                new ImportDocxCommand(file.FileName, file.ContentType, memory.ToArray(), ActorUserId(), RequestId()),
                ct);
            return Created($"/api/documents/{document.Id}", DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentImportException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
```

> The class-level `[Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]` already gates this action (Viewer → 403). No rule-based policy check is applied — by design, the draft has no access rules yet.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentImportEndpointTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Full backend build + test**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln`
Expected: PASS (all suites; Docker running for Testcontainers integration suites).

- [ ] **Step 7: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.Api services/dotnet-api/tests/AdvancedRag.Api.Tests/DocumentImportEndpointTests.cs
git commit -m "feat: add POST /api/documents/imports/docx endpoint"
```

---

## Task 7: Frontend — route DOCX import to the new endpoint; filename → title

**Files:**
- Modify: `apps/manage-web/src/api/documents.ts`
- Modify: `apps/manage-web/src/features/documents/DocumentsPage.tsx`
- Modify: `apps/manage-web/src/i18n/` resource files (es-AR default + en-US)

- [ ] **Step 1: Add the API client function**

In `apps/manage-web/src/api/documents.ts`, after `importDocumentText`:

```ts
export async function importDocx(file: File): Promise<DocumentDetail> {
  await ensureCsrfToken()
  const form = new FormData()
  form.append('file', file)
  return requestJson<DocumentDetail>('/api/documents/imports/docx', {
    method: 'POST',
    headers: csrfHeaders(),
    body: form,
  })
}
```

- [ ] **Step 2: Branch `runImport` by file type and set title from filename**

In `apps/manage-web/src/features/documents/DocumentsPage.tsx`:

(a) Add `importDocx` to the existing import from `../../api/documents`.

(b) Add a constant near `MaxImportFileSizeBytes`:

```ts
const DocxMimeType =
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
```

(c) Replace the body of `runImport` with mime-based branching:

```ts
  async function runImport(file: File) {
    setImportMessage(null);
    setImportError(null);
    setImportFileName(file.name);
    if (file.size > MaxImportFileSizeBytes) {
      setImportError(t("documents.import_too_large"));
      return;
    }

    const filenameTitle = file.name.replace(/\.[^.]+$/, "").trim();

    try {
      const isDocx =
        file.type === DocxMimeType || file.name.toLowerCase().endsWith(".docx");
      if (isDocx) {
        const created = await importDocx(file);
        onSaved(
          created,
          t("documents.import_success", { filename: file.name }),
        );
        return;
      }

      const result = await importDocumentText(file);
      const extractedHtml = result.contentHtml?.trim();
      setContentHtml(
        extractedHtml && extractedHtml.length > 0
          ? extractedHtml
          : plainTextToParagraphHtml(result.text),
      );
      if (filenameTitle.length > 0) {
        setTitle(filenameTitle);
      }
      setIsDirty(true);
      setImportMessage(
        t("documents.import_success", { filename: result.metadata.originalFilename }),
      );
    } catch (error) {
      const reference = error instanceof ApiError ? error.requestId : "unknown";
      setImportError(t("documents.import_error", { reference }));
    }
  }
```

> DOCX: the server creates the draft (title already set from the filename) and `onSaved` navigates to it in edit mode, where manual image upload is enabled. PDF and other text-only imports keep the in-form prefill behavior and now also set the title field from the filename client-side.

- [ ] **Step 3: Verify no new i18n keys are required**

The flow reuses existing keys `documents.import_success`, `documents.import_error`, `documents.import_too_large`. Confirm they exist:

Run: `pnpm.cmd --dir apps\manage-web exec grep -r "import_success" src/i18n`
Expected: a match in both es-AR and en-US resource files. If any referenced key is missing, add it to both locales (es-AR is the default).

- [ ] **Step 4: Typecheck + lint + unit tests**

Run: `pnpm.cmd --dir apps\manage-web typecheck`
Run: `pnpm.cmd --dir apps\manage-web test -- --run`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/manage-web/src
git commit -m "feat(manage-web): import DOCX via draft-creating endpoint, set title from filename"
```

---

## Task 8: Context docs (decision record + standards)

**Files:**
- Modify: `context/design-decisions.md`
- Modify: `context/code-standards.md`
- Modify: `context/progress-tracker.md`

- [ ] **Step 1: Append a design decision**

Add a dated entry to `context/design-decisions.md`:

```markdown
## 2026-06-13 — DOCX import extracts images to MinIO and creates the draft

**Decision:** Importing a `.docx` now extracts embedded images, normalizes them
with SkiaSharp, stores them in MinIO via the existing document-image pipeline, and
creates the draft document immediately (filename → title) referencing images through
stable `/api/document-images/{id}/content` URLs. The import-created draft has no
access rules yet and is role-gated only (it bypasses `CanManageDraftAsync`, which
returns false for empty rules). PDF import remains text-only prefill.

**New dependency:** `SkiaSharp` 2.88.x (+ `SkiaSharp.NativeAssets.Linux.NoDependencies`),
MIT-licensed — chosen over ImageSharp to avoid the Six Labors commercial-license
threshold. Used only for raster decode/downscale/re-encode (no text/SVG rendering).

**Supersedes** the prior code-standards stance "Embedded DOCX images are not imported"
and partially the "do not persist imported file bytes" rule (we persist extracted,
normalized images — not the original upload).

**Follow-ups (not done):** orphaned-image cleanup in MinIO (on `<img>` removal, archive,
or future hard-delete); PDF image extraction; SHA-256 dedup; transactional image persistence.
```

- [ ] **Step 2: Update code-standards**

In `context/code-standards.md`:
- Change the DOCX line "Embedded DOCX images are not imported in the current slice." to: "Embedded DOCX images **are** extracted on import, normalized with SkiaSharp, and stored in MinIO as document images referenced by stable app URLs (since 2026-06-13). The original upload bytes are still not persisted."
- Add a row to the dependency table: `| Image normalization | `SkiaSharp` 2.88.x (+ `NativeAssets.Linux.NoDependencies`) | DOCX import only. Raster decode/downscale/WebP re-encode. MIT-licensed. No text/SVG rendering. |`

- [ ] **Step 3: Update progress tracker**

Add to `context/progress-tracker.md` current status: "DOCX image extraction on import (draft-creating, SkiaSharp normalization, MinIO) — implemented 2026-06-13. Open: orphaned-image cleanup, PDF images."

- [ ] **Step 4: Refresh the knowledge graph**

Run: `graphify update .`
Expected: graph updated (AST-only, no API cost).

- [ ] **Step 5: Commit**

```bash
git add context/design-decisions.md context/code-standards.md context/progress-tracker.md graphify-out
git commit -m "docs: record DOCX image extraction on import decision"
```

---

## User-Owned Verification Checkpoint (Compose)

Agent work ends at Task 8. The following requires the running stack and is **user-owned** (per the Human-in-the-Loop protocol). Report results back.

1. Start the stack: `.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate`
2. Log in to `manage.localhost` as an editor; open "New document"; import a `.docx` that contains at least one embedded photo (PNG/JPEG) and one vector/metafile (EMF) image.
3. **Expect:** the editor navigates to a new draft whose **title equals the filename** (without extension); the photo renders inline via `/api/document-images/{id}/content`; the EMF image is silently dropped (best-effort skip).
4. In MinIO console (or `mc ls`), confirm objects exist under `documents/{documentId}/images/...`.
5. Confirm a row per kept image in `app.document_images` (`select object_key, content_type, size_bytes from app.document_images where document_id = '...';`).
6. Fill access rules + send-to-review + publish; confirm the published document still renders its images in `docs.localhost`.

## Postman Checklist (new/changed endpoints)

- **POST** `https://manage.localhost/api/documents/imports/docx` — auth: `__Host-session` cookie (role Admin/DocumentEditor/DocumentPublisher) + `X-CSRF-Token` + `__Host-CSRF` cookie; body: `multipart/form-data` field `file` (a `.docx`, ≤10 MB). Expect `201 Created` with `DocumentDetailResponse` (title = filename without extension). `400 VALIDATION_FAILED` for non-DOCX content type; `413 IMPORT_FILE_TOO_LARGE` over 10 MB; `422 IMPORT_TEXT_NOT_EXTRACTABLE` for a DOCX with no text; `403` as Viewer.
- **POST** `https://manage.localhost/api/documents/imports/extract` — unchanged (PDF + text-only fallback).

---

## Self-Review Notes

- **Spec coverage:** image extraction (Tasks 2,4), MinIO storage (Task 5), draft-at-import (Task 5,6), filename→title (Tasks 5,7), SkiaSharp normalization + license (Tasks 2,8), frontend wiring (Task 7), DOCX-only scope (controller + service mime guards). Deletion is explicitly out of scope and recorded as a follow-up.
- **Type consistency:** `ExtractDocxWithImagesAsync` signature, `ImportImageContent`, `DocxImportExtractionResult`, `ImportDocxCommand`, `IDocumentImportService.ImportDocxAsync`, `NormalizedImage`, `DocumentImageObjectKey.{TryGetExtension,Build}` are used identically across Tasks 3–7.
- **No new migration:** `app.document_images` already exists; empty-access-rules draft persists via existing `SaveAsync`.
