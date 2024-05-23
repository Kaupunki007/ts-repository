using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using System.IO;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Define Prometheus metrics
var requestCounter = Metrics.CreateCounter("api_requests_total", "Total number of requests received", new CounterConfiguration
{
    LabelNames = new[] { "method", "endpoint" }
});

var requestDuration = Metrics.CreateSummary("api_request_duration_seconds", "The duration of HTTP requests in seconds", new SummaryConfiguration
{
    LabelNames = new[] { "method", "endpoint" }
});

app.UseMetricServer(); // Expose the Prometheus metrics endpoint at /metrics

app.Use(async (context, next) =>
{
    // Start a timer to measure request duration
    var timer = requestDuration.WithLabels(context.Request.Method, context.Request.Path).NewTimer();

    try
    {
        context.Request.EnableBuffering();
        await next();
    }
    finally
    {
        // Stop the timer and observe the duration
        timer.ObserveDuration();

        // Increment the request counter
        requestCounter.WithLabels(context.Request.Method, context.Request.Path).Inc();
    }
});

app.Run(async context =>
{
    context.Response.ContentType = "text/html";

    var method = context.Request.Method;
    var headers = context.Request.Headers;
    string body;

    using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
    {
        body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;  // Reset the request body stream position for further reading
    }

    await context.Response.WriteAsync("<h1>Welcome to our API, here are your details for the request</h1>");
    await context.Response.WriteAsync("<h2>Headers</h2><pre>");
    foreach (var header in headers)
    {
        await context.Response.WriteAsync($"{header.Key}: {header.Value}<br>");
    }
    await context.Response.WriteAsync("</pre>");

    await context.Response.WriteAsync($"<h2>Method</h2><p>{method}</p>");
    await context.Response.WriteAsync($"<h2>Body</h2><pre>{body}</pre>");
});

app.Run();
