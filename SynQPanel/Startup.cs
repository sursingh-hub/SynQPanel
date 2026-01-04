//using Microsoft.AspNetCore.Builder;
//using Microsoft.Extensions.DependencyInjection;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace SynQPanel
//{
//    public class Startup
//    {
//        public void ConfigureServices(IServiceCollection services)
//        {
//            // Add any necessary services here
//        }

//        public void Configure(IApplicationBuilder app)
//        {

//            app.UseRouting();
//            app.UseEndpoints(endpoints =>
//            {
//                endpoints.MapGet("/", async context =>
//                {
//                    await context.Response.WriteAsync("Hello World!");
//                });
//            });
//        }
//    }
//}

using System.ComponentModel;
using System.Windows;

namespace SynQPanel
{
    internal static class DesignModeHelper
    {
        private static bool? _isInDesignMode;

        public static bool IsInDesignMode =>
            _isInDesignMode ??= DesignerProperties.GetIsInDesignMode(new DependencyObject());
    }
}
