using System;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Dapr.Client;
using System.Text.RegularExpressions;

namespace Provider
{
    public class Startup
    {
        // Name of the Dapr state store component
        public const string stateStore = "tweet-store";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddServiceProfiler(opt =>
            {
                opt.Duration = TimeSpan.FromSeconds(30);
            });

            services.AddDaprClient();
            services.AddSingleton(new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // The first value must match the name of the Dapr twitter binding
                // component.
                endpoints.MapPost("tweets", Tweet);
            });
        }

        private async Task Tweet(HttpContext context)
        {
            Regex validCharacterSet = new Regex(@"^[a-z0-9_:\/-\\]+$", RegexOptions.IgnoreCase|RegexOptions.Compiled);
            var client = context.RequestServices.GetRequiredService<DaprClient>();
            var requestBodyStream = context.Request.Body;

            TwitterTweet tweet = await JsonSerializer.DeserializeAsync<TwitterTweet>(requestBodyStream).ConfigureAwait(false);

            string fullText = tweet.FullText;
            if (string.IsNullOrEmpty(fullText))
            {
                fullText = tweet.Text;
            }
            Console.WriteLine("Tweet received: {0}: {1}", tweet.ID, fullText);

            bool includeInvalidCharacter = false;
            for (int i = 0; i < 1000000; i++)
            {
                includeInvalidCharacter = !validCharacterSet.IsMatch(fullText);
            }
            if (includeInvalidCharacter)
            {
                Console.WriteLine("There is invalid characters other than letter, numbers, underscore(_), and dash(-) signs.");
                return;
            }
            await client.SaveStateAsync<TwitterTweet>(stateStore, tweet.ID, tweet);
            Console.WriteLine("Tweet saved: {0}: {1}", tweet.ID, tweet);
            return;
        }
    }
}
