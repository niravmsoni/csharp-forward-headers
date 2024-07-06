# Forward Headers (Remote Client IP/Remote Port retrieval) in .NET Core

Let's go through how we extract Client IP addresses within a .NET Core Web API.

Proxy servers, load balancers, and other network appliances often hide information about the request before it reaches the app:
- When HTTPS requests are proxied over HTTP, the original scheme (HTTPS) is lost and must be forwarded in a header.
- Because an app receives a request from the proxy and not its true source on the Internet or corporate network, the originating client IP address must also be forwarded in a header.
This information may be important in request processing, for example in redirects, authentication, auditing, and client geolocation purposes.

Whenever the client makes an HTTP Request to the server and when it reaches to the proxy, the proxy do forward these headers:
- `X-Forwarded-For` - Holds the information about client that initiated request + Subsequent Proxies in chain of proxy
- `X-Forwarded-Proto` - Originating Schema (HTTP/HTTPS)
- `X-Forwarded-Host` - Original value of Host header
- `X-Forwarded-Prefix` - Base path requested by the client
    
Out of all the above-mentioned header, the one that we're interested in for extracting the IP address is `X-Forwarded-For`

## How does the proxy treat this `X-Forwarded-For` header?
Let's assume:
 - Client IP address is **106.101.1.1**
 - Proxy IP address is **192.169.0.1** (In real world this would be dynamic but keeping it fixed for brevity)

Let's say the end user making a request to the API. The request would first reach the proxy and then in-turn would reach out end application. In this case, if we examine the value for `X-Forwarded-For` header, it will look something like this: **['106.101.1.1', '192.169.0.1']**.

So, it is safe to say that this header contains the IP address for both the end user and the intermediary proxy(s).

If there are multiple intermediary proxies, then the Proxy would keep on adding its' IP address to this header.

## Forwarded Headers Middleware in-action
.NET Core provides [ForwardedHeadersMiddleware](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.httpoverrides.forwardedheadersmiddleware?view=aspnetcore-8.0) that we can use to extract the IP address. But it is of paramount importance to set the header options correctly. Let's see them in action.

### Configuring **Forward Headers** options:
Let's understand it step by step:
- Setup **ForwardedHeadersOptions** - By default, no headers are forwarded. So, we need to setup ForwardedHeaders to forward `X-Forwarded-For` header
    
- Setup **ForwardLimit** - 
   - Single-Proxy - We are not explicitly required to set the **ForwardLimit** to 1 since that is the default value.
   - Multi-Proxy - In-case if the request routes through more than 1 intermediary proxy/load balancers, we need to appropriately set the **ForwardLimit** value for instructing the middleware to go the amount of depth (From Right to Left) in the IP Array as we saw above. If for example, we have `2` intermediary proxies, then this value needs to setup as **2**. If we do not do this, there are chances that the Middleware will set the Wrong value in the HttpContext object.
    
- Setup **KnownProxies**/**KnownNetworks** -
   - Proxy with a Fixed IP(s) 
      - We should use the **KnownProxies** property and feed that information in,
   - Proxy with a Dynamic IP(s) 
      - We should use the **KnownNetworks** and feed the CIDR Range using which the Proxy's IP address would be assigned to.
      - If we integrate our application with any 3rd party firewall solutions such as [Imperva](https://www.imperva.com/) within our deployment architecture, we need to know the possible IP addresses through which our request would be routed through. For example, Imperva does expose its public IPs through [this](https://my.imperva.com/api/integration/v1/ips) endpoint. 
    
### Injecting the **Forward Headers** Middleware
Since we are using the built-in middleware, it is important to place the middleware correctly. Microsoft recommends running this middleware before other middleware. This ensures that if any other middleware does rely on the forwarded header information, then they can consume the correct header values for processing.
The middleware sets the value present in `X-Forwarded-For` header based on the option configured in the **Forwarded Headers Options** in this HttpContext property:
- HttpContext.Connection.RemoteIpAddress

## Show me the code!

```csharp
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
...
// Configure Header Options 
builder.Services.Configure<ForwardedHeadersOptions>(options => 
{ 
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor; 
    options.ForwardLimit = 1; 
    options.KnownProxies.Add(IPAddress.Parse("192.169.0.1")); 
});
builder.AddHttpContextAccessor();

var app = builder.Build();

app.UseHttpsRedirection();
//Forwarded Headers Middleware setup
app.UseForwardedHeaders();
app.UseHttpsRedirection();
...

app.MapGet("/get-ip", (IHttpContextAccessor httpContextAccessor) =>
{
    var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;

    if (ipAddress == null)
    {
        return Results.Problem("Unable to determine IP address.");
    }

    return Results.Ok(new { IpAddress = ipAddress.ToString() });
});

app.Run();
```

## Troubleshooting
When headers aren't forwarded as expected, enable debug level logging and HTTP request logging
```csharp
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

//Enable HTTP Logging
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    //ForwardOptions Configuration
});

var app = builder.Build();

app.UseForwardedHeaders();
// Integrate HTTP Logging Middleware
app.UseHttpLogging();

app.Use(async (context, next) =>
{
    // Connection: RemoteIp
    app.Logger.LogInformation("Request RemoteIp: {RemoteIpAddress}",
        context.Connection.RemoteIpAddress);

    await next(context);
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
```
Updating `appsettings.json` to enable logging from  `Microsoft.AspnetCore.HttpLogging` namespace at Information level!
```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Microsoft.AspNetCore.HttpLogging": "Information"
    }
  }
}
```
## References
   - [MSFT Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0)
   - [ForwardedHeadersOptions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.forwardedheadersoptions?view=aspnetcore-8.0)
   - [ForwardedHeadersMiddleware](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.httpoverrides.forwardedheadersmiddleware?view=aspnetcore-8.0)


## Give a Star! ⭐
Feel free to request an issue on github if you find bugs or request a new feature. Your valuable feedback is much appreciated to better improve this project. If you find this useful, please give it a star to show your support for this project.
