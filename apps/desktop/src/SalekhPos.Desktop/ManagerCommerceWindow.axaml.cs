using System.Globalization;
using Avalonia.Controls;
using SalekhPos.Desktop.Application.Management;

namespace SalekhPos.Desktop;

public sealed partial class ManagerCommerceWindow : Window
{
    private readonly ICommerceExtensions commerce;
    private readonly Guid organizationId;
    private readonly Guid branchId;

    public ManagerCommerceWindow() => throw new InvalidOperationException("An authenticated management runtime is required.");
    public ManagerCommerceWindow(ICommerceExtensions commerce, Guid organizationId, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(commerce);
        if(organizationId==Guid.Empty||branchId==Guid.Empty)throw new ArgumentException("Management scope is required.");
        this.commerce=commerce; this.organizationId=organizationId; this.branchId=branchId;
        InitializeComponent(); ScopeText.Text=$"Organization {organizationId:D} · Branch {branchId:D}";
        Opened += async (_,_) => await RefreshAll();
    }

    private async void RefreshAllClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await RefreshAll();
    private async Task RefreshAll(){await RefreshTransfers();await RefreshPromotions();await RefreshLoyalty();}
    private async Task RefreshTransfers()
    {
        await Execute(async()=>{var page=await commerce.ListStockTransfersAsync(organizationId,branchId,100,null,default);TransferList.ItemsSource=page.Items;TransferMessage.Text=$"{page.Items.Count} transfer(s) loaded.";});
    }
    private async void RefreshTransfersClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await RefreshTransfers();
    private async void CreateTransferClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Execute(async()=>
        {
            var destination=Guid.Parse(DestinationBranchBox.Text??""); var product=Guid.Parse(TransferProductBox.Text??"");
            var quantity=decimal.Parse(TransferQuantityBox.Text??"",NumberStyles.Number,CultureInfo.InvariantCulture);
            await commerce.CreateStockTransferAsync(organizationId,branchId,
                new(destination,string.IsNullOrWhiteSpace(TransferReferenceBox.Text)?null:TransferReferenceBox.Text!.Trim(),
                    [new(product,quantity)]),Guid.NewGuid(),default);
            await RefreshTransfers();
        });
    }
    private async Task ChangeTransfer(string action)
    {
        if(TransferList.SelectedItem is not StockTransferSummary transfer){TransferMessage.Text="Select a transfer first.";return;}
        await Execute(async()=>{await commerce.ChangeStockTransferAsync(organizationId,branchId,transfer,action,default);await RefreshTransfers();});
    }
    private async void DispatchTransferClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await ChangeTransfer("dispatch");
    private async void ReceiveTransferClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await ChangeTransfer("receive");
    private async void CancelTransferClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await ChangeTransfer("cancel");
    private async Task RefreshPromotions()
    {
        await Execute(async()=>{var page=await commerce.ListPromotionsAsync(organizationId,branchId,100,null,default);PromotionList.ItemsSource=page.Items;PromotionMessage.Text=$"{page.Items.Count} promotion(s) loaded.";});
    }
    private async void RefreshPromotionsClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await RefreshPromotions();
    private async void CreatePromotionClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Execute(async()=>
        {
            var kind=(PromotionKindBox.SelectedItem as ComboBoxItem)?.Content?.ToString()??"percentage";
            var value=decimal.Parse(PromotionValueBox.Text??"",NumberStyles.Number,CultureInfo.InvariantCulture);
            var minimum=decimal.Parse(PromotionMinimumBox.Text??"",NumberStyles.Number,CultureInfo.InvariantCulture);
            var currency=kind=="fixed"?(PromotionCurrencyBox.Text??"").Trim().ToUpperInvariant():null;
            await commerce.CreatePromotionAsync(organizationId,branchId,
                new((PromotionCodeBox.Text??"").Trim().ToUpperInvariant(),(PromotionNameBox.Text??"").Trim(),kind,
                    value,currency,minimum,DateTimeOffset.UtcNow,null),Guid.NewGuid(),default);
            await RefreshPromotions();
        });
    }
    private async void DeactivatePromotionClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        if(PromotionList.SelectedItem is not PromotionSummary promotion){PromotionMessage.Text="Select a promotion first.";return;}
        await Execute(async()=>{await commerce.DeactivatePromotionAsync(organizationId,branchId,promotion,default);await RefreshPromotions();});
    }
    private async void EvaluatePromotionClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Execute(async()=>
        {
            var subtotal=decimal.Parse(PromotionSubtotalBox.Text??"",NumberStyles.Number,CultureInfo.InvariantCulture);
            var currency=(PromotionCurrencyBox.Text??"").Trim().ToUpperInvariant();
            var result=await commerce.EvaluatePromotionsAsync(organizationId,branchId,subtotal,currency,DateTimeOffset.UtcNow,default);
            PromotionEvaluationText.Text=$"Discount {result.TotalDiscount:0.00} {result.Currency} · Payable {result.Payable:0.00} · {result.Applied.Count} campaign(s)";
        });
    }
    private async Task RefreshLoyalty()
    {
        await Execute(async()=>{var page=await commerce.ListLoyaltyAccountsAsync(organizationId,100,null,default);LoyaltyList.ItemsSource=page.Items;LoyaltyMessage.Text=$"{page.Items.Count} loyalty account(s) loaded.";});
    }
    private async void RefreshLoyaltyClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await RefreshLoyalty();
    private async void OpenLoyaltyClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
    {
        await Execute(async()=>{var customer=Guid.Parse(LoyaltyCustomerBox.Text??"");await commerce.OpenLoyaltyAccountAsync(organizationId,customer,Guid.NewGuid(),default);await RefreshLoyalty();});
    }
    private async Task ChangePoints(string action)
    {
        if(LoyaltyList.SelectedItem is not LoyaltyAccountSummary account){LoyaltyMessage.Text="Select a loyalty account first.";return;}
        await Execute(async()=>
        {
            var points=int.Parse(LoyaltyPointsBox.Text??"",CultureInfo.InvariantCulture);
            var result=await commerce.ChangeLoyaltyPointsAsync(organizationId,account,action,points,(LoyaltyReasonBox.Text??"").Trim(),Guid.NewGuid(),default);
            LoyaltyMessage.Text=$"{result.Kind}: {result.Points} · balance {result.Account.PointsBalance} · {result.Account.Tier}";
            await RefreshLoyalty();
        });
    }
    private async void EarnPointsClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await ChangePoints("earn");
    private async void RedeemPointsClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)=>await ChangePoints("redeem");
    private async Task Execute(Func<Task> action)
    {
        try{await action();}
        catch(Exception exception) when(exception is ArgumentException or InvalidOperationException or HttpRequestException or FormatException)
        {
            var message=exception is HttpRequestException ? "The management service request failed." : exception.Message;
            TransferMessage.Text=message; PromotionMessage.Text=message; LoyaltyMessage.Text=message;
        }
    }
}
