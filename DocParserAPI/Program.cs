using Amazon.Runtime.Internal;
using Microsoft.AspNetCore.Builder;
using DocParserAPI.Models;
using DocParserAPI.Services;
using DocParser;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;


const string tessDataPath = @"C:\Users\Maros\source\repos\DocParser\DocParserAPI\bin\Debug\net7.0\tessdata";

var objectSerializer = new ObjectSerializer(type =>
    ObjectSerializer.DefaultAllowedTypes(type) || type.FullName.StartsWith("DocParser"));
BsonSerializer.RegisterSerializer(objectSerializer);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<DocParserDatabaseSettings>(
    builder.Configuration.GetSection("DocParserDatabase"));
builder.Services.AddSingleton<DocsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/api/documents", async (DocsService db) => await db.GetAsync())
    //.WithName("GetDocuments")
    .WithOpenApi();

app.MapGet("/api/documents/{id}", async (string id, DocsService db) => await db.GetAsync(id))
    .WithOpenApi();

app.MapPost("api/parse", async (Doc doc, DocsService db) =>
    {
        var parser = new PdfParser(tessDataPath);
        doc.Data = parser.Parse(Convert.FromBase64String(doc.File));
        doc.File = doc.File;
        await db.CreateAsync(doc);
        return Results.Created($"/api/documents/{doc.Id}", doc);
    })
    .WithOpenApi();

app.Run();