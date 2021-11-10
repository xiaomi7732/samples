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
using System.IO;
using System.Diagnostics;

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

            async Task Tweet(HttpContext context)
            {
                var client = context.RequestServices.GetRequiredService<DaprClient>();
                var requestBodyStream = context.Request.Body;

                TwitterTweet tweet = null;
                string tweetContent = string.Empty;


                int random = new Random().Next();

                if(random % 2 == 0)
                {
                    RunSort(5000);
                    tweet = await JsonSerializer.DeserializeAsync<TwitterTweet>(requestBodyStream);
                }
                else
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        await requestBodyStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                        for (int i = 0; i < 10000; i++)
                        {
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            tweet = await JsonSerializer.DeserializeAsync<TwitterTweet>(memoryStream).ConfigureAwait(false);
                            tweetContent += tweet.Text;
                        }
                    }
                }

                Console.WriteLine("Tweet received: {0}: {1}", tweet.ID, tweet.Text);

                await client.SaveStateAsync<TwitterTweet>(stateStore, tweet.ID, tweet);
                Console.WriteLine("Tweet saved: {0}: {1}", tweet.ID, tweet);

                return;
            }
        }

          public static int RunSort(int milliseconds)
        {
            int[] num = new int[1000000];
            int[] target = new int[1000000];
            Random rnd = new Random();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int i = 0;

            while (true)
            {
                i = i > 999999? 0 : ++i;
                num[i] = rnd.Next(0, 999999);
                Array.Sort(num);
                watch.Stop();

                if (watch.ElapsedMilliseconds > milliseconds)
                {
                    break;
                }

                watch.Start();
            }

            return 0;
        }
    }
}
