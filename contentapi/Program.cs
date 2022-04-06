using Amazon.S3;
using contentapi;
using contentapi.Controllers;
using contentapi.Db;
using contentapi.Main;
using contentapi.Search;
using contentapi.Setup;
using contentapi.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// We only use defaults for our regular runtime stuff! Overriding defaults is for testing
// or special deploys or whatever.
DefaultSetup.AddDefaultServices(builder.Services);
DefaultSetup.AddConfigBinding<GenericSearcherConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<UserServiceConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<UserControllerConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<FileControllerConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<EmailConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<RateLimitConfig>(builder.Services, builder.Configuration);
DefaultSetup.AddConfigBinding<StatusControllerConfig>(builder.Services, builder.Configuration);
builder.Services.AddTransient<BaseControllerServices>();

// Set up configurable services, as in stuff that users might want to turn off/etc
var desiredEmailer = builder.Configuration.GetValue<string>("EmailSender");

if(desiredEmailer == "null")
    builder.Services.AddSingleton<IEmailService, NullEmailService>();
else if(desiredEmailer == "default")
    builder.Services.AddSingleton<IEmailService, EmailService>();
else
    throw new InvalidOperationException($"Unknown emailer type {desiredEmailer}");

string secretKey = builder.Configuration.GetValue<string>("SecretKey"); //"pleasechangethis";
var validationParameters = DefaultSetup.AddSecurity(builder.Services, secretKey);

//The default setup doesn't set up our database provider though
builder.Services.AddTransient<ContentApiDbConnection>(ctx => 
    new ContentApiDbConnection(new SqliteConnection(builder.Configuration.GetConnectionString("contentapi"))));

//In kland, the amazon stuff comes after cors. Just want to make sure it's the same...
builder.Services.AddCors();
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();

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

Action<Newtonsoft.Json.JsonSerializerSettings> setupJsonOptions = opts =>
{
    opts.Converters.Add(new CustomDateTimeConverter());
    opts.Converters.Add(new StringEnumConverter());
};
JsonConvert.DefaultSettings = () => {
    var settings = new JsonSerializerSettings();
    setupJsonOptions(settings);
    return settings;
};

//// System.Text STILL does not do what I want it to do
builder.Services.AddControllers().AddNewtonsoftJson(options => setupJsonOptions(options.SerializerSettings));
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
app.UseSwaggerUI(c => {
    c.ConfigObject.AdditionalItems["syntaxHighlight"] = new Dictionary<string, object>
    {
        ["activated"] = false
    };
});
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

app.UseStaticFiles(builder.Configuration.GetValue<string>("StaticPath"));

app.UseAuthentication();
app.UseAuthorization();

//MUST COME BEFORE USE ENDPOINTS 
//https://www.koskila.net/httpcontext-websockets-iswebsocketrequest-always-null-in-your-net-core-code/
app.UseWebSockets(new WebSocketOptions()
{
    //Make this configurable later?
    KeepAliveInterval = TimeSpan.FromSeconds(15)
});

app.MapControllers();

app.Run();
