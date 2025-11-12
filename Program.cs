using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace SimpleRepricer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }


        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls("http://0.0.0.0:5000");
                });
    }


    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }


            // Serve default files (index.html) from wwwroot
            app.UseDefaultFiles();       // <-- add
            app.UseStaticFiles();        // <-- add


            app.UseRouting();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    // serve index from wwwroot to match UseStaticFiles()
                    await context.Response.SendFileAsync("wwwroot/index.html");
                });


                // API для расчета цен
                endpoints.MapPost("/api/calculate", async context =>
                {
                    var request = await context.Request.ReadFromJsonAsync<CalculationRequest>();
                    var calculator = new PriceCalculator();
                    var results = calculator.CalculatePrices(request.Products, request.Competitors);
                    await context.Response.WriteAsJsonAsync(results);
                });


                // Fallback to wwwroot/index.html for SPA routing
                endpoints.MapFallbackToFile("index.html"); // <-- add
            });
        }
    }


    public class CalculationRequest
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public List<Competitor> Competitors { get; set; } = new List<Competitor>();
    }


    public class Product
    {
        public string Article { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal CostPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = "";
        public decimal RecommendedPrice { get; set; }
        public string Strategy { get; set; } = "";
        public int CompetitorsCount { get; set; }
        public decimal MinCompetitorPrice { get; set; }
        public decimal AvgCompetitorPrice { get; set; }
    }


    public class Competitor
    {
        public string ProductArticle { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string ProductName { get; set; } = "";
    }


    public class PriceCalculator
    {
        private const decimal MIN_MARGIN = 10.0m; // Минимальная маржа 10%


        public List<Product> CalculatePrices(List<Product> products, List<Competitor> competitors)
        {
            var results = new List<Product>();


            foreach (var product in products)
            {
                var productCompetitors = competitors
                    .Where(c => c.ProductArticle == product.Article)
                    .ToList();


                decimal recommendedPrice;
                string strategy;


                if (productCompetitors.Any())
                {
                    // Стратегия: быть на 2% ниже средней цены, но не ниже минимальной маржи
                    var avgCompetitorPrice = productCompetitors.Average(c => c.Price);
                    var minPriceWithMargin = product.CostPrice * (1 + MIN_MARGIN / 100);
                   
                    recommendedPrice = Math.Max(avgCompetitorPrice * 0.98m, minPriceWithMargin);
                    strategy = "Конкурентная";
                }
                else
                {
                    // Базовая цена с маржой
                    recommendedPrice = product.CostPrice * (1 + MIN_MARGIN / 100);
                    strategy = "Базовая";
                }


                // Округляем до целых
                recommendedPrice = Math.Round(recommendedPrice);


                results.Add(new Product
                {
                    Article = product.Article,
                    Name = product.Name,
                    CostPrice = product.CostPrice,
                    CurrentPrice = product.CurrentPrice,
                    Stock = product.Stock,
                    Category = product.Category,
                    RecommendedPrice = recommendedPrice,
                    Strategy = strategy,
                    CompetitorsCount = productCompetitors.Count,
                    MinCompetitorPrice = productCompetitors.Any() ? productCompetitors.Min(c => c.Price) : 0,
                    AvgCompetitorPrice = productCompetitors.Any() ? productCompetitors.Average(c => c.Price) : 0
                });
            }


            return results;
        }
    }
}
