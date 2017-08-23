using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace QuillNetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //MWCTODO: published app can't find views when running "dotnet Quill.dll".
            // this is a common problem, but so far solutions involve project.json which doesn't apply here.

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                // many people say using .Assembly is required for them--but app won't run at all with it for us.
                //.UseContentRoot(System.Reflection.Assembly.GetEntryAssembly().Location)
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
