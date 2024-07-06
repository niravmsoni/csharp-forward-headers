using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
//Enable HTTP Logging
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;

    // In-case of a single hard-coded proxy/load balancer's IP Address, use KnownProxies else use KnownNetworks
    options.KnownProxies.Add(IPAddress.Parse("192.169.0.1"));

    // In-case if a dynamic IP address assignment to the Proxy/Load Balancer, use KnownNetworks
    // If there are more than 1 Proxies through which the requests are routed, add all the Proxy addresses to the KnownNetworks and also set the ForwardLimit to the appropriate value (Based on number of intermediary proxies/load balancers)
    options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse("192.168.0.0/24"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

// Integrate HTTP Logging Middleware
app.UseHttpLogging();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
