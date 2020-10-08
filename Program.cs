using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Quill
{
    /* 2020-10-05: notes here because there's not a better place for them right now.
     * 
     * this is a rework of the old code to ASP.NET Core 3.1 with a new build of Ink as well. at least on Windows, this is working well so far.
     * many things have been revised and reworked along the way. some are required (Ink has changed quite a bit since earlier versions) and other things
     * are improvements I noticed while working or have been meaning to do for a long time.
     * 
     * n.b., all the JSON error properties are CateErrors now. 
     * 
     * we got rid of the inklecate binaries! hurray! some things get simpler after this. upgrades should be a lot easier.
     * 
     * rethinking error handling so the js client can receive errors, warnings, and story as different properties would be good to do, but i'm out of steam for that for now.
     * 
     * on linux, "systemd" is quite a bit different than the previous "upstart". a few things that may be helpful if you need to mess with this:
     * the directory /etc/systemd/system and the quill.service file within it
     * the commands "systemctl stop quill.service", "systemctl stop quill.service", and (if you alter the service file) "systemctl daemon-reload".
     * 
     */

    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureLogging((ctx, builder) =>
                        {
                            builder.AddConfiguration(ctx.Configuration.GetSection("Logging"));
                            // while ContentRootPath is a confusing name, it's actually what we want. see further path config in appsettings.json.
                            builder.AddFile(o => o.RootPath = ctx.HostingEnvironment.ContentRootPath);
                        })
                        .UseStartup<Startup>();
                });
    }
}
