namespace Metaspesa.RestApi.Shopping.CheckoutShoppingList;

/// <summary>Identifier of an accepted checkout.</summary>
/// <param name="CheckoutId">Stable checkout occurrence ID.</param>
internal sealed record CheckoutResponse(Guid CheckoutId);