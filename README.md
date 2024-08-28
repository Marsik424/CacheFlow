# CacheFlow
Automates cache creation and invalidation with reference handling using `Metalama`.


## How does this work?
This project is not intended for public use for now, and because it uses `Metelama Factory` you are **required** to have your own `Metalama API Key`.
`CacheFlow` provides with two interceptors: one for cache creation and another for cache invalidation.

Firstly, you have to reference NuGet package into your project or library, and it will automatically locate all Repositories.
You should also include the next line inside your `Program.cs`.
```cs
builder.Services.AddCacheService();
```
> Repository classes should be located inside the Repository folder and have [Type]Repository name. The [Type] might be used as a hash name.

By default, `CacheFlow` is not going to use reference handling, and if you want to enable it go to the `Reference Handler` section.

Let's say you have a simple `OrderRepository` with CRUD operations.

[Order](https://github.com/mqsrr/CoffeeSpace/blob/main/CoffeeSpace.Domain/Ordering/Orders/Order.cs) model has the next properties
```cs
public sealed class Order
{
    public required Guid Id { get; init; }
    
    public required OrderStatus Status { get; set; }

    public required Guid BuyerId { get; init; }

    public required Address Address { get; init; }
    
    public required IEnumerable<OrderItem> OrderItems { get; init; }
}
```

```cs
    public async Task<IEnumerable<Order>> GetAllByBuyerIdAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        bool isNotEmpty = await _orderingDbContext.Orders.AnyAsync(cancellationToken);
        if (!isNotEmpty)
        {
            return Enumerable.Empty<Order>();
        }

        var orders = await _orderingDbContext.Orders
            .Where(order => order.BuyerId == buyerId)
            .Include(order => order.OrderItems)
            .Include(order => order.Address)
            .ToListAsync(cancellationToken);
        
        return orders;
    }
  ```
```cs
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderingDbContext.Orders
            .Include(order => order.OrderItems)
            .Include(order => order.Address)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
        
        return order;
    }
  ```
Cache interceptor is looking for the methods where the method name contains Get string, so the following methods are going to be intercepted for cache creation.
The return type is going to be used as a `Hash Key`. If the return type is `IEnumerable<T>`, the `T` parameter is the `Hash Key`.
The first parameter is going to be used as a key for a cache. And since the reference handling is disabled, the generated code should be familiar to the most devs.
```cs
public async Task<IEnumerable<Order>> GetAllByBuyerIdAsync(
      Guid buyerId,
      CancellationToken cancellationToken)
    {
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
      interpolatedStringHandler.AppendFormatted<Guid>(buyerId);
      string key = interpolatedStringHandler.ToStringAndClear(); // cache key
      Type resultType = typeof (IEnumerable<Order>);
      string hashKey = resultType.GetGenericArguments()[0].Name; // hash name
      object result = await this._cacheService.HashGetAsync(hashKey, key, resultType);
      if (result != null) // if there is a cached value, return without creating cache
      {
        // ISSUE: reference to a compiler-generated field
        if (OrderRepository.<>o__2.<>p__0 == null)
        {
          // ISSUE: reference to a compiler-generated field
          OrderRepository.<>o__2.<>p__0 = CallSite<Func<CallSite, object, IEnumerable<Order>>>.Create(Binder.Convert(CSharpBinderFlags.ConvertExplicit, typeof (IEnumerable<Order>), typeof (OrderRepository)));
        }
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        return OrderRepository.<>o__2.<>p__0.Target((CallSite) OrderRepository.<>o__2.<>p__0, result);
      }
      IEnumerable<Order> methodResponse = await this.GetAllByBuyerIdAsync_Source(buyerId, cancellationToken);
      if (methodResponse == null)
        return methodResponse;
      int num = await this._cacheService.HashSetAsync(hashKey, key, JsonConvert.SerializeObject((object) methodResponse)) ? 1 : 0;
      return methodResponse;
    }

    private async Task<IEnumerable<Order>> GetAllByBuyerIdAsync_Source(
      Guid buyerId,
      CancellationToken cancellationToken)
    {
      bool isNotEmpty = await this._orderingDbContext.Orders.AnyAsync<Order>(cancellationToken);
      if (!isNotEmpty)
        return Enumerable.Empty<Order>();
      List<Order> orders = await this._orderingDbContext.Orders.Where<Order>((Expression<Func<Order, bool>>) (order => order.BuyerId == buyerId)).Include<Order, IEnumerable<OrderItem>>((Expression<Func<Order, IEnumerable<OrderItem>>>) (order => order.OrderItems)).Include<Order, Address>((Expression<Func<Order, Address>>) (order => order.Address)).ToListAsync<Order>(cancellationToken);
      return (IEnumerable<Order>) orders;
    }
    
  ```
```cs
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
      interpolatedStringHandler.AppendFormatted<Guid>(id);
      string key = interpolatedStringHandler.ToStringAndClear();
      Type resultType = typeof (Order);
      string hashKey = resultType.Name;
      object result = await this._cacheService.HashGetAsync(hashKey, key, resultType);
      if (result != null)
      {
        // ISSUE: reference to a compiler-generated field
        if (OrderRepository.<>o__4.<>p__0 == null)
        {
          // ISSUE: reference to a compiler-generated field
          OrderRepository.<>o__4.<>p__0 = CallSite<Func<CallSite, object, Order>>.Create(Binder.Convert(CSharpBinderFlags.ConvertExplicit, typeof (Order), typeof (OrderRepository)));
        }
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        return OrderRepository.<>o__4.<>p__0.Target((CallSite) OrderRepository.<>o__4.<>p__0, result);
      }
      Order methodResponse = await this.GetByIdAsync_Source(id, cancellationToken);
      if (methodResponse == null)
        return methodResponse;
      int num = await this._cacheService.HashSetAsync(hashKey, key, JsonConvert.SerializeObject((object) methodResponse)) ? 1 : 0;
      return methodResponse;
    }

    private async Task<Order?> GetByIdAsync_Source(Guid id, CancellationToken cancellationToken)
    {
      Order order1 = await this._orderingDbContext.Orders.Include<Order, IEnumerable<OrderItem>>((Expression<Func<Order, IEnumerable<OrderItem>>>) (order => order.OrderItems)).Include<Order, Address>((Expression<Func<Order, Address>>) (order => order.Address)).FirstOrDefaultAsync<Order>((Expression<Func<Order, bool>>) (order => order.Id == id), cancellationToken);
      Order byIdAsyncSource = order1;
      order1 = (Order) null;
      return byIdAsyncSource;
    }
```
---

If the method name contains Create, Update or Delete, then the cache invalidation interceptor is going to decorate these methods.
```cs
    public async Task<bool> CreateAsync(Order order, CancellationToken cancellationToken)
    {
        await _orderingDbContext.Orders.AddAsync(order, cancellationToken);
        int result = await _orderingDbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }
```
```cs
    public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        int result = await _orderingDbContext.Orders
            .Where(order => order.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return result > 0;
    }
```
Usually, these methods do not return the full model, and because of that hash key is being retrieved based on the repository name. So if you have `OrderReporisory`, the hash name will be `Order`
The cache invalidation can have two possible implementations. When the first parameter is the class object (string excluded), interceptor is going to find every `Id` property.
```cs
public sealed class Order
{
    public required Guid Id { get; init; }
    
    public required OrderStatus Status { get; set; }

    public required Guid BuyerId { get; init; }

    public required Address Address { get; init; }
    
    public required IEnumerable<OrderItem> OrderItems { get; init; }
}
```
If the property has the reference type, it's going to try to retrieve id of this object. In this case, interceptor will register `Id`, `BuyerId`, and `Address.Id`.
```cs
public async Task<bool> CreateAsync(Order order, CancellationToken cancellationToken)
    {
      bool methodResponse = await this.CreateAsync_Source(order, cancellationToken);
      Guid guid = order.Id;
      string parameterId = guid.ToString();
      guid = order.BuyerId;
      string id = guid.ToString();
      int num1 = await this._cacheService.HashRemoveAsync("Buyer", id).ConfigureAwait(false) ? 1 : 0; // remove buyer cache from hash with "Buyer" key
      ConfiguredTaskAwaitable<bool> configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Buyer", "all").ConfigureAwait(false);
      int num2 = await configuredTaskAwaitable ? 1 : 0;
      id = (string) null;
      guid = order.Address.Id;
      string id_1 = guid.ToString();
      configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Address", id_1).ConfigureAwait(false);
      int num3 = await configuredTaskAwaitable ? 1 : 0;
      configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Address", "all").ConfigureAwait(false);
      int num4 = await configuredTaskAwaitable ? 1 : 0;
      id_1 = (string) null;
      configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Order", parameterId).ConfigureAwait(false);
      int num5 = await configuredTaskAwaitable ? 1 : 0;
      configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Order", "all").ConfigureAwait(false);
      int num6 = await configuredTaskAwaitable ? 1 : 0;
      parameterId = (string) null;
      return methodResponse;
    }

    private async Task<bool> CreateAsync_Source(Order order, CancellationToken cancellationToken)
    {
      EntityEntry<Order> entityEntry = await this._orderingDbContext.Orders.AddAsync(order, cancellationToken);
      int result = await this._orderingDbContext.SaveChangesAsync(cancellationToken);
      return result > 0;
    }
```
The cache key `all` is being used when the return type of method is `IEnumerable<T>`.
`public async Task<IEnumerable<Order>> GetAllByBuyerIdAsync(
      Guid buyerId,
      CancellationToken cancellationToken)` calling this method will create cache inside `Order` hash with `all` key, so this call `this._cacheService.HashRemoveAsync("Order", "all").ConfigureAwait(false);`
is going to invalidate `GetAllByBuyerIdAsync`.

However, sometimes there is not enough information about the object.
 ```cs
public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
      bool methodResponse = await this.DeleteByIdAsync_Source(id, cancellationToken);
      ConfiguredTaskAwaitable<bool> configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Order", id.ToString()).ConfigureAwait(false);
      int num1 = await configuredTaskAwaitable ? 1 : 0;
      configuredTaskAwaitable = this._cacheService.HashRemoveAsync("Order", "all").ConfigureAwait(false);
      int num2 = await configuredTaskAwaitable ? 1 : 0;
      return methodResponse;
    }

    private async Task<bool> DeleteByIdAsync_Source(Guid id, CancellationToken cancellationToken)
    {
      int result = await this._orderingDbContext.Orders.Where<Order>((Expression<Func<Order, bool>>) (order => order.Id == id)).ExecuteDeleteAsync<Order>(cancellationToken);
      return result > 0;
    }
```
When you have only an id and type name from the repository name, there is not a lot of what you can do.

But is there any way to invalidate all related caches with a single id property?
## Reference Invalidation
You should have noticed that `CacheFlow` uses `Redis` and in addition to that it also uses `Hashes`. This datatype has the scanning command, which helps to find the cache key without entering the full key. And `CacheFlow` can use scanning to invalidate cache.
To enable reference handling, create a cs file with any name and replace all content with the next code.
```cs
using CacheFlow.Options;

[assembly: CacheOptions(UseReferenceCacheInvalidation = true)]
```
And that's it! Now rebuild your project.

```cs
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderingDbContext.Orders
            .Include(order => order.OrderItems)
            .Include(order => order.Address)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);
        
        return order;
    }
```
The generated code for cache creation now is going to include scanning feature, and it's also going to change how to cache key is being created.
> The `GetAll` is going to stay the same.

```cs
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
      interpolatedStringHandler.AppendFormatted<Guid>(id);
      string key = interpolatedStringHandler.ToStringAndClear();
      Type resultType = typeof (Order);
      string hashKey = resultType.Name;
      object result = await this._cacheService.HashScan(hashKey, "*" + key + "*", resultType); // find object using pattern
      if (result != null)
      {
        // ISSUE: reference to a compiler-generated field
        if (OrderRepository.<>o__4.<>p__0 == null)
        {
          // ISSUE: reference to a compiler-generated field
          OrderRepository.<>o__4.<>p__0 = CallSite<Func<CallSite, object, Order>>.Create(Binder.Convert(CSharpBinderFlags.ConvertExplicit, typeof (Order), typeof (OrderRepository)));
        }
        // ISSUE: reference to a compiler-generated field
        // ISSUE: reference to a compiler-generated field
        return OrderRepository.<>o__4.<>p__0.Target((CallSite) OrderRepository.<>o__4.<>p__0, result);
      }
      Order methodResponse = await this.GetByIdAsync_Source(id, cancellationToken);
      if (methodResponse == null)
        return methodResponse;
      string key_1 = key;
      Order methodResponse_1 = methodResponse;
      Address propertyValue = methodResponse_1.Address;
      string propertyId = propertyValue.Id.ToString();
      string key_2 = propertyId;
      Address methodResponse_2 = propertyValue;
      string complexKey = methodResponse_1.Id.ToString() + "-" + key_2;
      await this._cacheService.HashRemoveAllAsync("Address", "*" + propertyId + "*").ConfigureAwait(false); // Remove all occurence of id inside the "Address" hash
      int num1 = await this._cacheService.HashSetAsync("Address", complexKey, JsonConvert.SerializeObject((object) propertyValue)) ? 1 : 0; // Add the new value
      propertyValue = (Address) null;
      propertyId = (string) null;
      key_2 = (string) null;
      methodResponse_2 = (Address) null;
      complexKey = (string) null;
      IEnumerable<OrderItem> arrayProperty = methodResponse_1.OrderItems;
      int num2 = await this._cacheService.HashSetAsync("OrderItem", "all", JsonConvert.SerializeObject((object) arrayProperty)) ? 1 : 0;
      arrayProperty = (IEnumerable<OrderItem>) null;
      ICacheService cacheService = this._cacheService;
      string hashKey1 = hashKey;
      interpolatedStringHandler = new DefaultInterpolatedStringHandler(7, 4);
      interpolatedStringHandler.AppendFormatted(key_1);
      interpolatedStringHandler.AppendLiteral("-");
      interpolatedStringHandler.AppendFormatted<Guid>(methodResponse_1.BuyerId);
      interpolatedStringHandler.AppendLiteral("-");
      interpolatedStringHandler.AppendFormatted<Guid>(methodResponse_1.Address.Id);
      interpolatedStringHandler.AppendLiteral("-");
      interpolatedStringHandler.AppendFormatted(typeof (IEnumerable<OrderItem>).GetGenericArguments()[0].Name);
      interpolatedStringHandler.AppendLiteral("-all"); // key for arrays
      string stringAndClear = interpolatedStringHandler.ToStringAndClear();
      string entity = JsonConvert.SerializeObject((object) methodResponse);
      int num3 = await cacheService.HashSetAsync(hashKey1, stringAndClear, entity) ? 1 : 0;
      return methodResponse;
    }

    private async Task<Order?> GetByIdAsync_Source(Guid id, CancellationToken cancellationToken)
    {
      Order order1 = await this._orderingDbContext.Orders.Include<Order, IEnumerable<OrderItem>>((Expression<Func<Order, IEnumerable<OrderItem>>>) (order => order.OrderItems)).Include<Order, Address>((Expression<Func<Order, Address>>) (order => order.Address)).FirstOrDefaultAsync<Order>((Expression<Func<Order, bool>>) (order => order.Id == id), cancellationToken);
      Order byIdAsyncSource = order1;
      order1 = (Order) null;
      return byIdAsyncSource;
    }
```
Using scanning feature, `CacheFlow` handles most of the relationships. Mostly, it's possible because of how the cache keys are being constructed.

Each key contains the next information
- ID of the main object
- ID of the reference type
- FK (BuyerId, AddressId ...)
- <Type>-all for IEnumerable<Type>
  So, in the example above the generated key will look like this: `<order-id>-<buyer-id>-<address-id>-OrderItem-all`. When an interceptor searches for the key, it only needs one part of the key, in this case it's order's id.

Although, if you want to invalidate the address when the order is deleted, this line is a must-have.
```cs
      await this._cacheService.HashRemoveAllAsync("Address", "*" + propertyId + "*").ConfigureAwait(false); // Remove all occurence of id inside the "Address" hash
      int num1 = await this._cacheService.HashSetAsync("Address", complexKey, JsonConvert.SerializeObject((object) propertyValue)) ? 1 : 0; // add <order-id>-<address-id> inside "Address" hash.
```


And now, if you look at the previous methods.
```cs
public async Task<bool> CreateAsync(Order order, CancellationToken cancellationToken)
    {
      bool methodResponse = await this.CreateAsync_Source(order, cancellationToken);
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 1);
      interpolatedStringHandler.AppendLiteral("*");
      interpolatedStringHandler.AppendFormatted<Guid>(order.Id);
      interpolatedStringHandler.AppendLiteral("*");
      string propertyKeyPattern = interpolatedStringHandler.ToStringAndClear();
      string propertyHashKey = "Address";
      ConfiguredTaskAwaitable configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, "*Order*").ConfigureAwait(false);
      await configuredTaskAwaitable1;
      ConfiguredTaskAwaitable<bool> configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync(propertyHashKey, "all").ConfigureAwait(false);
      int num1 = await configuredTaskAwaitable2 ? 1 : 0;
      string propertyHashKey_1 = typeof (IEnumerable<OrderItem>).GetGenericArguments()[0].Name;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey_1, propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey_1, "*Order*").ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync(propertyHashKey_1, "all").ConfigureAwait(false);
      int num2 = await configuredTaskAwaitable2 ? 1 : 0;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync("Order", propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync("Order", "all").ConfigureAwait(false);
      int num3 = await configuredTaskAwaitable2 ? 1 : 0;
      propertyKeyPattern = (string) null;
      propertyHashKey = (string) null;
      propertyHashKey_1 = (string) null;
      return methodResponse;
    }
```
The cache invalidation interceptor uses property types as the hash names and invalidates all occurence of the given id.

```cs
public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
      bool methodResponse = await this.DeleteByIdAsync_Source(id, cancellationToken);
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 1);
      interpolatedStringHandler.AppendLiteral("*");
      interpolatedStringHandler.AppendFormatted<Guid>(id);
      interpolatedStringHandler.AppendLiteral("*");
      string propertyKeyPattern = interpolatedStringHandler.ToStringAndClear();
      string propertyHashKey = "Address";
      ConfiguredTaskAwaitable configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, "*Order*").ConfigureAwait(false);
      await configuredTaskAwaitable1;
      ConfiguredTaskAwaitable<bool> configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync(propertyHashKey, "all").ConfigureAwait(false);
      int num1 = await configuredTaskAwaitable2 ? 1 : 0;
      string propertyHashKey_1 = typeof (IEnumerable<OrderItem>).GetGenericArguments()[0].Name;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey_1, propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey_1, "*Order*").ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync(propertyHashKey_1, "all").ConfigureAwait(false);
      int num2 = await configuredTaskAwaitable2 ? 1 : 0;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync("Order", propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync("Order", "all").ConfigureAwait(false);
      int num3 = await configuredTaskAwaitable2 ? 1 : 0;
      propertyKeyPattern = (string) null;
      propertyHashKey = (string) null;
      propertyHashKey_1 = (string) null;
      return methodResponse;
    }

    private async Task<bool> DeleteByIdAsync_Source(Guid id, CancellationToken cancellationToken)
    {
      int result = await this._orderingDbContext.Orders.Where<Order>((Expression<Func<Order, bool>>) (order => order.Id == id)).ExecuteDeleteAsync<Order>(cancellationToken);
      return result > 0;
    }
```
The delete method is similar, the interceptor searches for the model inside your project and retrieves property names.
And that's it!. Now, if you delete order, then address, order items (array), and the buyer is going to be invalidated as well. The same goes with a buyer, who contains the list of orders.
```cs
public async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken)
    {
      bool methodResponse = await this.DeleteByIdAsync_Source(id, cancellationToken);
      DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 1);
      interpolatedStringHandler.AppendLiteral("*");
      interpolatedStringHandler.AppendFormatted<Guid>(id);
      interpolatedStringHandler.AppendLiteral("*");
      string propertyKeyPattern = interpolatedStringHandler.ToStringAndClear();
      string propertyHashKey = typeof (IEnumerable<Order>).GetGenericArguments()[0].Name;
      ConfiguredTaskAwaitable configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync(propertyHashKey, "*Buyer*").ConfigureAwait(false);
      await configuredTaskAwaitable1;
      ConfiguredTaskAwaitable<bool> configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync(propertyHashKey, "all").ConfigureAwait(false);
      int num1 = await configuredTaskAwaitable2 ? 1 : 0;
      configuredTaskAwaitable1 = this._cacheService.HashRemoveAllAsync("Buyer", propertyKeyPattern).ConfigureAwait(false);
      await configuredTaskAwaitable1;
      configuredTaskAwaitable2 = this._cacheService.HashRemoveAsync("Buyer", "all").ConfigureAwait(false);
      int num2 = await configuredTaskAwaitable2 ? 1 : 0;
      propertyKeyPattern = (string) null;
      propertyHashKey = (string) null;
      return methodResponse;
    }
```
When you call DeleteBuyer, the program is going to remove any occurence of the provided id inside `Order` hash. So, when you delete the buyer, it also deletes related order.
