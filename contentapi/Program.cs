using System.Text;
using contentapi.AutoMapping;
using contentapi.Db;
using contentapi.Implementations;
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

//The default setup doesn't set up our database provider though
builder.Services.AddTransient<ContentApiDbConnection>(ctx => 
    new ContentApiDbConnection(new SqliteConnection(builder.Configuration.GetConnectionString("contentapi"))));

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
    x.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("pleasereplacethis")),
            //tokenSection.GetValue<string>("SecretKey"))),
        ValidateIssuer = false,
        ValidateAudience = false
    };
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
