using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop.Infrastructure.Management;

public sealed class HttpCommerceExtensions(HttpClient client) : ICommerceExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UuidPage<StockTransferSummary>> ListStockTransfersAsync(Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId); ValidatePage(pageSize);
        var path=$"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/stock-transfers?pageSize={pageSize}";
        if(after.HasValue) path += $"&after={after.Value:D}";
        var result=await Get<UuidPage<StockTransferSummary>>(path,cancellationToken);
        if(result.Items.Count>pageSize||result.Items.Any(x=>x.SourceBranchId!=branchId&&x.DestinationBranchId!=branchId))
            throw new InvalidOperationException("Stock transfer response is outside the requested scope.");
        return result;
    }

    public async Task<StockTransferSummary> CreateStockTransferAsync(Guid organizationId, Guid branchId,
        CreateStockTransferInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId); ValidateOperation(operationId);
        if(input.DestinationBranchId==Guid.Empty||input.DestinationBranchId==branchId||input.Lines.Count is <1 or >500)
            throw new ArgumentException("Stock transfer input is invalid.");
        var result=await Post<StockTransferSummary>($"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/stock-transfers",
            new { input.DestinationBranchId,input.Reference,Lines=input.Lines },operationId,cancellationToken);
        ValidateTransfer(result,branchId); return result;
    }
    public async Task<StockTransferSummary> ChangeStockTransferAsync(Guid organizationId, Guid branchId,
        StockTransferSummary transfer, string action, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId); ValidateTransfer(transfer,branchId);
        if(action is not ("dispatch" or "receive" or "cancel")) throw new ArgumentException("Transfer action is invalid.");
        var result=await Post<StockTransferSummary>($"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/stock-transfers/{transfer.Id:D}/{action}",
            new { ExpectedVersion=transfer.Version },null,cancellationToken);
        ValidateTransfer(result,branchId); if(result.Id!=transfer.Id)throw new InvalidOperationException("Transfer identity changed.");
        return result;
    }

    public async Task<UuidPage<PromotionSummary>> ListPromotionsAsync(Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId); ValidatePage(pageSize);
        var path=$"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions?pageSize={pageSize}";
        if(after.HasValue)path += $"&after={after.Value:D}";
        var result=await Get<UuidPage<PromotionSummary>>(path,cancellationToken);
        if(result.Items.Count>pageSize||result.Items.Any(x=>x.BranchId.HasValue&&x.BranchId.Value!=branchId))
            throw new InvalidOperationException("Promotion response is outside the requested scope.");
        foreach(var item in result.Items)ValidatePromotion(item,branchId); return result;
    }

    public async Task<PromotionSummary> CreatePromotionAsync(Guid organizationId, Guid branchId,
        CreatePromotionInput input, Guid operationId, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId); ValidateOperation(operationId); ValidatePromotionInput(input);
        var body=new { input.Code,input.Name,BranchId=branchId,input.DiscountKind,input.Value,input.Currency,
            input.MinimumSubtotal,input.StartsAt,input.EndsAt };
        var result=await Post<PromotionSummary>($"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions",body,operationId,cancellationToken);
        ValidatePromotion(result,branchId); return result;
    }
    public async Task<PromotionSummary> DeactivatePromotionAsync(Guid organizationId, Guid branchId,
        PromotionSummary promotion, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId); ValidatePromotion(promotion,branchId);
        var result=await Post<PromotionSummary>($"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions/{promotion.Id:D}/deactivate",
            new { ExpectedVersion=promotion.Version },null,cancellationToken);
        ValidatePromotion(result,branchId); if(result.Id!=promotion.Id)throw new InvalidOperationException("Promotion identity changed.");
        return result;
    }

    public async Task<PromotionEvaluationSummary> EvaluatePromotionsAsync(Guid organizationId, Guid branchId,
        decimal subtotal, string currency, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId,branchId);
        if(subtotal<0||InvalidAmount(subtotal)||InvalidCurrency(currency)||at.Offset!=TimeSpan.Zero)
            throw new ArgumentException("Promotion evaluation input is invalid.");
        var result=await Post<PromotionEvaluationSummary>($"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/promotions/evaluate",
            new { Subtotal=subtotal,Currency=currency,At=at },null,cancellationToken);
        if(result.Subtotal!=subtotal||result.Currency!=currency||result.TotalDiscount<0||result.Payable<0
            || result.Payable+result.TotalDiscount!=result.Subtotal)
            throw new InvalidOperationException("Promotion evaluation response is invalid.");
        return result;
    }

    public async Task<UuidPage<LoyaltyAccountSummary>> ListLoyaltyAccountsAsync(Guid organizationId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        if(organizationId==Guid.Empty)throw new ArgumentException("Organization is required."); ValidatePage(pageSize);
        var path=$"api/v1/organizations/{organizationId:D}/loyalty/accounts?pageSize={pageSize}";
        if(after.HasValue)path += $"&after={after.Value:D}";
        var result=await Get<UuidPage<LoyaltyAccountSummary>>(path,cancellationToken);
        if(result.Items.Count>pageSize)throw new InvalidOperationException("Loyalty page is too large.");
        foreach(var account in result.Items)ValidateLoyalty(account); return result;
    }
    public async Task<LoyaltyAccountSummary> OpenLoyaltyAccountAsync(Guid organizationId, Guid customerId,
        Guid operationId, CancellationToken cancellationToken)
    {
        if(organizationId==Guid.Empty||customerId==Guid.Empty)throw new ArgumentException("Loyalty identifiers are invalid.");
        ValidateOperation(operationId);
        var result=await Post<LoyaltyAccountSummary>($"api/v1/organizations/{organizationId:D}/loyalty/accounts",
            new { CustomerId=customerId },operationId,cancellationToken);
        ValidateLoyalty(result); if(result.CustomerId!=customerId)throw new InvalidOperationException("Loyalty customer changed.");
        return result;
    }

    public async Task<LoyaltyPointsSummary> ChangeLoyaltyPointsAsync(Guid organizationId,
        LoyaltyAccountSummary account, string action, int points, string reason, Guid operationId,
        CancellationToken cancellationToken)
    {
        if(organizationId==Guid.Empty)throw new ArgumentException("Organization is required."); ValidateLoyalty(account);
        if(action is not ("earn" or "redeem")||points is <1 or >1_000_000||string.IsNullOrWhiteSpace(reason)
            ||reason.Length>200||reason!=reason.Trim()||reason.Any(char.IsControl))throw new ArgumentException("Loyalty mutation is invalid.");
        ValidateOperation(operationId);
        var result=await Post<LoyaltyPointsSummary>($"api/v1/organizations/{organizationId:D}/loyalty/accounts/{account.Id:D}/{action}",
            new { Points=points,Reason=reason },operationId,cancellationToken);
        ValidateLoyalty(result.Account); if(result.Account.Id!=account.Id||result.Kind!=action||result.Points!=points)
            throw new InvalidOperationException("Loyalty response is invalid.");
        return result;
    }

    private async Task<T> Get<T>(string path,CancellationToken cancellationToken)
    {
        using var response=await client.GetAsync(path,cancellationToken); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions,cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }
    private async Task<T> Post<T>(string path,object body,Guid? operationId,CancellationToken cancellationToken)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body,options:JsonOptions)};
        if(operationId.HasValue)request.Headers.TryAddWithoutValidation("Idempotency-Key",operationId.Value.ToString("D"));
        using var response=await client.SendAsync(request,cancellationToken); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions,cancellationToken)
            ?? throw new InvalidOperationException("The server response is empty.");
    }

    private static void ValidateTransfer(StockTransferSummary value,Guid branchId)
    {
        if(value.Id==Guid.Empty||(value.SourceBranchId!=branchId&&value.DestinationBranchId!=branchId)
            ||value.SourceBranchId==value.DestinationBranchId||value.Status is not ("draft" or "in_transit" or "received" or "cancelled")
            ||value.Version<1||value.Lines.Count is <1 or >500||value.Lines.Any(x=>x.ProductId==Guid.Empty||x.Quantity<=0||InvalidAmount(x.Quantity)))
            throw new InvalidOperationException("Stock transfer response is invalid.");
    }

    private static void ValidatePromotion(PromotionSummary value,Guid branchId)
    {
        if(value.Id==Guid.Empty||value.BranchId.HasValue&&value.BranchId.Value!=branchId
            ||string.IsNullOrWhiteSpace(value.Code)||string.IsNullOrWhiteSpace(value.Name)
            ||value.DiscountKind is not ("percentage" or "fixed")||value.Value<=0||InvalidAmount(value.Value)
            ||value.MinimumSubtotal<0||InvalidAmount(value.MinimumSubtotal)||value.Version<1
            ||value.DiscountKind=="fixed"&&InvalidCurrency(value.Currency)
            ||value.DiscountKind=="percentage"&&value.Value>100)
            throw new InvalidOperationException("Promotion response is invalid.");
    }
    private static void ValidatePromotionInput(CreatePromotionInput value)
    {
        if(string.IsNullOrWhiteSpace(value.Code)||value.Code.Length>40||string.IsNullOrWhiteSpace(value.Name)||value.Name.Length>160
            ||value.DiscountKind is not ("percentage" or "fixed")||value.Value<=0||InvalidAmount(value.Value)
            ||value.MinimumSubtotal<0||InvalidAmount(value.MinimumSubtotal)||value.StartsAt.Offset!=TimeSpan.Zero
            ||value.EndsAt.HasValue&&(value.EndsAt.Value.Offset!=TimeSpan.Zero||value.EndsAt<=value.StartsAt)
            ||value.DiscountKind=="fixed"&&InvalidCurrency(value.Currency)||value.DiscountKind=="percentage"&&value.Value>100)
            throw new ArgumentException("Promotion input is invalid.");
    }

    private static void ValidateLoyalty(LoyaltyAccountSummary value)
    {
        if(value.Id==Guid.Empty||value.CustomerId==Guid.Empty||value.Tier is not ("bronze" or "silver" or "gold" or "platinum")
            ||value.PointsBalance<0||value.LifetimePoints<value.PointsBalance||value.Version<1
            ||value.CreatedAt.Offset!=TimeSpan.Zero||value.UpdatedAt.Offset!=TimeSpan.Zero||value.UpdatedAt<value.CreatedAt)
            throw new InvalidOperationException("Loyalty account response is invalid.");
    }

    private static void ValidateScope(Guid organizationId,Guid branchId)
    { if(organizationId==Guid.Empty||branchId==Guid.Empty)throw new ArgumentException("Organization and branch are required."); }
    private static void ValidatePage(int pageSize)
    { if(pageSize is <1 or >100)throw new ArgumentOutOfRangeException(nameof(pageSize)); }
    private static void ValidateOperation(Guid operationId)
    { if(operationId==Guid.Empty)throw new ArgumentException("Operation ID is required."); }
    private static bool InvalidCurrency(string? value)=>value is null||value.Length!=3||value.Any(c=>c<'A'||c>'Z');
    private static bool InvalidAmount(decimal value)=>decimal.Round(value,6)!=value;
}
