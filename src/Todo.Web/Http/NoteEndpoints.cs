using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Todo.Application.Common;
using Todo.Domain.Notes;
using Todo.Web.Contracts;

namespace Todo.Web.Http;

/// <summary>Notes API. Notes are global (not space scoped), so there is no <c>space</c> parameter.</summary>
internal static class NoteEndpoints
{
    internal static void MapNoteApi(this RouteGroupBuilder api, DashboardDependencies dependencies)
    {
        api.MapGet("/notes", async (string? q, CancellationToken ct) =>
            ResultMapping.ToHttpResult(
                await dependencies.NoteService.ListAsync(q, ct).ConfigureAwait(false),
                notes => Results.Json(
                    new NotesResponse { Notes = [.. notes.Select(ToDto)] },
                    DashboardJsonContext.Default.NotesResponse)));

        api.MapGet("/notes/{id}", async (string id, CancellationToken ct) =>
            !TryParseId(id, out var noteId)
                ? NotFound(id)
                : ToJson(await dependencies.NoteService.GetAsync(noteId, ct).ConfigureAwait(false)));

        api.MapPost("/notes", async (HttpContext context, CancellationToken ct) =>
        {
            var body = await DashboardEndpoints.ReadJsonAsync(context, DashboardJsonContext.Default.CreateNoteRequest, ct).ConfigureAwait(false);
            if (!body.TryGetValue(out var request))
            {
                return ResultMapping.ToHttpResult(body.Error!);
            }

            return ToJson(
                await dependencies.NoteService.AddAsync(request.Title, request.Content, ct).ConfigureAwait(false),
                StatusCodes.Status201Created);
        });

        api.MapPatch("/notes/{id}", async (string id, HttpContext context, CancellationToken ct) =>
        {
            if (!TryParseId(id, out var noteId))
            {
                return NotFound(id);
            }

            var body = await DashboardEndpoints.ReadJsonAsync(context, DashboardJsonContext.Default.UpdateNoteRequest, ct).ConfigureAwait(false);
            if (!body.TryGetValue(out var request))
            {
                return ResultMapping.ToHttpResult(body.Error!);
            }

            return ToJson(await dependencies.NoteService.EditAsync(noteId, request.Title, request.Content, ct).ConfigureAwait(false));
        });

        api.MapDelete("/notes/{id}", async (string id, CancellationToken ct) =>
            !TryParseId(id, out var noteId)
                ? NotFound(id)
                : ResultMapping.ToHttpResult(await dependencies.NoteService.DeleteAsync(noteId, ct).ConfigureAwait(false), Results.NoContent));
    }

    private static NoteDto ToDto(Note note) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt.UtcDateTime,
    };

    private static IResult ToJson(Result<Note> result, int statusCode = StatusCodes.Status200OK) =>
        ResultMapping.ToHttpResult(
            result,
            note => Results.Json(ToDto(note), DashboardJsonContext.Default.NoteDto, statusCode: statusCode));

    private static bool TryParseId(string? raw, out int id) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static IResult NotFound(string id) =>
        ResultMapping.ToHttpResult(TodoError.NotFound($"Note '{id}' was not found."));
}
