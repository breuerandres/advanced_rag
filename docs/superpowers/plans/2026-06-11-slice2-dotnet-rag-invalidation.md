# Slice 2: .NET Semantic Cache Invalidation Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire `.NET` document lifecycle mutations to rag-api's existing `POST /internal/cache-invalidations` so stale cached answers stop being served after archive, edit-after-publish (republish), and restore-to-draft.

**Architecture:** A new `IInternalCacheInvalidationClient` (App layer) with an HTTP implementation in Infrastructure that mirrors `FastApiInternalIndexingClient` (same base address, same `X-Internal-Service-Token`). `DocumentLifecycleService` calls it best-effort **after** the state transition persists: invalidation failure logs and does not roll back the user's operation (retrieval correctness is already guaranteed by Slice 1's SQL predicate; the cache is the only stale surface and entries expire by TTL anyway).

**Tech Stack:** ASP.NET Core 8, `HttpClient` + `JsonContent` (Web defaults → camelCase, matching FastAPI's `documentIds` alias), xUnit.

---

## Background For A Zero-Context Engineer

- rag-api endpoint (already implemented, `services/rag-api/src/advanced_rag/api/routers/chat.py` ~line 162): `POST /internal/cache-invalidations`, header `X-Internal-Service-Token`, body `{"documentIds": ["<uuid>", ...]}`, response `{"invalidated": <int>}`. It deletes `rag.semantic_cache_entries` whose `rag.semantic_cache_sources` reference any of the ids.
- The reference client to copy is `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/FastApiInternalIndexingClient.cs`; its DI registration is in `services/dotnet-api/src/AdvancedRag.Api/Program.cs` (~lines 146-156) — reuse the same HttpClient configuration and token resolution.
- Mutation points live in `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleService.cs`. Audit actions mark them: `document.published` (~line 328 — invalidate only when a previous published version was replaced), `document.archived` (~line 372), `document.restored` (~line 422).
- `context/architecture.md` lists "accessibility change" (document access-rule change) as an invalidation trigger too. Access-rule updates do not live in `DocumentLifecycleService`; find them with `grep -rn "AccessRules" services/dotnet-api/src --include=*.cs` and apply the same post-save call there.

## File Map

- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleTypes.cs` (interface, next to `IInternalIndexingClient` ~line 223)
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/FastApiInternalCacheInvalidationClient.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleService.cs`
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Program.cs`
- Modify: `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentLifecycleServiceTests.cs`
- Create: `services/dotnet-api/tests/AdvancedRag.Infrastructure.Tests/FastApiInternalCacheInvalidationClientTests.cs` (place beside existing Infrastructure tests; if the indexing client's tests live elsewhere, follow that location)

---

## Task 1: Interface And No-Op Default

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleTypes.cs`

- [ ] **Step 1: Add the interface next to `IInternalIndexingClient`**

```csharp
public interface IInternalCacheInvalidationClient
{
    /// <summary>Best-effort invalidation of rag semantic-cache entries sourced from the given documents.</summary>
    Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore`
Expected: OK.

- [ ] **Step 3: Commit**

```bash
git add services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleTypes.cs
git commit -m "feat(dotnet): add internal cache invalidation client contract"
```

## Task 2: HTTP Client Implementation

**Files:**
- Create: `services/dotnet-api/src/AdvancedRag.Infrastructure/Documents/FastApiInternalCacheInvalidationClient.cs`
- Create: test file for it (mirror the location/pattern of the indexing client's tests; if none exist, create `FastApiInternalCacheInvalidationClientTests.cs` in the App/Api test project that already references Infrastructure)

- [ ] **Step 1: Write failing tests with a stub `HttpMessageHandler`**

```csharp
public sealed class FastApiInternalCacheInvalidationClientTests
{
    [Fact]
    public async Task PostsDocumentIdsWithInternalToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler((request, _) =>
        {
            captured = request;
            return Respond(HttpStatusCode.OK, "{\"invalidated\": 3}");
        });
        var client = new FastApiInternalCacheInvalidationClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://rag-api:8000") },
            "token-123");

        int invalidated = await client.InvalidateDocumentsAsync(
            new List<Guid> { Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(3, invalidated);
        Assert.Equal("/internal/cache-invalidations", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("token-123", captured.Headers.GetValues("X-Internal-Service-Token").Single());
        string body = await captured.Content!.ReadAsStringAsync();
        Assert.Contains("documentIds", body); // camelCase from JsonContent Web defaults
    }

    [Fact]
    public async Task ReturnsZeroOnHttpFailure()
    {
        var handler = new StubHandler((_, _) => Respond(HttpStatusCode.InternalServerError, "{}"));
        var client = new FastApiInternalCacheInvalidationClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://rag-api:8000") },
            "token-123");

        int invalidated = await client.InvalidateDocumentsAsync(
            new List<Guid> { Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(0, invalidated); // best-effort: failure must not throw
    }
}
```

(`StubHandler`/`Respond`: if the test project already has an equivalent stub for the indexing client, reuse it; otherwise implement a minimal `DelegatingHandler` returning the canned `HttpResponseMessage`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~FastApiInternalCacheInvalidationClientTests"`
Expected: FAIL (type does not exist).

- [ ] **Step 3: Implement the client (mirror of the indexing client)**

```csharp
using System.Net.Http.Json;
using AdvancedRag.App.Documents;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class FastApiInternalCacheInvalidationClient : IInternalCacheInvalidationClient
{
    private readonly HttpClient _http;
    private readonly string _internalServiceToken;

    public FastApiInternalCacheInvalidationClient(HttpClient http, string internalServiceToken)
    {
        _http = http;
        _internalServiceToken = internalServiceToken;
    }

    public async Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0)
        {
            return 0;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/cache-invalidations")
        {
            Content = JsonContent.Create(new CacheInvalidationHttpRequest(documentIds)),
        };
        message.Headers.Add("X-Internal-Service-Token", _internalServiceToken);

        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var body = await response.Content.ReadFromJsonAsync<CacheInvalidationHttpResponse>(cancellationToken: ct);
            return body?.Invalidated ?? 0;
        }
        catch (HttpRequestException)
        {
            return 0; // best-effort: cache entries expire by TTL; do not fail the lifecycle operation
        }
    }

    private sealed record CacheInvalidationHttpRequest(IReadOnlyList<Guid> DocumentIds);

    private sealed record CacheInvalidationHttpResponse(int Invalidated);
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~FastApiInternalCacheInvalidationClientTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add services/dotnet-api
git commit -m "feat(dotnet): FastAPI internal cache invalidation HTTP client"
```

## Task 3: Lifecycle Service Calls

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.App/Documents/DocumentLifecycleService.cs`
- Modify: `services/dotnet-api/tests/AdvancedRag.App.Tests/DocumentLifecycleServiceTests.cs`

- [ ] **Step 1: Write failing tests with a recording fake**

Add to `DocumentLifecycleServiceTests.cs` a recording fake and three tests (follow the file's existing arrangement helpers for repository/policy fakes):

```csharp
private sealed class RecordingCacheInvalidationClient : IInternalCacheInvalidationClient
{
    public List<IReadOnlyList<Guid>> Calls { get; } = [];

    public Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
    {
        Calls.Add(documentIds);
        return Task.FromResult(documentIds.Count);
    }
}

[Fact] // ArchiveAsync invalidates the archived document id.
[Fact] // RequestPublishAsync invalidates ONLY when a previous published version existed (republish).
[Fact] // RestoreAsync invalidates the restored document id.
// Plus one negative: first-time publish (no previous published version) does NOT call the client.
```

Assert `fake.Calls.Single().Single() == document.Id` (or `fake.Calls` empty for the negative case).

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentLifecycleServiceTests"`
Expected: new tests FAIL (constructor has no such dependency).

- [ ] **Step 3: Implement**

- Add an optional constructor dependency mirroring the indexing client style: `IInternalCacheInvalidationClient? cacheInvalidationClient = null`, stored as `_cacheInvalidation = cacheInvalidationClient ?? new NoOpInternalCacheInvalidationClient();` and add the no-op class next to `UnavailableInternalIndexingClient`:

```csharp
internal sealed class NoOpInternalCacheInvalidationClient : IInternalCacheInvalidationClient
{
    public Task<int> InvalidateDocumentsAsync(IReadOnlyList<Guid> documentIds, CancellationToken ct)
        => Task.FromResult(0);
}
```

- In `ArchiveAsync`: after the `SaveAsync` that records `document.archived`, add
  `await _cacheInvalidation.InvalidateDocumentsAsync([document.Id], ct);`
- In `RestoreAsync`: after the `SaveAsync` that records `document.restored`, same call.
- In the publish completion path (where `document.published` is audited): capture `bool isRepublish = document.CurrentPublishedVersion is not null;` **before** the aggregate is replaced, and after the publish `SaveAsync` run the call only `if (isRepublish)`.

- [ ] **Step 4: Run the lifecycle tests**

Run: `dotnet test services\dotnet-api\AdvancedRag.sln --filter "FullyQualifiedName~DocumentLifecycleServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add services/dotnet-api
git commit -m "feat(dotnet): invalidate rag semantic cache on archive, republish, and restore"
```

## Task 4: Access-Rule Changes And DI Wiring

**Files:**
- Modify: `services/dotnet-api/src/AdvancedRag.Api/Program.cs`
- Modify: the service that updates document access rules (locate it first)

- [ ] **Step 1: Locate the access-rule update path**

Run: `grep -rn "AccessRules" services/dotnet-api/src --include=*.cs` and identify the service method that persists access-rule changes for a document. Add the same post-save `InvalidateDocumentsAsync([documentId], ct)` call there, with a recording-fake test in that service's test class (same shape as Task 3). If access rules are persisted inside `DocumentLifecycleService` itself, this folds into Task 3.

- [ ] **Step 2: Register the client in `Program.cs`**

Next to the existing `IInternalIndexingClient` registration (~line 146), register the new client with the same HttpClient base address and internal token resolution:

```csharp
builder.Services.AddScoped<IInternalCacheInvalidationClient>(services =>
{
    // copy the HttpClient + token construction used for IInternalIndexingClient verbatim
    return new FastApiInternalCacheInvalidationClient(http, token);
});
```

and pass it where `DocumentLifecycleService` is constructed (check how the lifecycle service is registered and extend that registration).

- [ ] **Step 3: Full verification**

Run: `dotnet build services\dotnet-api\AdvancedRag.sln --no-restore && dotnet test services\dotnet-api\AdvancedRag.sln`
Expected: green.

- [ ] **Step 4: Update graph, commit**

Run: `graphify update .`

```bash
git add services/dotnet-api graphify-out
git commit -m "feat(dotnet): wire cache invalidation client into DI and access-rule updates"
```

- [ ] **Step 5 (USER-OWNED): Compose acceptance**

Ask the user to: start the stack, ask a question (gets cached), re-ask the same question (verify `cache-hit` SSE event), archive the source document, re-ask — must NOT be a cache hit and must not cite the archived document. Report back.

## Postman Checklist (endpoint behavior exercised by this slice)

- POST `https://rag.<host>/internal/cache-invalidations` — internal only (not routed through Caddy); header `X-Internal-Service-Token: <token>`; body `{"documentIds": ["<uuid>"]}`; expect `200 {"invalidated": N}`; without token expect `401 AUTH_INTERNAL_TOKEN_INVALID`.
