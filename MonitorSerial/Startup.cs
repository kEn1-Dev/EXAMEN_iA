using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(MonitorSerial.Startup))]
namespace MonitorSerial
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
