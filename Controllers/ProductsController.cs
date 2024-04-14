using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SampleCode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {

        private readonly ILogger<ProductsController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly IDistributedCache _cache;

        public ProductsController(IProductRepository productRepository, IDistributedCache cache)
        {
            _productRepository = productRepository;
            _cache = cache;
        }


        public ProductsController(ILogger<ProductsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("api/products")]
        public async Task<IHttpActionResult> GetProducts()
        {
            try
            {
                //optimize this code by using cache
                const string cacheKey = "ProductsCacheKey";
                var products = await _cache.GetStringAsync(cacheKey);

                if (products == null)
                {
                    var productQuery = await _productRepository.GetProductsAsync();

                    // Fetch only necessary data from the database
                    var simplifiedProducts = productQuery.Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Price
                    }).ToList();


                    products = JsonConvert.SerializeObject(simplifiedProducts);

                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Cache for 1 hour
                    };

                    await _cache.SetStringAsync(cacheKey, products, options);
                }

                var productListFromCache = JsonConvert.DeserializeObject<List<Product>>(products);
                return Ok(productListFromCache);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpGet]
        [Route("api/products/all")]
        public async Task<IHttpActionResult> GetAllProducts()
        {
            try
            {
                var products = await Task.Run(() => _productRepository.GetProductsAsync());
                // Process the products in parallel
                var processedProducts = await Task.WhenAll(products.Select(ProcessProductAsync));

                return Ok(processedProducts);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private async Task<dynamic> ProcessProductAsync(Product product)
        {
            // Simulate some CPU-bound processing
            await Task.Delay(100);

            return new
            {
                Id = product.Id,
                Name = product.Name.ToUpper(),
                Price = product.Price * 1.1 // Increase price by 10%
            };
        }
    }
}
