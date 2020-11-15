using System.Linq;
using AutoMapper;
using BatBot.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BatBot.Server.Models;
using BatBot.Server.Profiles;
using BatBot.Server.Services;
using Microsoft.AspNetCore.ResponseCompression;

namespace BatBot.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddScoped<MempoolMonitoringService>()
                .AddScoped<BlocknativeMonitoringService>()
                .AddScoped<TransactionProcessorService>()
                .AddScoped<BlocknativeMessageService>()
                .AddSingleton<BlockchainService>()
                .AddSingleton<PairInfoService>()
                .AddSingleton<BackoffService>()
                .AddSingleton<MessagingService>()
                .AddSingleton<GraphService>()
                .AddSingleton<EthereumSubscriptionService>()
                .AddSingleton<TransactionWaitService>()
                .Configure<BatBotOptions>(Configuration.GetSection("BatBot"))
                .Configure<SettingsOptions>(Configuration.GetSection("Settings"))
                .AddAutoMapper(typeof(TransactionProfile))
                .AddAutoMapper(typeof(GraphProfile));

            services.AddControllersWithViews();
            services.AddRazorPages();
            services.AddSignalR();
            services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] {"application/octet-stream"});
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
                endpoints.MapHub<MessageHub>("/messageHub");
            });
        }
    }
}
