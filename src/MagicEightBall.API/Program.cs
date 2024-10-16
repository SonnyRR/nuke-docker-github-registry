using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpLogging(logging => logging.LoggingFields = HttpLoggingFields.All);

builder.Services.AddSwaggerGen(options =>
{
    string gitHubProfile = builder.Configuration["Links:GitHub"] ?? string.Empty;
    string license = builder.Configuration["Links:License"] ?? string.Empty;

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Magic 8-Ball API",
        Description = "An ASP.NET Core Web API for answering questions via an 8-Ball like mechanism.",
        Contact = new OpenApiContact
        {
            Name = "Vasil Kotsev",
            Url = new Uri(gitHubProfile)
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri(license)
        }
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();

app.UseHttpLogging();
app.UseSwagger();
app.UseRewriter(new RewriteOptions().AddRewrite("^$", "swagger", true));
app.UseSwaggerUI();
app.MapControllers();
await app.RunAsync();