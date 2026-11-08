using Admission.Api.Contracts;
using Admission.Api.Services;

namespace Admission.Api.Endpoints;

public static class HoldEndpoints
{
    public static IEndpointRouteBuilder MapHoldEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/holds/{holdId:guid}", GetHoldAsync)
            .WithName("GetHold")
            .WithTags("Holds")
            .Produces<HoldResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/holds/{holdId:guid}/confirm", ConfirmAsync)
            .WithName("ConfirmHold")
            .WithTags("Holds")
            .Produces<ConfirmHoldResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapDelete("/holds/{holdId:guid}", CancelAsync)
            .WithName("CancelHold")
            .WithTags("Holds")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapGet("/me/holds", GetMyHoldsAsync)
            .WithName("GetMyHolds")
            .WithTags("Holds")
            .Produces<IReadOnlyList<MyHoldResponse>>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetHoldAsync(
        Guid holdId,
        SeatService seats,
        CancellationToken cancellationToken)
        => Results.Ok(await seats.GetHoldAsync(holdId, cancellationToken));

    private static async Task<IResult> ConfirmAsync(
        Guid holdId,
        HttpRequest request,
        SeatService seats,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Require(request);
        return Results.Ok(await seats.ConfirmAsync(holdId, studentId, cancellationToken));
    }

    private static async Task<IResult> CancelAsync(
        Guid holdId,
        HttpRequest request,
        SeatService seats,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Require(request);
        await seats.CancelAsync(holdId, studentId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMyHoldsAsync(
        HttpRequest request,
        SeatService seats,
        CancellationToken cancellationToken)
    {
        var studentId = StudentId.Require(request);
        return Results.Ok(await seats.GetMyHoldsAsync(studentId, cancellationToken));
    }
}
