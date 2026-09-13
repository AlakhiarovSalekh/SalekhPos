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
        viewModel.InitializeFromPreparedState();
        await viewModel.ScanAsync("10001", default); await viewModel.ScanAsync("10001", default);

        Assert.Equal(0, workspace.OpenCalls);
        Assert.Single(viewModel.CartLines); Assert.Equal(2m, viewModel.CartLines[0].Quantity);
        Assert.Equal("20.00 GEL", viewModel.GrandTotalText); Assert.True(viewModel.CanCompleteSale);

        await viewModel.CompleteAsync("20", default);
        Assert.Empty(viewModel.CartLines); Assert.Equal(2m, workspace.Checkout!.Items.Single().Quantity);
    }

    [Fact]
    public async Task UncertainCashMovementRetryKeepsOperationIdAndOriginalIntent()
    {
        var workspace = new Workspace { FailFirstMovement = true }; var viewModel = new CashierViewModel(workspace);
        viewModel.InitializeFromPreparedState();

        await Assert.ThrowsAsync<HttpRequestException>(() => viewModel.RecordMovementAsync(
            "cash_in", "5", "Float", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.RecordMovementAsync(
            "cash_in", "6", "Float", default));
        await viewModel.RecordMovementAsync("cash_in", "5", "Float", default);

        Assert.Equal(2, workspace.MovementOperations.Count);
        Assert.Equal(workspace.MovementOperations[0], workspace.MovementOperations[1]);
    }

    [Fact]
    public async Task SuccessfulShiftOpenEnablesSellingFromRefreshedWorkspaceState()
    {
        var workspace = new Workspace(hasSession: false); var viewModel = new CashierViewModel(workspace);
        viewModel.InitializeFromPreparedState();

        Assert.True(viewModel.CanOpenShift); Assert.False(viewModel.IsOnlineSession);
        await viewModel.OpenShiftAsync(" gel ", "10.123456", default);

        Assert.True(viewModel.IsOnlineSession); Assert.False(viewModel.CanOpenShift);
        Assert.Equal("GEL", workspace.OpenIntents.Single().Currency);
        Assert.Equal("Cash shift opened.", viewModel.Message);
    }

    [Fact]
    public async Task UncertainShiftOpenPreservesOperationAndRejectsChangedRetryIntent()
    {
        var workspace = new Workspace(hasSession: false) { FailFirstOpen = true };
        var viewModel = new CashierViewModel(workspace); viewModel.InitializeFromPreparedState();

        await Assert.ThrowsAsync<HttpRequestException>(() => viewModel.OpenShiftAsync("GEL", "5", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.OpenShiftAsync("USD", "6", default));
        await viewModel.OpenShiftAsync("gel", "5.0", default);

        Assert.Equal(2, workspace.OpenIntents.Count);
        Assert.Equal(workspace.OpenIntents[0].OperationId, workspace.OpenIntents[1].OperationId);
        Assert.All(workspace.OpenIntents, intent =>
        {
            Assert.Equal("GEL", intent.Currency); Assert.Equal(5m, intent.OpeningBalance);
        });
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("GE1", "0")]
    [InlineData("GEL", "-1")]
    [InlineData("GEL", "1.1234567")]
    [InlineData("GEL", "1,2")]
    public async Task InvalidShiftOpenInputSendsNothing(string currency, string balance)
    {
        var workspace = new Workspace(hasSession: false); var viewModel = new CashierViewModel(workspace);
        viewModel.InitializeFromPreparedState();

        await Assert.ThrowsAsync<ArgumentException>(() => viewModel.OpenShiftAsync(currency, balance, default));

        Assert.Empty(workspace.OpenIntents); Assert.NotEmpty(viewModel.Message);
    }

    private sealed class Workspace
        : IPosWorkspace
    {
        private readonly Guid organization = Guid.NewGuid(); private readonly Guid branch = Guid.NewGuid();
        private readonly Guid device = Guid.NewGuid(); private readonly Guid register = Guid.NewGuid();
        private readonly Guid shift = Guid.NewGuid(); private readonly Guid product = Guid.NewGuid();
        public bool FailFirstMovement { get; init; }
        public bool FailFirstOpen { get; init; }
        public int OpenCalls { get; private set; }
        public List<Guid> MovementOperations { get; } = [];
        public List<(Guid OperationId, string Currency, decimal OpeningBalance)> OpenIntents { get; } = [];
        public CashCheckoutRequest? Checkout { get; private set; }
        public PosWorkspaceState CurrentState { get; private set; }
        public Workspace(bool hasSession = true)
        {
            var scope = new PosWorkspaceScope(organization, branch, device);
            var session = hasSession ? new LocalCashSession(organization, branch, device, register, shift, "GEL",
                0m, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow) : null;
            CurrentState = new(scope, session, true, 0, false, true,
                DateTimeOffset.UtcNow);
        }
        public Task<PosWorkspaceState> OpenOnlineAsync(CancellationToken cancellationToken)
        {
            OpenCalls++;
            return Task.FromResult(CurrentState);
        }
        public Task<PosWorkspaceState> OpenOfflineAsync(CancellationToken cancellationToken)
        {
            OpenCalls++;
            return Task.FromResult(CurrentState with { IsOnline = false });
        }
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
        public Task<OpenCashSessionResult> OpenCashSessionAsync(Guid operationId, string currency,
            decimal openingBalance, CancellationToken cancellationToken)
        {
            OpenIntents.Add((operationId, currency, openingBalance));
            if (FailFirstOpen && OpenIntents.Count == 1) throw new HttpRequestException("Uncertain");
            var openedAt = DateTimeOffset.UtcNow;
            CurrentState = CurrentState with
            {
                CashSession = new LocalCashSession(organization, branch, device, register, shift, currency,
                    openingBalance, openedAt, openedAt)
            };
            return Task.FromResult(new OpenCashSessionResult(shift, branch, register, "open", currency,
                openingBalance, openedAt, "cashier"));
        }
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
