using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using SalekhPos.Loyalty.Application.Accounts;
using SalekhPos.Loyalty.Contracts.Accounts;
using SalekhPos.Promotions.Application.Campaigns;
using SalekhPos.Promotions.Contracts.Campaigns;
using SalekhPos.Warehousing.Application.Transfers;
using SalekhPos.Warehousing.Contracts.Transfers;

namespace SalekhPos.Api.Authentication;

public static class WebCommerceExtensionsEndpoints
{
    public static void MapWebCommerceExtensionsEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/bff/api/v1").AllowAnonymous().RequireRateLimiting("business");
        MapTransfers(group); MapPromotions(group); MapLoyalty(group);
    }

    private static void MapTransfers(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/stock-transfers",
            async(Guid organizationId,Guid branchId,HttpContext context,WebAuthenticationState state,IStockTransferService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId)||!WebBusinessEndpoints.TryListQuery(context,out var size,out var after))return Invalid("invalid_stock_transfer_query");
            return Results.Ok(await service.ListAsync(new(raw.Value.Issuer,raw.Value.Subject),organizationId,branchId,size,after,ct));
        });
        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/stock-transfers",
            async(Guid organizationId,Guid branchId,CreateStockTransferRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,IStockTransferService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId)||!await ValidMutation(context,state,antiforgery)||!TryOperationId(context,out var op))return Invalid("invalid_stock_transfer_request");
            try
            {
                var result=await service.CreateAsync(new(raw.Value.Issuer,raw.Value.Subject),new(organizationId,Guid.NewGuid(),op,branchId,request.DestinationBranchId,request.Reference,request.Lines),ct);
                var location=$"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/stock-transfers/{result.Transfer.Id:D}";
                return result.Created?Results.Created(location,result.Transfer):Results.Ok(result.Transfer);
            }
            catch(ArgumentException){return Invalid("invalid_stock_transfer_request");}
        });
        MapTransferAction(group,"dispatch");MapTransferAction(group,"receive");MapTransferAction(group,"cancel");
    }

    private static void MapTransferAction(RouteGroupBuilder group,string action)
    {
        group.MapPost($"/organizations/{{organizationId:guid}}/branches/{{branchId:guid}}/stock-transfers/{{transferId:guid}}/{action}",
            async(Guid organizationId,Guid branchId,Guid transferId,ChangeStockTransferStatusRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,IStockTransferService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId,transferId)||!await ValidMutation(context,state,antiforgery))return Invalid("invalid_stock_transfer_request");
            try
            {
                var identity=new WarehousingIdentity(raw.Value.Issuer,raw.Value.Subject);
                var result=action switch
                {
                    "dispatch"=>await service.DispatchAsync(identity,organizationId,branchId,transferId,request.ExpectedVersion,ct),
                    "receive"=>await service.ReceiveAsync(identity,organizationId,branchId,transferId,request.ExpectedVersion,ct),
                    _=>await service.CancelAsync(identity,organizationId,branchId,transferId,request.ExpectedVersion,ct)
                };
                return Results.Ok(result);
            }
            catch(ArgumentException){return Invalid("invalid_stock_transfer_request");}
        });
    }

    private static void MapPromotions(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/promotions",
            async(Guid organizationId,Guid branchId,HttpContext context,WebAuthenticationState state,IPromotionService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId)||!WebBusinessEndpoints.TryListQuery(context,out var size,out var after))return Invalid("invalid_promotion_query");
            return Results.Ok(await service.ListAsync(new(raw.Value.Issuer,raw.Value.Subject),organizationId,branchId,size,after,ct));
        });
        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/promotions",
            async(Guid organizationId,Guid branchId,CreatePromotionRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,IPromotionService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId)||!await ValidMutation(context,state,antiforgery)||!TryOperationId(context,out var op))return Invalid("invalid_promotion_request");
            try
            {
                if(request.BranchId.HasValue&&request.BranchId.Value!=branchId)return Invalid("invalid_promotion_request");
                var result=await service.CreateAsync(new(raw.Value.Issuer,raw.Value.Subject),new(organizationId,Guid.NewGuid(),op,request.Code,request.Name,request.BranchId??branchId,request.DiscountKind,request.Value,request.Currency,request.MinimumSubtotal,request.StartsAt,request.EndsAt),ct);
                var location=$"/bff/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions/{result.Promotion.Id:D}";
                return result.Created?Results.Created(location,result.Promotion):Results.Ok(result.Promotion);
            }
            catch(ArgumentException){return Invalid("invalid_promotion_request");}
        });
        group.MapPost("/organizations/{organizationId:guid}/branches/{branchId:guid}/promotions/{promotionId:guid}/deactivate",
            async(Guid organizationId,Guid branchId,Guid promotionId,ChangePromotionStatusRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,IPromotionService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,branchId,promotionId)||!await ValidMutation(context,state,antiforgery))return Invalid("invalid_promotion_request");
            return Results.Ok(await service.DeactivateAsync(new(raw.Value.Issuer,raw.Value.Subject),organizationId,branchId,promotionId,request.ExpectedVersion,ct));
        });
        group.MapGet("/organizations/{organizationId:guid}/branches/{branchId:guid}/promotions/evaluate",
            async(Guid organizationId,Guid branchId,HttpContext context,WebAuthenticationState state,IPromotionService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!decimal.TryParse(context.Request.Query["subtotal"],System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var subtotal)
                || context.Request.Query["currency"].Count!=1 || context.Request.Query["at"].Count!=1
                || !DateTimeOffset.TryParse(context.Request.Query["at"],out var at))return Invalid("invalid_promotion_query");
            return Results.Ok(await service.EvaluateAsync(new(raw.Value.Issuer,raw.Value.Subject),organizationId,branchId,subtotal,context.Request.Query["currency"]!,at,ct));
        });
    }

    private static void MapLoyalty(RouteGroupBuilder group)
    {
        group.MapGet("/organizations/{organizationId:guid}/loyalty/accounts",async(Guid organizationId,HttpContext context,WebAuthenticationState state,ILoyaltyAccountService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId)||!WebBusinessEndpoints.TryListQuery(context,out var size,out var after))return Invalid("invalid_loyalty_query");
            return Results.Ok(await service.ListAsync(new(raw.Value.Issuer,raw.Value.Subject),organizationId,size,after,ct));
        });
        group.MapPost("/organizations/{organizationId:guid}/loyalty/accounts",async(Guid organizationId,OpenLoyaltyAccountRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,ILoyaltyAccountService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId)||!await ValidMutation(context,state,antiforgery)||!TryOperationId(context,out var op))return Invalid("invalid_loyalty_request");
            var result=await service.OpenAsync(new(raw.Value.Issuer,raw.Value.Subject),new(organizationId,Guid.NewGuid(),op,request.CustomerId),ct);
            return result.Created?Results.Created($"/bff/api/v1/organizations/{organizationId:D}/loyalty/accounts/{result.Account.Id:D}",result.Account):Results.Ok(result.Account);
        });
        MapLoyaltyPoints(group,"earn",false);MapLoyaltyPoints(group,"redeem",true);
    }

    private static void MapLoyaltyPoints(RouteGroupBuilder group,string action,bool redeem)
    {
        group.MapPost($"/organizations/{{organizationId:guid}}/loyalty/accounts/{{accountId:guid}}/{action}",async(Guid organizationId,Guid accountId,LoyaltyPointsRequest request,HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery,ILoyaltyAccountService service,CancellationToken ct)=>
        {
            var raw=await RawIdentity(context,state);if(raw is null)return Unauthenticated(state);
            if(!ValidIds(organizationId,accountId)||!await ValidMutation(context,state,antiforgery)||!TryOperationId(context,out var op))return Invalid("invalid_loyalty_request");
            var command=new ChangeLoyaltyPointsCommand(organizationId,accountId,op,request.Points,request.Reason);
            var result=redeem?await service.RedeemAsync(new(raw.Value.Issuer,raw.Value.Subject),command,ct):await service.EarnAsync(new(raw.Value.Issuer,raw.Value.Subject),command,ct);
            return Results.Ok(result);
        });
    }

    private static async Task<(string Issuer,string Subject)?> RawIdentity(HttpContext context,WebAuthenticationState state)
    {
        if(state.Settings is null)return null;var result=await context.AuthenticateAsync(WebAuthentication.CookieScheme);
        if(!result.Succeeded||result.Principal is null)return null;var issuers=result.Principal.FindAll("iss").ToArray();var subjects=result.Principal.FindAll("sub").ToArray();
        if(issuers.Length!=1||subjects.Length!=1)return null;var issuer=issuers[0].Value;var subject=subjects[0].Value;
        return string.IsNullOrWhiteSpace(issuer)||issuer.Length>2048||issuer.Any(char.IsControl)||string.IsNullOrWhiteSpace(subject)||subject.Length>256||subject.Any(char.IsControl)?null:(issuer,subject);
    }
    private static Task<bool> ValidMutation(HttpContext context,WebAuthenticationState state,IAntiforgery antiforgery)=>state.Settings is null?Task.FromResult(false):WebAuthentication.ValidateMutation(context,antiforgery,state.Settings);
    private static bool TryOperationId(HttpContext context,out Guid operationId)=>Guid.TryParseExact(context.Request.Headers["Idempotency-Key"],"D",out operationId)&&operationId!=Guid.Empty;
    private static bool ValidIds(params Guid[] values)=>values.All(value=>value!=Guid.Empty);
    private static IResult Unauthenticated(WebAuthenticationState state)=>state.Settings is null
        ?Results.Problem(statusCode:503,title:"Web authentication is unavailable",extensions:new Dictionary<string,object?>{{"code","web_authentication_unavailable"}})
        :Results.Unauthorized();
    private static IResult Invalid(string code)=>Results.Problem(statusCode:400,title:"The commerce extension request is invalid",extensions:new Dictionary<string,object?>{{"code",code}});
}
