using Catalog.Application.Products;
using Catalog.UnitTests.Fakes;
using DevOpsStore.BuildingBlocks.Results;

namespace Catalog.UnitTests.Products;

public class ProductServiceTests
{
    private readonly InMemoryProductRepository _repository = new();
    private readonly DictionaryProductCache _cache = new();
    private readonly ProductService _service;

    public ProductServiceTests() => _service = new ProductService(_repository, _cache);

    [Fact]
    public async Task GetAll_ReturnsSeededProducts()
    {
        var products = await _service.GetAllAsync(category: null);

        Assert.Equal(6, products.Count);
    }

    [Fact]
    public async Task GetAll_FiltersByCategory_CaseInsensitive()
    {
        var products = await _service.GetAllAsync("accessories");

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal("Accessories", p.Category));
    }

    [Fact]
    public async Task Create_ValidRequest_AssignsNewIdAndTrimsValues()
    {
        var result = await _service.CreateAsync(new ProductRequest("  Desk Lamp ", "Lighting", 34.50m, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.Id);
        Assert.Equal("Desk Lamp", result.Value.Name);
        Assert.Equal(string.Empty, result.Value.Description);
    }

    [Theory]
    [InlineData("", "Lighting", 10, "name")]
    [InlineData("Lamp", "", 10, "category")]
    [InlineData("Lamp", "Lighting", 0, "price")]
    public async Task Create_InvalidRequest_ReturnsValidationError(string name, string category, decimal price, string field)
    {
        var result = await _service.CreateAsync(new ProductRequest(name, category, price, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(field, result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Update_UnknownProduct_ReturnsNotFound()
    {
        var result = await _service.UpdateAsync(999, new ProductRequest("Lamp", "Lighting", 10, null));

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Delete_ExistingProduct_RemovesIt()
    {
        var result = await _service.DeleteAsync(1);

        Assert.True(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await _service.GetByIdAsync(1)).Error!.Type);
    }

    [Fact]
    public async Task GetAll_SecondCall_IsServedFromCache()
    {
        await _service.GetAllAsync(null);
        await _service.GetAllAsync("audio");

        Assert.Equal(1, _repository.GetAllCalls);
    }

    [Fact]
    public async Task Create_InvalidatesCachedList()
    {
        await _service.GetAllAsync(null);

        await _service.CreateAsync(new ProductRequest("Desk Lamp", "Lighting", 34.50m, null));
        var products = await _service.GetAllAsync(null);

        Assert.Equal(7, products.Count);
        Assert.Equal(2, _repository.GetAllCalls);
    }

    [Fact]
    public async Task Update_InvalidatesCachedItem()
    {
        await _service.GetByIdAsync(1);

        await _service.UpdateAsync(1, new ProductRequest("Silent Mouse", "Accessories", 19.99m, null));
        var product = await _service.GetByIdAsync(1);

        Assert.Equal("Silent Mouse", product.Value.Name);
        Assert.Equal(1, _cache.Invalidations);
    }
}
