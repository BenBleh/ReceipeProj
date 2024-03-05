﻿using RecipeAPI.Controllers;
using RecipeAPI.DataAccess.JsonFileBased;
using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using System.Collections;
var builder = WebApplication.CreateBuilder(args);

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

