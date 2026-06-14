using AdvancedRag.App.Documents;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentTypeServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CreateAsync_TrimsNameAndDefaultsSortOrder()
    {
        var repository = new InMemoryDocumentTypeRepository();
        var service = new DocumentTypeService(repository);

        DocumentTypeRecord created = await service.CreateAsync(
            new CreateDocumentTypeCommand("  Articulo  ", null, ActorId),
            CancellationToken.None);

        created.Name.Should().Be("Articulo");
        created.IsActive.Should().BeTrue();
        created.SortOrder.Should().Be(0);
        repository.Items.Should().ContainSingle(item => item.Name == "Articulo");
    }

    [Fact]
    public async Task CreateAsync_RejectsEmptyName()
    {
        var service = new DocumentTypeService(new InMemoryDocumentTypeRepository());

        var act = () => service.CreateAsync(
            new CreateDocumentTypeCommand("   ", null, ActorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentTypeException>().Where(error => error.Code == "VALIDATION_FAILED");
    }

    [Fact]
    public async Task CreateAsync_RejectsDuplicateNameCaseInsensitive()
    {
        var repository = new InMemoryDocumentTypeRepository();
        await repository.CreateAsync("Articulo", 0, ActorId, CancellationToken.None);
        var service = new DocumentTypeService(repository);

        var act = () => service.CreateAsync(
            new CreateDocumentTypeCommand("articulo", null, ActorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentTypeException>().Where(error => error.Code == "CONFLICT");
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ThrowsNotFound()
    {
        var service = new DocumentTypeService(new InMemoryDocumentTypeRepository());

        var act = () => service.UpdateAsync(
            new UpdateDocumentTypeCommand(Guid.NewGuid(), "Nuevo", null, null, ActorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentTypeException>().Where(error => error.Code == "NOT_FOUND");
    }

    [Fact]
    public async Task UpdateAsync_RejectsDuplicateNameExcludingSelf()
    {
        var repository = new InMemoryDocumentTypeRepository();
        DocumentTypeRecord articulo = await repository.CreateAsync("Articulo", 0, ActorId, CancellationToken.None);
        await repository.CreateAsync("Instructivo", 1, ActorId, CancellationToken.None);
        var service = new DocumentTypeService(repository);

        var act = () => service.UpdateAsync(
            new UpdateDocumentTypeCommand(articulo.Id, "Instructivo", null, null, ActorId),
            CancellationToken.None);

        await act.Should().ThrowAsync<DocumentTypeException>().Where(error => error.Code == "CONFLICT");
    }

    [Fact]
    public async Task UpdateAsync_AllowsRenamingOwnEntryAndTogglingState()
    {
        var repository = new InMemoryDocumentTypeRepository();
        DocumentTypeRecord articulo = await repository.CreateAsync("Articulo", 0, ActorId, CancellationToken.None);
        var service = new DocumentTypeService(repository);

        DocumentTypeRecord updated = await service.UpdateAsync(
            new UpdateDocumentTypeCommand(articulo.Id, "Artículo", false, 5, ActorId),
            CancellationToken.None);

        updated.Name.Should().Be("Artículo");
        updated.IsActive.Should().BeFalse();
        updated.SortOrder.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_WhenInUse_ThrowsDocumentTypeInUse()
    {
        var repository = new InMemoryDocumentTypeRepository();
        DocumentTypeRecord type = await repository.CreateAsync("Articulo", 0, ActorId, CancellationToken.None);
        repository.InUseIds.Add(type.Id);
        var service = new DocumentTypeService(repository);

        var act = () => service.DeleteAsync(type.Id, CancellationToken.None);

        await act.Should()
            .ThrowAsync<DocumentTypeException>()
            .Where(error => error.Code == "DOCUMENT_TYPE_IN_USE" && error.HttpStatus == 409);
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ThrowsNotFound()
    {
        var service = new DocumentTypeService(new InMemoryDocumentTypeRepository());

        var act = () => service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<DocumentTypeException>().Where(error => error.Code == "NOT_FOUND");
    }

    [Fact]
    public async Task DeleteAsync_WhenUnused_RemovesType()
    {
        var repository = new InMemoryDocumentTypeRepository();
        DocumentTypeRecord type = await repository.CreateAsync("Articulo", 0, ActorId, CancellationToken.None);
        var service = new DocumentTypeService(repository);

        await service.DeleteAsync(type.Id, CancellationToken.None);

        repository.Items.Should().BeEmpty();
    }

    private sealed class InMemoryDocumentTypeRepository : IDocumentTypeRepository
    {
        public List<DocumentTypeRecord> Items { get; } = [];

        public HashSet<Guid> InUseIds { get; } = [];

        public Task<IReadOnlyList<DocumentTypeRecord>> ListAsync(bool includeInactive, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<DocumentTypeRecord> result = Items
                .Where(item => includeInactive || item.IsActive)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<DocumentTypeRecord?> FindAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        }

        public Task<bool> NameExistsAsync(string name, Guid? excludingId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            bool exists = Items.Any(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)
                && (excludingId is null || item.Id != excludingId));
            return Task.FromResult(exists);
        }

        public Task<DocumentTypeRecord> CreateAsync(string name, int sortOrder, Guid actorUserId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var record = new DocumentTypeRecord(Guid.NewGuid(), name, true, sortOrder);
            Items.Add(record);
            return Task.FromResult(record);
        }

        public Task<DocumentTypeRecord?> UpdateAsync(
            Guid id,
            string? name,
            bool? isActive,
            int? sortOrder,
            Guid actorUserId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            int index = Items.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return Task.FromResult<DocumentTypeRecord?>(null);
            }

            DocumentTypeRecord current = Items[index];
            DocumentTypeRecord updated = current with
            {
                Name = name ?? current.Name,
                IsActive = isActive ?? current.IsActive,
                SortOrder = sortOrder ?? current.SortOrder,
            };
            Items[index] = updated;
            return Task.FromResult<DocumentTypeRecord?>(updated);
        }

        public Task<DocumentTypeDeletionOutcome> DeleteAsync(Guid id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            DocumentTypeRecord? existing = Items.SingleOrDefault(item => item.Id == id);
            if (existing is null)
            {
                return Task.FromResult(DocumentTypeDeletionOutcome.NotFound);
            }

            if (InUseIds.Contains(id))
            {
                return Task.FromResult(DocumentTypeDeletionOutcome.InUse);
            }

            Items.Remove(existing);
            return Task.FromResult(DocumentTypeDeletionOutcome.Deleted);
        }
    }
}
