namespace Demo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // SecurityModuleNotes.cs
            // This file is a compact assignment-like demo with notes & mini-snippets per video part.
            // Keep it in the solution just for study/demo; real implementation goes in proper files.

            using Microsoft.AspNetCore.Identity;
            using Microsoft.EntityFrameworkCore;

            #region Part 01 - Security Module Overview
            /*
            Goals:
            - Understand the building blocks: DbContext, Identity, Middleware pipeline.
            - Register MVC, EF Core, Identity, and cookie paths.

            Checklist:
            - AddControllersWithViews().
            - AddDbContext<ApplicationDbContext>(UseSqlServer/UseSqlite).
            - AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<...>().
            - ConfigureApplicationCookie(LoginPath, LogoutPath, AccessDeniedPath).
            - app.UseAuthentication(); app.UseAuthorization(); Map default route.
            */

            void ConfigureOverview(WebApplicationBuilder builder)
            {
                builder.Services.AddControllersWithViews();

                builder.Services.AddDbContext<ApplicationDbContext>(opt =>
                    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
                // or: opt.UseSqlite("Data Source=route_security_demo.db");

                builder.Services
                    .AddIdentity<ApplicationUser, IdentityRole>(opt =>
                    {
                        opt.Password.RequiredLength = 6;
                        opt.Password.RequireDigit = true;
                        opt.Password.RequireNonAlphanumeric = false;
                        opt.User.RequireUniqueEmail = true;
                    })
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders();

                builder.Services.ConfigureApplicationCookie(opt =>
                {
                    opt.LoginPath = "/Account/Login";
                    opt.LogoutPath = "/Account/Logout";
                    opt.AccessDeniedPath = "/Account/AccessDenied";
                });
            }
#endregion

            #region Part 02 - Identity Tables - Migration
            /*
            Goals:
            - Create Identity schema via EF Core Migrations.
            - Know default tables: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens.

            Steps:
            1) Add ApplicationDbContext : IdentityDbContext<ApplicationUser>.
            2) Add connection string.
            3) PMC:
               - Add-Migration InitIdentity
               - Update-Database
            Hints:
            - Ensure the project has Microsoft.EntityFrameworkCore.* and Identity packages.
            */

public class ApplicationUser : IdentityUser { }

        public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
        {
            public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        }
        #endregion

        #region Part 03 - Users & Roles Seeding
        /*
        Goals:
        - Seed default roles (Admin/User) and an Admin account on startup.

        Key Ideas:
        - CreateScope() at startup, resolve RoleManager & UserManager.
        - Check role existence -> create.
        - Check admin user -> create -> AddToRole("Admin").

        Common Pitfalls:
        - Seeding must be awaited before app.Run().
        - EmailConfirmed = true for demo login without email flows.
        */

        static async Task SeedAsync(IServiceProvider sp)
        {
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "Admin", "User" })
                if (!await roleMgr.RoleExistsAsync(role))
                    await roleMgr.CreateAsync(new IdentityRole(role));

            var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var adminEmail = "admin@demo.com";
            var admin = await userMgr.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                var ok = await userMgr.CreateAsync(admin, "Admin123!");
                if (ok.Succeeded) await userMgr.AddToRoleAsync(admin, "Admin");
            }
        }
        #endregion

        #region Part 04 - Account Service
        /*
        Goals:
        - Encapsulate auth logic in a service layer (SOLID).
        - Methods: Register, Login, Logout.

        Notes:
        - Return (bool ok, IEnumerable<string> errors) for clean controller code.
        - UserManager handles users, SignInManager handles cookies & sign-in.

        */

        public interface IAccountService
        {
            Task<(bool ok, IEnumerable<string> errors)> RegisterAsync(string email, string password);
            Task<(bool ok, IEnumerable<string> errors)> LoginAsync(string email, string password, bool rememberMe);
            Task LogoutAsync();
        }

        public class AccountService : IAccountService
        {
            private readonly UserManager<ApplicationUser> _users;
            private readonly SignInManager<ApplicationUser> _signIn;

            public AccountService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
            {
                _users = users; _signIn = signIn;
            }

            public async Task<(bool ok, IEnumerable<string> errors)> RegisterAsync(string email, string password)
            {
                var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var r = await _users.CreateAsync(u, password);
                return r.Succeeded ? (true, Array.Empty<string>()) : (false, r.Errors.Select(e => e.Description));
            }

            public async Task<(bool ok, IEnumerable<string> errors)> LoginAsync(string email, string password, bool rememberMe)
            {
                var r = await _signIn.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
                return r.Succeeded ? (true, Array.Empty<string>()) : (false, new[] { "Invalid email or password." });
            }

            public Task LogoutAsync() => _signIn.SignOutAsync();
        }
        #endregion

        #region Part 05 - Account Controller - Login
        /*
        Goals:
        - Build Login GET/POST actions with ReturnUrl + ModelState errors.

        Tips:
        - Only redirect to local URLs (Url.IsLocalUrl).
        - Keep ViewModel minimal: Email, Password, RememberMe, ReturnUrl.

        */

        public class LoginVm
        {
            [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
            public string Email { get; set; } = string.Empty;

            [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.DataType(System.ComponentModel.DataAnnotations.DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
            public string? ReturnUrl { get; set; }
        }
        #endregion

        #region Part 06 - Account Controller - Logout
        /*
        Goals:
        - Provide Logout action secured by [Authorize].
        - After logout, redirect to Home/Index.

        Notes:
        - Cookie is cleared by SignInManager.SignOutAsync() through AccountService.
        */
        #endregion

        #region Part 07 - Account Controller - AccessDenied
        /*
        Goals:
        - Show friendly page for 403 (authorization failure).
        - Configure cookie AccessDeniedPath = "/Account/AccessDenied".
        */
        #endregion

        #region Bonus - Minimal Startup Sketch (for your Program.cs)
        /*
        var builder = WebApplication.CreateBuilder(args);
        ConfigureOverview(builder);                       // Part 01

        builder.Services.AddScoped<IAccountService, AccountService>(); // Part 04

        var app = builder.Build();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDefaultControllerRoute();

        // Part 03: seeding at startup
        using (var scope = app.Services.CreateScope())
            await SeedAsync(scope.ServiceProvider);

        app.Run();
        */
        #endregion

    }
}
}
