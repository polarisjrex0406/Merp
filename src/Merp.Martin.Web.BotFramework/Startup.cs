﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Merp.Martin.Web.BotFramework
{
    /// <summary>
    /// The Startup class configures services and the request pipeline.
    /// </summary>
    public class Startup
    {
        private ILoggerFactory _loggerFactory;
        private bool _isProduction = true;

        public Startup(IWebHostEnvironment env)
        {
            //_isProduction = env.IsProduction();

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> specifies the contract for a collection of service descriptors.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/dependency-injection"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/azure/bot-service/bot-service-manage-channels?view=azure-bot-service-4.0"/>
        public void ConfigureServices(IServiceCollection services)
        {
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;
            var botFilePath = Configuration.GetSection("botFilePath")?.Value;

            // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
            var botConfig = BotConfiguration.Load(botFilePath ?? @".\BotConfiguration.bot", secretKey);
            services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot configuration file could not be loaded. ({botConfig})"));

            // Initialize Bot Connected Services clients.
            var connectedServices = new BotServices(botConfig);
            services.AddSingleton(sp => connectedServices);

            services.AddSingleton(sp => botConfig);

            services.AddDbContext<Merp.Accountancy.QueryStack.AccountancyDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("Merp-Accountancy-ReadModel")));
            services.AddDbContext<Merp.Accountancy.Drafts.AccountancyDraftsDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("Merp-Accountancy-Drafts")));
            services.AddDbContext<Merp.Registry.QueryStack.RegistryDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("Merp-Registry-ReadModel")));
            services.AddDbContext<Merp.TimeTracking.TaskManagement.QueryStack.TaskManagementDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("Merp-TimeTracking-ReadModel")));

            services.AddScoped<Merp.Martin.Intents.Accountancy.GrossIncomeIntentWorker>();
            services.AddScoped<Merp.Martin.Intents.Accountancy.NetIncomeIntentWorker>();
            services.AddScoped<Merp.Martin.Intents.Accountancy.OutgoingInvoicePaymentCheckWorker>();

            services.AddScoped<Merp.Martin.Intents.General.LogoutWorker>();
            services.AddScoped<Merp.Martin.Intents.General.HelpWorker>();
            services.AddScoped<Merp.Martin.Intents.General.GreetingWorker>();

            services.AddBot<LuisBot>(options =>
            {
                // Retrieve current endpoint.
                var environment = _isProduction ? "production" : "development";
                var service = botConfig.Services.Where(s => s.Type == "endpoint" && s.Name == environment).FirstOrDefault();
                if (!(service is EndpointService endpointService))
                {
                    throw new InvalidOperationException($"The .bot file does not contain an endpoint with name '{environment}'.");
                }

                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                // Creates a logger for the application to use.
                ILogger logger = _loggerFactory.CreateLogger<LuisBot>();

                // Catches any errors that occur during a conversation turn and logs them.
                options.OnTurnError = async (context, exception) =>
                {
                    logger.LogError($"Exception caught : {exception}");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };

                // The Memory Storage used here is for local bot debugging only. When the bot
                // is restarted, everything stored in memory will be gone.
                IStorage dataStore = new MemoryStorage();

                // For production bots use the Azure Blob or
                // Azure CosmosDB storage providers. For the Azure
                // based storage providers, add the Microsoft.Bot.Builder.Azure
                // Nuget package to your solution. That package is found at:
                // https://www.nuget.org/packages/Microsoft.Bot.Builder.Azure/
                // Uncomment the following lines to use Azure Blob Storage
                // //Storage configuration name or ID from the .bot file.
                // const string StorageConfigurationId = "<STORAGE-NAME-OR-ID-FROM-BOT-FILE>";
                // var blobConfig = botConfig.FindServiceByNameOrId(StorageConfigurationId);
                // if (!(blobConfig is BlobStorageService blobStorageConfig))
                // {
                //    throw new InvalidOperationException($"The .bot file does not contain an blob storage with name '{StorageConfigurationId}'.");
                // }
                // // Default container name.
                // const string DefaultBotContainer = "<DEFAULT-CONTAINER>";
                // var storageContainer = string.IsNullOrWhiteSpace(blobStorageConfig.Container) ? DefaultBotContainer : blobStorageConfig.Container;
                // IStorage dataStore = new Microsoft.Bot.Builder.Azure.AzureBlobStorage(blobStorageConfig.ConnectionString, storageContainer);

                // Create Conversation State object.
                // The Conversation State object is where we persist anything at the conversation-scope.
                var conversationState = new ConversationState(dataStore);

                options.State.Add(conversationState);
            });

            // Create and register state accessors.
            // Accessors created here are passed into the IBot-derived class on every turn.
            services.AddSingleton<LuisBotAccessors>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;

                if (options == null)
                {
                    throw new InvalidOperationException("BotFrameworkOptions must be configured prior to setting up the State Accessors.");
                }

                var conversationState = options.State.OfType<ConversationState>().FirstOrDefault();

                if (conversationState == null)
                {
                    throw new InvalidOperationException("ConversationState must be defined and added before adding conversation-scoped state accessors.");
                }

                // Create Custom State Property accessors
                // State Property Accessors enable components to read and write individual properties, without having to
                // pass the entire state object.
                var accessors = new LuisBotAccessors
                {
                    ConversationDialogState = conversationState.CreateProperty<DialogState>(LuisBotAccessors.DialogStateName),
                };

                return accessors;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
