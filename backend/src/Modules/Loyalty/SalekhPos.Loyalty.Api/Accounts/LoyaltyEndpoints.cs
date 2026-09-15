using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SalekhPos.Loyalty.Application.Accounts;
using SalekhPos.Loyalty.Contracts.Accounts;

namespace SalekhPos.Loyalty.Api.Accounts;

public static class LoyaltyEndpoints
{
    public static void MapLoyaltyEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/organizations/{organizationId:guid}/loyalty/accounts")
            .RequireAuthorization("business-api").RequireRateLimiting("business");
        group.MapGet("",async(Guid organizationId,HttpContext context,ILoyaltyAccountService service,CancellationToken ct)=>
        {
            if(!TryPage(context,out var size,out var after))return Invalid();
            return Results.Ok(await service.ListAsync(Identity(context),organizationId,size,after,ct));
        });
        group.MapGet("/{accountId:guid}",async(Guid organizationId,Guid accountId,HttpContext context,ILoyaltyAccountService service,CancellationToken ct)=>
        { var value=await service.ReadAsync(Identity(context),organizationId,accountId,ct);return value is null?Results.NotFound():Results.Ok(value); });
        group.MapPost("",async(Guid organizationId,OpenLoyaltyAccountRequest request,HttpContext context,ILoyaltyAccountService service,IAntiforgery antiforgery,CancellationToken ct)=>
        {
            if(!await ValidateMutation(context,antiforgery)||!Operation(context,out var op))return Invalid();
            try
            {
                var result=await service.OpenAsync(Identity(context),new(organizationId,Guid.NewGuid(),op,request.CustomerId),ct);
                var location=$"/api/v1/organizations/{organizationId:D}/loyalty/accounts/{result.Account.Id:D}";
                return result.Created?Results.Created(location,result.Account):Results.Ok(result.Account);
            }
            catch(ArgumentException){return Invalid();}
        });
        MapPoints(group,"earn",false); MapPoints(group,"redeem",true);
        group.MapGet("/{accountId:guid}/events",async(Guid organizationId,Guid accountId,HttpContext context,ILoyaltyAccountService service,CancellationToken ct)=>
        {
            if(!TryPage(context,out var size,out var after))return Invalid();
            return Results.Ok(await service.ListEventsAsync(Identity(context),organizationId,accountId,size,after,ct));
        });
    }

    private static void MapPoints(RouteGroupBuilder group,string action,bool redeem)
    {
        group.MapPost($"/{{accountId:guid}}/{action}",async(Guid organizationId,Guid accountId,LoyaltyPointsRequest request,HttpContext context,ILoyaltyAccountService service,IAntiforgery antiforgery,CancellationToken ct)=>
        {
            if(!await ValidateMutation(context,antiforgery)||!Operation(context,out var op))return Invalid();
            try
            {
                var command=new ChangeLoyaltyPointsCommand(organizationId,accountId,op,request.Points,request.Reason);
                var result=redeem?await service.RedeemAsync(Identity(context),command,ct):await service.EarnAsync(Identity(context),command,ct);
                return Results.Ok(result);
            }
            catch(ArgumentException){return Invalid();}
        });
    }

    private static LoyaltyIdentity Identity(HttpContext context)=>new(context.User.FindFirst("iss")!.Value,context.User.FindFirst("sub")!.Value);
    private static bool Operation(HttpContext context,out Guid operationId)=>Guid.TryParseExact(context.Request.Headers["Idempotency-Key"],"D",out operationId)&&operationId!=Guid.Empty;
    private static async Task<bool> ValidateMutation(HttpContext context,IAntiforgery antiforgery)
    { if(context.Request.Headers.ContainsKey("Authorization"))return true;try{await antiforgery.ValidateRequestAsync(context);return true;}catch(AntiforgeryValidationException){return false;} }
    private static IResult Invalid()=>Results.Problem(statusCode:400,title:"The loyalty request is invalid",extensions:new Dictionary<string,object?>{{"code","invalid_loyalty_request"}});
    private static bool TryPage(HttpContext context,out int size,out Guid? after)
    {
        size=50;after=null;if(context.Request.Query.Keys.Any(k=>k is not "pageSize" and not "after"))return false;
        if(context.Request.Query.TryGetValue("pageSize",out var sizes)&&(sizes.Count!=1||!int.TryParse(sizes[0],NumberStyles.None,CultureInfo.InvariantCulture,out size)||size is <1 or >100))return false;
        if(context.Request.Query.TryGetValue("after",out var cursors))
        { if(cursors.Count!=1||!Guid.TryParseExact(cursors[0],"D",out var cursor)||cursor==Guid.Empty)return false;after=cursor; }
        return true;
    }
}
