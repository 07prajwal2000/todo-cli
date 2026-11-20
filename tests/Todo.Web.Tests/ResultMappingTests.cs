using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Todo.Application.Common;
using Todo.Web.Contracts;
using Todo.Web.Http;

namespace Todo.Web.Tests;

public sealed class ResultMappingTests
{
    [Theory]
    [InlineData((int)ErrorCode.Validation, 400)]
    [InlineData((int)ErrorCode.NotFound, 404)]
    [InlineData((int)ErrorCode.Conflict, 409)]
    [InlineData((int)ErrorCode.Configuration, 503)]
    [InlineData((int)ErrorCode.Permission, 403)]
    [InlineData((int)ErrorCode.Unavailable, 502)]
    [InlineData((int)ErrorCode.Unexpected, 500)]
    [InlineData((int)ErrorCode.None, 200)]
    public void StatusCodeForMapsEveryErrorCode(int rawCode, int expected)
    {
        var code = (ErrorCode)rawCode;

        Assert.Equal(expected, ResultMapping.StatusCodeFor(code));
    }

    [Theory]
    [InlineData((int)ErrorCode.Validation, "validation")]
    [InlineData((int)ErrorCode.NotFound, "notfound")]
    [InlineData((int)ErrorCode.Conflict, "conflict")]
    [InlineData((int)ErrorCode.Configuration, "configuration")]
    [InlineData((int)ErrorCode.Permission, "permission")]
    [InlineData((int)ErrorCode.Unavailable, "unavailable")]
    [InlineData((int)ErrorCode.Unexpected, "unexpected")]
    public void ErrorResponseCodeIsTheLowercaseName(int rawCode, string expected)
    {
        var code = (ErrorCode)rawCode;
        var error = ErrorFor(code);

        var response = ResultMapping.ToErrorResponse(error);

        Assert.Equal(expected, response.Code);
        Assert.Equal(error.Message, response.Message);
    }

    [Fact]
    public void ValidationErrorCarriesFieldsInTheBody()
    {
        var error = TodoError.Validation(
            "The task is not valid.",
            new Dictionary<string, string[]> { ["name"] = ["Name is required."] });

        var response = ResultMapping.ToErrorResponse(error);
        var json = JsonSerializer.Serialize(response, DashboardJsonContext.Default.ErrorResponse);
        using var document = JsonDocument.Parse(json);

        Assert.NotNull(response.Fields);
        Assert.Equal(["Name is required."], response.Fields["name"]);

        var root = document.RootElement;
        Assert.Equal("validation", root.GetProperty("code").GetString());
        Assert.Equal("The task is not valid.", root.GetProperty("message").GetString());
        Assert.Equal(
            "Name is required.",
            root.GetProperty("fields").GetProperty("name")[0].GetString());
    }

    [Fact]
    public void ErrorWithoutFieldsOmitsThemFromTheJsonBody()
    {
        var response = ResultMapping.ToErrorResponse(TodoError.NotFound("Task 9 was not found."));

        var json = JsonSerializer.Serialize(response, DashboardJsonContext.Default.ErrorResponse);
        using var document = JsonDocument.Parse(json);

        Assert.Null(response.Fields);
        Assert.False(document.RootElement.TryGetProperty("fields", out _));
        Assert.Equal("notfound", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ToHttpResultExecutesTheFailureBodyWithTheMappedStatusCode()
    {
        var result = Result<int>.Failure(TodoError.Conflict("Space already exists."));

        var http = ResultMapping.ToHttpResult(result, _ => Results.Ok());

        var (status, body) = await ExecuteAsync(http);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("conflict", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ToHttpResultExecutesTheSuccessPathWhenThereIsAValue()
    {
        var result = Result<int>.Success(7);

        var http = ResultMapping.ToHttpResult(result, value => Results.Ok(value));

        var (status, body) = await ExecuteAsync(http);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("7", body);
    }

    [Fact]
    public async Task NonGenericToHttpResultReturnsNoContentOnSuccess()
    {
        var http = ResultMapping.ToHttpResult(Result.Success(), Results.NoContent);

        var (status, _) = await ExecuteAsync(http);

        Assert.Equal(StatusCodes.Status204NoContent, status);
    }

    private static TodoError ErrorFor(ErrorCode code) => code switch
    {
        ErrorCode.Validation => TodoError.Validation("bad input"),
        ErrorCode.NotFound => TodoError.NotFound("missing"),
        ErrorCode.Conflict => TodoError.Conflict("clash"),
        ErrorCode.Configuration => TodoError.Configuration("not configured"),
        ErrorCode.Permission => TodoError.Permission("nope"),
        ErrorCode.Unavailable => TodoError.Unavailable("upstream down"),
        ErrorCode.Unexpected => TodoError.Unexpected("boom"),
        _ => TodoError.Unexpected("unexpected"),
    };

    internal static async Task<(int Status, string Body)> ExecuteAsync(IResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var context = new DefaultHttpContext();
        using var stream = new MemoryStream();
        context.Response.Body = stream;

        // Results.* resolve logging and JSON options from RequestServices.
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.RequestServices = services;

        await result.ExecuteAsync(context);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }
}
