using GovUk.Frontend.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using TeacherIdentityServer;
using TeacherIdentityServer.Models;
using TeacherIdentityServer.State;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGovUkFrontend(options => options.AddImportsToHtml = false);

builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

        options.Events.OnRedirectToLogin = ctx =>
        {
            var authStateId = Guid.NewGuid();
            var authState = new AuthenticationState(authStateId, ctx.Properties.RedirectUri!);
            ctx.HttpContext.Features.Set(new AuthenticationStateFeature(authState));

            // TODO Use IUrlHelper here

            ctx.Response.Redirect("/email" + QueryString.Create(AuthenticationStateMiddleware.IdQueryParameterName, authStateId.ToString()));

            return Task.CompletedTask;
        };
    });

builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages(options =>
{
    // Every page within the SignIn folder must have AuthenticationState passed to it
    options.Conventions.AddFolderApplicationModelConvention("/SignIn", model => model.Filters.Add(new RequireAuthenticationStateFilter()));
});

builder.Services.AddSession();

builder.Services.AddDbContext<TeacherIdentityServerDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    options.UseOpenIddict();
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<TeacherIdentityServerDbContext>();
    })
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            //.SetLogoutEndpointUris
            .SetTokenEndpointUris("/connect/token")
            .SetUserinfoEndpointUris("/connect/userinfo");

        options.RegisterScopes(Scopes.Email, Scopes.Profile);

        options
            .AllowAuthorizationCodeFlow()
            .AllowImplicitFlow()
            .AllowHybridFlow()
            .AllowClientCredentialsFlow();

        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableLogoutEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

        options.DisableAccessTokenEncryption();

        options.RegisterClaims(CustomClaims.QualifiedTeacherTrn);
        options.RegisterScopes(CustomScopes.QualifiedTeacher);
    });
// TODO Validation?

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseStatusCodePagesWithReExecute("~/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();

app.UseMiddleware<AuthenticationStateMiddleware>();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapRazorPages();
});

using (var scope = app.Services.CreateAsyncScope())
{
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    var dbContext = scope.ServiceProvider.GetRequiredService<TeacherIdentityServerDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Web client
    {
        var client = await manager.FindByClientIdAsync("client");
        if (client is not null)
        {
            await manager.DeleteAsync(client);
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor()
        {
            ClientId = "client",
            ClientSecret = "super-secret",
            //ConsentType = ConsentTypes.Explicit,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Sample Client app",
            RedirectUris =
            {
                new Uri("https://localhost:7261/oidc/callback")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.Implicit,
                Permissions.ResponseTypes.Code,
                Permissions.ResponseTypes.IdToken,
                Permissions.ResponseTypes.CodeIdToken,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                $"scp:{CustomScopes.QualifiedTeacher}"
            },
            Requirements =
            {
                //Requirements.Features.ProofKeyForCodeExchange
            }
        });
    }

    // Console app client
    {
        var client = await manager.FindByClientIdAsync("client2");
        if (client is not null)
        {
            await manager.DeleteAsync(client);
        }

        await manager.CreateAsync(new OpenIddictApplicationDescriptor()
        {
            ClientId = "client2",
            ClientSecret = "another-big-secret",
            //ConsentType = ConsentTypes.Explicit,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Sample Client app 2",
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.ResponseTypes.Token,
                //Permissions.Scopes.Email,
                //Permissions.Scopes.Profile,
            },
            Requirements =
            {
                //Requirements.Features.ProofKeyForCodeExchange
            }
        });
    }
}

app.Run();
