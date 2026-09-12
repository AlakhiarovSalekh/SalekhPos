using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.LocalCatalog;
using SalekhPos.Desktop.Domain.LocalSales;
using SalekhPos.Desktop.Domain.Shifts;
using SalekhPos.Desktop.ViewModels;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class CashierViewModelTests
{
    [Fact]
    public async Task ScanBuildsCartAndCompletionUsesStableSaleIntent()
    {
        var workspace = new Workspace(); var viewModel = new CashierViewModel(workspace);
        await viewModel.InitializeAsync(default);
        await viewModel.ScanAsync("10001", default); await viewModel.ScanAsync("10001", default);

        Assert.Single(viewModel.CartLines); Assert.Equal(2m, viewModel.CartLines[0].Quantity);
        Assert.Equal("20.00 GEL", viewModel.GrandTotalText); Assert.True(viewModel.CanCompleteSale);

        await viewModel.CompleteAsync("20", default);
        Assert.Empty(viewModel.CartLines); Assert.Equal(2m, workspace.Checkout!.Items.Single().Quantity);
    }

    [Fact]
    public async Task UncertainCashMovementRetryKeepsOperationIdAndOriginalIntent()
    {
        var workspace = new Workspace { FailFirstMovement = true }; var viewModel = new CashierViewModel(workspace);
        await viewModel.InitializeAsync(default);

        await Assert.ThrowsAsync<HttpRequestException>(() => viewModel.RecordMovementAsync(
            "cash_in", "5", "Float", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RecordMovementAsync(
            "cash_in", "6", "Float", default));
        await viewModel.RecordMovementAsync("cash_in", "5", "Float", default);

        Assert.Equal(2, workspace.MovementOperations.Count);
        Assert.Equal(workspace.MovementOperations[0], workspace.MovementOperations[1]);
    }

    private sealed class Workspace : IPosWorkspace
    {
        private readonly Guid organization = Guid.NewGuid(); private readonly Guid branch = Guid.NewGuid();
        private readonly Guid device = Guid.NewGuid(); private readonly Guid register = Guid.NewGuid();
        private readonly Guid shift = Guid.NewGuid(); private readonly Guid product = Guid.NewGuid();
        public bool FailFirstMovement { get; init; }
        public List<Guid> MovementOperations { get; } = [];
        public CashCheckoutRequest? Checkout { get; private set; }
        public PosWorkspaceState CurrentState { get; private set; }
        public Workspace()
        {
            var scope = new PosWorkspaceScope(organization, branch, device);
            CurrentState = new(scope, new LocalCashSession(organization, branch, device, register, shift, "GEL",
                0m, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow), 0, false, true, DateTimeOffset.UtcNow);
        }
        public Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken) => Task.FromResult(CurrentState);
        public Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken) => Task.FromResult(CurrentState with { IsOnline = false });
        public Task<LocalSellableItem?> FindByProductAsync(Guid productId, DateTimeOffset at, CancellationToken cancellationToken) => Task.FromResult<LocalSellableItem?>(Item());
        public Task<LocalSellableItem?> FindByBarcodeAsync(string barcode, DateTimeOffset at, CancellationToken cancellationToken) => Task.FromResult<LocalSellableItem?>(Item());
        public Task<LocalSaleWriteResult> CompleteCashSaleAsync(CashCheckoutRequest request, CancellationToken cancellationToken)
        {
            Checkout = request; var line = new LocalSaleLine(product, Guid.NewGuid(), request.Items[0].Quantity, 10m, "GEL", "inclusive", 18m);
            var sale = new LocalSale(organization, branch, device, request.SaleId, shift, register, request.CompletedAt,
                request.CashReceived, [line]); var message = new LocalOutboxMessage(Guid.NewGuid(), sale.SaleId, device,
                1, "sale.completed.v1", "{}", new string('A', 64), "pending", null, DateTimeOffset.UtcNow);
            CurrentState = CurrentState with { PendingSales = 1 }; return Task.FromResult(new LocalSaleWriteResult(sale, message, true));
        }
        public Task<PosWorkspaceState> SynchronizeAsync(CancellationToken cancellationToken) => Task.FromResult(CurrentState);
        public Task<CashMovementResult> RecordCashMovementAsync(Guid operationId, string kind, decimal amount,
            string reason, CancellationToken cancellationToken)
        {
            MovementOperations.Add(operationId);
            if (FailFirstMovement && MovementOperations.Count == 1) throw new HttpRequestException("Uncertain");
            return Task.FromResult(new CashMovementResult(Guid.NewGuid(), shift, kind, "GEL", amount, reason,
                DateTimeOffset.UtcNow, "cashier"));
        }
        public Task<ClosedCashSessionResult> CloseCashSessionAsync(Guid operationId, decimal countedCash,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        private LocalSellableItem Item() => new(organization, branch, product, Guid.NewGuid(), "SKU-1", "Tea",
            "EA", "10001", 10m, 10m, "GEL", "inclusive", 18m, DateTimeOffset.UtcNow.AddDays(-1), null);
    }
}
