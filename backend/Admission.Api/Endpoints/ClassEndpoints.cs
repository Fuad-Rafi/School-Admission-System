using Admission.Api.Contracts;
using Admission.Api.Services;

namespace Admission.Api.Endpoints;

public static class ClassEndpoints
{
    public static IEndpointRouteBuilder MapClassEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/classes", CreateClassAsync)
            .WithName("CreateClass")
            .WithTags("Classes")
            .Produces<ClassCreatedResponse>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);

        app.MapGet("/classes", GetClassesAsync)
            .WithName("ListClasses")
            .WithTags("Classes")
            .Produces<IReadOnlyList<ClassResponse>>();

        app.MapGet("/classes/{classId:guid}", GetClassAsync)
            .WithName("GetClass")
            .WithTags("Classes")
            .Produces<ClassResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/classes/{classId:guid}/holds", ApplyAsync)
            .WithName("ApplyForSeat")
            .WithTags("Holds")
            .Produces<HoldCreatedResponse>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> CreateClassAsync(
        CreateClassRequest request,
        SeatService seats,
        CancellationToken cancellationToken)
    {
        var created = await seats.CreateClassAsync(request, cancellationToken);
        return Results.Created($"/classes/{created.Id}", created);
    }

    private static async Task<IResult> GetClassesAsync(
        SeatService seats,
        CancellationToken cancellationToken)
        => Results.Ok(await seats.GetClassesAsync(cancellationToken));

    private static async Task<IResult> GetClassAsync(
        Guid classId,
        SeatService seats,
        CancellationToken cancellationToken)
        => Results.Ok(await seats.GetClassAsync(classId, cancellationToken));

    private static async Task<IResult> ApplyAsync(
        Guid classId,
        HttpRequest request,
        SeatService seats,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Require(request);
        var hold = await seats.ApplyAsync(classId, studentId, cancellationToken);

        return Results.Created($"/holds/{hold.HoldId}", hold);
    }
}
