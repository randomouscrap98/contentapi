using contentapi.Db;
using contentapi.Setup;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// We only use defaults for our regular runtime stuff! Overriding defaults is for testing
// or special deploys or whatever.
DefaultSetup.AddDefaultServices(builder.Services);

//The default setup doesn't set up our database provider though
builder.Services.AddTransient<ContentApiDbConnection>(ctx => 
    new ContentApiDbConnection(new SqliteConnection("Data Source=newcontent.db")));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
// I ALWAYS want swagger, no matter what environment it is
app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
