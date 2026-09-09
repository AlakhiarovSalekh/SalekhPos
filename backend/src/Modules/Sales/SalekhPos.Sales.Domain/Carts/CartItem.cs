namespace SalekhPos.Sales.Domain.Carts;

public sealed record CartItem
{
    public Guid ProductId { get; }
    public decimal Quantity { get; }
    public CartItem(Guid productId, decimal quantity)
    {
        if (productId == Guid.Empty || quantity <= 0 || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentException("Cart item is invalid.");
        ProductId = productId;
        Quantity = quantity;
    }
}
