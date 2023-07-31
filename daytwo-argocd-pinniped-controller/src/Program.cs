using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace daytwo
{
    public static class Globals
    {
        public static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        public static CancellationToken cancellationToken = new CancellationToken();

        public static Service service;
        public static ILogger log;
    }

    /*
    public static class MyJPIF
    {
        public static NewtonsoftJsonPatchInputFormatter GetJsonPatchInputFormatter()
        {
            var builder = new ServiceCollection()
                .AddLogging()
                .AddMvc()
                .AddNewtonsoftJson()
                .Services.BuildServiceProvider();

            return builder
                .GetRequiredService<IOptions<MvcOptions>>()
                .Value
                .InputFormatters
                .OfType<NewtonsoftJsonPatchInputFormatter>()
                .First();
        }
    }
    */

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddHostedService<Service>();

            // get instance of logger for writing to console
            Globals.log = LoggerFactory.Create(config =>
            {
                config.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy:MM:dd H:mm:ss ";
                });
            }).CreateLogger("");


            // Add services to the container.
            builder.Services.AddControllers();
            /*
            builder.Services.AddControllers(options =>
            {
                options.InputFormatters.Insert(0, MyJPIF.GetJsonPatchInputFormatter());
            });
            */

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "list",
                    Description = "",
                    TermsOfService = new Uri("https://travisloyd.xyz/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Travis Loyd",
                        Url = new Uri("https://travisloyd.xyz")
                    }
                });

                /*
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme.<br><br>
                      Enter 'Bearer' [space] and then your token in the text input below.
                      <br><br>Example: 'Bearer 4d947de29ba54093a8ba57d5c9ed18764d947de29ba54093a8ba57d5c9ed1876=='",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                */

                c.AddSecurityDefinition("X-Api-Key", new OpenApiSecurityScheme
                {
                    Name = "X-Api-Key",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKeyScheme",
                    In = ParameterLocation.Header,
                    Description = "X-Api-Key must appear in header"
                });

                /*
                c.AddSecurityRequirement(new OpenApiSecurityRequirement() {{
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
                }});
                */

                c.AddSecurityRequirement(new OpenApiSecurityRequirement {{
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "X-Api-Key"
                        },
                        In = ParameterLocation.Header
                    },
                    new string[]{}
                }});

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            /*
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins()
                                .AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                        //builder.WithOrigins("http://example.com",
                        //                    "http://www.contoso.com");
                    });
            });
            */

            /*
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            IdentityModelEventSource.ShowPII = true;

            builder.Services.AddAuthentication(options => {

                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => {
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // use always when using https
                //options.Cookie.SameSite = SameSiteMode.Lax;
                //options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            })
            .AddOpenIdConnect(options => {

                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.Authority = Environment.GetEnvironmentVariable("OIDC_ENDPOINT");
                options.ClientId = Environment.GetEnvironmentVariable("OIDC_CLIENT_ID");
                options.ClientSecret = Environment.GetEnvironmentVariable("OIDC_CLIENT_SECRET");
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.CallbackPath = Environment.GetEnvironmentVariable("OIDC_CALLBACK");
                options.GetClaimsFromUserInfoEndpoint = true;

                string scopeString = Environment.GetEnvironmentVariable("OIDC_SCOPE");
                scopeString.Split(" ", StringSplitOptions.TrimEntries).ToList().ForEach(scope => {
                    options.Scope.Add(scope);
                });

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Authority,
                    ValidAudience = options.ClientId
                };

                options.Events.OnRedirectToIdentityProviderForSignOut = (context) =>
                {
                    //context.ProtocolMessage.PostLogoutRedirectUri = "https://gge.vc-non.k.home.net";
                    return Task.CompletedTask;
                };

                options.SaveTokens = true;
            });
            */


            // Set listen port to 80
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://*:80");

            var app = builder.Build();

            // Configure the HTTP request pipeline.
//            if (app.Environment.IsDevelopment())
//            {
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "swagger/list/{documentName}/swagger.json";
                });
                app.UseSwaggerUI(c => {
                    c.SwaggerEndpoint("/swagger/list/v1/swagger.json", "My API V1");
                    c.RoutePrefix = "swagger/list";
                });


                // This is required when developing using 'http' & 'localhost'
                app.UseCookiePolicy(new CookiePolicyOptions()
                {
                    MinimumSameSitePolicy = SameSiteMode.Lax
                });
//            }

            // enable rest methods
            app.MapControllers();

            app.Run();
        }
    }
}