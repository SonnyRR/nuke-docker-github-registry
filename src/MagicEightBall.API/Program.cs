using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddHttpLogging(logging => logging.LoggingFields = HttpLoggingFields.All);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseHttpLogging();
app.MapOpenApi();
app.MapScalarApiReference(
    "/api/documentation",
    options =>
    {
        options.WithTitle("Magic 8-Ball API");
        options.WithOperationTitleSource(OperationTitleSource.Path);
        options.SortTagsAlphabetically();
    });

var possibleAnswers = new[]
{
    "As I see it, yes.",
    "Ask again later.",
    "Better not tell you now.",
    "Cannot predict now.",
    "Concentrate and ask again.",
    "Don't count on it.",
    "It is certain.",
    "It is decidedly so."
};

var random = new Random();

app.MapPost("/api/question/ask", (string q) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest();

    var answer = possibleAnswers[random.Next(possibleAnswers.Length - 1)];
    return Results.Ok(answer);
})
.WithName("AskQuestion")
.WithSummary("Answers a question provided by the user")
.WithDescription("Provides a random Magic 8-Ball answer to a question");

await app.RunAsync();
