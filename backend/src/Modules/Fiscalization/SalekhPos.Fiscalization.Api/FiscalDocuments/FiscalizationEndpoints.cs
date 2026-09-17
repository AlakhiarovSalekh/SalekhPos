using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SalekhPos.Fiscalization.Application.FiscalDocuments;
using SalekhPos.Fiscalization.Contracts.FiscalDocuments;

namespace SalekhPos.Fiscalization.Api.FiscalDocuments;

public static class FiscalizationEndpoints
{
    public static void MapFiscalizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/organizations/{organizationId:guid}/fiscal-documents")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapPost("/branches/{branchId:guid}", Submit);
        group.MapPost("/{documentId:guid}/retry", Retry);
        group.MapGet("/{documentId:guid}", Read);
    }

    private static async Task<IResult> Submit(Guid organizationId, Guid branchId,
        SubmitFiscalDocumentRequest request, HttpContext context, IFiscalDocumentService service, CancellationToken ct)
    {
        if (request.DocumentId == Guid.Empty || request.SaleId == Guid.Empty) return Invalid();
        var result = await service.SubmitAsync(Identity(context), organizationId, branchId, request, ct);
        return result.Created ? Results.Created(
            $"/api/v1/organizations/{organizationId:D}/fiscal-documents/{result.Document.DocumentId:D}", result)
            : Results.Ok(result);
    }

    private static async Task<IResult> Retry(Guid organizationId, Guid documentId, HttpContext context,
        IFiscalDocumentService service, CancellationToken ct) =>
        Results.Ok(await service.RetryAsync(Identity(context), organizationId, documentId, ct));

    private static async Task<IResult> Read(Guid organizationId, Guid documentId, HttpContext context,
        IFiscalDocumentService service, CancellationToken ct)
    {
        var value = await service.ReadAsync(Identity(context), organizationId, documentId, ct);
        return value is null ? Results.NotFound() : Results.Ok(value);
    }

    private static FiscalIdentity Identity(HttpContext context) =>
        new(context.User.FindFirst("iss")!.Value, context.User.FindFirst("sub")!.Value);
    private static IResult Invalid() => Results.Problem(statusCode: 400,
        title: "The fiscal document request is invalid",
        extensions: new Dictionary<string, object?> { ["code"] = "invalid_fiscal_document" });
}
