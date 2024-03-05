﻿using RecipeAPI.Controllers;
using RecipeAPI.DataAccess.JsonFileBased;
using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using System.Collections;
var builder = WebApplication.CreateBuilder(args);

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("https://localhost:4200", "https://localhost:7087");
                      });
});

//Set gloabl configs
ConfigurationHelper.Instance.RecipeFilePathValue = builder.Configuration.GetSection("RecipeFilePath").Value;

//Dependency injection
builder.Services.AddTransient<IRecipeAccess, RecipeAccessJson>();


// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(x => x
.WithOrigins("https://localhost:4200")
.AllowAnyHeader()
.AllowCredentials()
.AllowAnyMethod());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

