using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using VerduleriaAugust.Web;
using VerduleriaAugust.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("No se configuró ApiBaseUrl.");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<SessionAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<SessionAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductsService>();
builder.Services.AddScoped<CashRegistersService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<PaymentMethodsService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<ScalesService>();
builder.Services.AddScoped<GoodsReceiptsService>();

await builder.Build().RunAsync();
