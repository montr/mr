using Microsoft.AspNetCore.Http.Features;
using MonoReport.Api.Services;
using MonoReport.Models;

var builder = WebApplication.CreateBuilder(args);
var requestLimit = builder.Configuration.GetValue<long?>("MonoReport:MaxRequestBytes") ?? 50L * 1024 * 1024;
if (requestLimit <= 0 || requestLimit > int.MaxValue) throw new InvalidOperationException("MonoReport:MaxRequestBytes must be positive and no greater than Int32.MaxValue.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = requestLimit);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = requestLimit;
    options.ValueLengthLimit = (int)requestLimit;
});
builder.Services.AddMonoReport();
builder.Services.AddTransient<MultipartRequestReader>();
builder.Services.AddTransient<DocumentEndpoints>();
builder.Services.AddTransient<GenerationOptionsFactory>();
builder.Services.AddSingleton<ApiProblemFactory>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer<ApiOperationTransformer>();
    options.AddSchemaTransformer<ValidationSchemaTransformer>();
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    var problems = context.RequestServices.GetRequiredService<ApiProblemFactory>();
    try
    {
        if (context.Request.ContentLength > requestLimit)
            throw new RequestInputException(413, "request.too_large");
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = requestLimit;
        await next(context);
    }
    catch (RequestInputException error) { await problems.Create(context, error.Status, error.Code, error.Field).ExecuteAsync(context); }
    catch (BadHttpRequestException error) { await problems.Create(context, error.StatusCode == 413 ? 413 : 400, error.StatusCode == 413 ? "request.too_large" : "request.invalid_multipart").ExecuteAsync(context); }
    catch (TemplateException error) { await problems.Create(context, 422, error.Code, error.Field).ExecuteAsync(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Document request failed ({TraceId})", context.TraceIdentifier);
        if (!context.Response.HasStarted) await problems.Create(context, 500, "server.internal_error").ExecuteAsync(context);
    }
});
app.MapPost("/api/documents/generate", (HttpContext context, DocumentEndpoints endpoints) => endpoints.Generate(context))
    .WithName("GenerateDocument").Produces(200, contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
    .ProducesProblem(400).ProducesProblem(413).ProducesProblem(415).ProducesProblem(422).ProducesProblem(500);
app.MapPost("/api/documents/validate", (HttpContext context, DocumentEndpoints endpoints) => endpoints.Validate(context))
    .WithName("ValidateTemplate").Produces<TemplateValidationResult>()
    .ProducesProblem(400).ProducesProblem(413).ProducesProblem(415).ProducesProblem(500);

if (builder.Configuration.GetValue<bool?>("MonoReport:DocumentationEnabled") ?? true)
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "docs";
        options.SwaggerEndpoint("/openapi/v1.json", "MonoReport API v1");
        options.DocumentTitle = "MonoReport API";
    });
}
app.Run();

public partial class Program;
