using System.Configuration;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.WebApi;
using StackExchange.Redis;

namespace MilkBot
{
    public class WebApiApplication : HttpApplication
    {
        private ConnectionMultiplexer _redisConnectionMultiplexer;

        protected void Application_Start()
        {
            _redisConnectionMultiplexer = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["REDIS_CONFIG"]);

            AreaRegistration.RegisterAllAreas(); 
            GlobalConfiguration.Configure(WebApiConfig.Register);
            ConfigureDependencyResolver(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void ConfigureDependencyResolver(HttpConfiguration configuration)
        {
            var builder = new ContainerBuilder();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly())
                .PropertiesAutowired();

            builder.RegisterInstance(_redisConnectionMultiplexer).SingleInstance().AsSelf();

            configuration.DependencyResolver
                = new AutofacWebApiDependencyResolver(builder.Build());
        }
    }
}
