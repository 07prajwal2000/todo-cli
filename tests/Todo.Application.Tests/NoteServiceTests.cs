using Todo.Application.Common;
using Todo.Application.Notes;
using Todo.Application.Tests.Fakes;
using Todo.Domain.Notes;

namespace Todo.Application.Tests;

public sealed class NoteServiceTests
{
    private readonly FakeNotes _repository = new();
    private readonly NoteService _service;

    public NoteServiceTests() => _service = new NoteService(_repository, new FakeClock());

    [Fact]
    public async Task AddTrimsTheTitleStampsTheClockAndKeepsContentVerbatim()
    {
        var added = await _service.AddAsync("  Plan  ", "  # keep  \n", default);

        Assert.Equal("Plan", added.Value.Title);
        Assert.Equal("  # keep  \n", added.Value.Content);
        Assert.Equal(new FakeClock().UtcNow, added.Value.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task AddRejectsABlankTitleWithAFieldError(string? title)
    {
        var added = await _service.AddAsync(title, "x", default);

        Assert.Equal(ErrorCode.Validation, added.Error!.Code);
        Assert.True(added.Error.Fields!.ContainsKey("title"));
        Assert.Empty(_repository.Notes);
    }

    [Fact]
    public async Task AddRejectsOversizedContentAndTitle()
    {
        Assert.True((await _service.AddAsync("t", new string('x', NoteSchema.MaxContentLength + 1), default)).Error!.Fields!.ContainsKey("content"));
        Assert.True((await _service.AddAsync(new string('t', NoteSchema.MaxTitleLength + 1), "", default)).Error!.Fields!.ContainsKey("title"));
        Assert.True((await _service.AddAsync("t", new string('x', NoteSchema.MaxContentLength), default)).IsSuccess);
    }

    [Fact]
    public async Task EditChangesOnlyWhatWasSuppliedAndSkipsNoOpWrites()
    {
        await _service.AddAsync("Title", "Body", default);

        var contentOnly = await _service.EditAsync(1, null, "New body", default);
        Assert.Equal("Title", contentOnly.Value.Title);
        Assert.Equal("New body", contentOnly.Value.Content);

        var writes = _repository.Updates;
        await _service.EditAsync(1, "Title", "New body", default);
        Assert.Equal(writes, _repository.Updates);
    }

    [Fact]
    public async Task EditCanClearContentAndRejectsABlankTitle()
    {
        await _service.AddAsync("Title", "Body", default);

        Assert.Equal(string.Empty, (await _service.EditAsync(1, null, "", default)).Value.Content);
        Assert.Equal(ErrorCode.Validation, (await _service.EditAsync(1, "  ", null, default)).Error!.Code);
    }

    [Fact]
    public async Task ListIsNewestFirstAndSearchMatchesTitleOrContentIgnoringCase()
    {
        await _service.AddAsync("alpha", "first", default);
        await _service.AddAsync("beta", "Needle", default);
        await _service.AddAsync("NEEDLE title", "", default);

        Assert.Equal([3, 2, 1], (await _service.ListAsync(null, default)).Value.Select(note => note.Id));
        Assert.Equal([3, 2], (await _service.ListAsync(" needle ", default)).Value.Select(note => note.Id));
    }

    [Fact]
    public async Task MissingNotesAreNotFound()
    {
        Assert.Equal(ErrorCode.NotFound, (await _service.GetAsync(5, default)).Error!.Code);
        Assert.Equal(ErrorCode.NotFound, (await _service.EditAsync(5, "x", null, default)).Error!.Code);
        Assert.Equal(ErrorCode.NotFound, (await _service.DeleteAsync(5, default)).Error!.Code);
    }

    private sealed class FakeNotes : INoteRepository
    {
        public List<Note> Notes { get; } = [];

        public int Updates { get; private set; }

        public Task<Result<IReadOnlyList<Note>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<Note>>.Success([.. Notes]));

        public Task<Result<Note>> FindAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.FirstOrDefault(note => note.Id == id) is { } found
                ? Result<Note>.Success(found)
                : TodoError.NotFound("missing"));

        public Task<Result<Note>> AddAsync(Note draft, CancellationToken cancellationToken)
        {
            var note = draft with { Id = Notes.Count + 1 };
            Notes.Add(note);
            return Task.FromResult(Result<Note>.Success(note));
        }

        public Task<Result<Note>> UpdateAsync(Note note, CancellationToken cancellationToken)
        {
            Updates++;
            Notes[Notes.FindIndex(existing => existing.Id == note.Id)] = note;
            return Task.FromResult(Result<Note>.Success(note));
        }

        public Task<Result> DeleteAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Notes.RemoveAll(note => note.Id == id) > 0
                ? Result.Success()
                : Result.Failure(TodoError.NotFound("missing")));
    }
}
