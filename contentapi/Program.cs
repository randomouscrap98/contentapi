using System.Text;
using contentapi.Controllers;
using contentapi.Db;
using contentapi.Db.History;
using contentapi.Search;
using contentapi.Setup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAutoMapper(typeof(ContentSnapshotProfile)); //You can pick ANY profile, it just needs some type from the binary

// We only use defaults for our regular runtime stuff! Overriding defaults is for testing
// or special deploys or whatever.
DefaultSetup.AddDefaultServices(builder.Services);
DefaultSetup.AddConfigBinding<GenericSearcherConfig>(builder.Services, builder.Configuration);
builder.Services.AddTransient<BaseControllerServices>();

string secretKey = builder.Configuration.GetValue<string>("SecretKey"); //"pleasechangethis";

//The default setup doesn't set up our database provider though
builder.Services.AddTransient<ContentApiDbConnection>(ctx => 
    new ContentApiDbConnection(new SqliteConnection(builder.Configuration.GetConnectionString("contentapi"))));

//Not sure if this is ok, but adding security stuff to the service collection.
//It's just me using it, and I need this stuff in multiple places
var validationParameters = new TokenValidationParameters()
{
    ValidateIssuerSigningKey = true,
    RequireExpirationTime = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
    ValidateIssuer = false,
    ValidateAudience = false
};
builder.Services.AddSingleton(validationParameters);
builder.Services.AddSingleton(
    new SigningCredentials(
        new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(secretKey)), 
        SecurityAlgorithms.HmacSha256Signature)
);

builder.Services.AddCors();

//This section sets up(?) jwt authentication
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = validationParameters; 
});

// System.Text STILL does not do what I want it to do
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();

//This section sets up JWT to be used in swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "New SBS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"Bearer [space] token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// I ALWAYS want swagger, no matter what environment it is
app.UseSwagger();
app.UseSwaggerUI();
//c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
//    c.RoutePrefix = "api";
//});

app.UseCors(builder =>
{
    builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    .WithExposedHeaders("*");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
