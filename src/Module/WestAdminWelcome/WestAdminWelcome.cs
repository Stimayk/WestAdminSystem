using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Modularity;
using WestAdminApi;

namespace WestAdminWelcome
{
    public class WestAdminWelcome : BasePlugin, IModulePlugin
    {
        public override string ModuleName => "WestAdminWelcome";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "E!N";

        private IApiProvider? _apiProvider;

        public void LoadModule(IApiProvider provider)
        {
            if (provider.Get<IWestAdminApi>().IsCoreLoaded())
            {
                _apiProvider = provider;
                Server.PrintToConsole($"WEST ADMIN | API для {ModuleName} было инициализировано.");
            }
            else
            {
                Server.PrintToConsole($"WEST ADMIN | API для {ModuleName} не было инициализировано.");
            }
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (_apiProvider != null)
            {
                if (_apiProvider.Get<IWestAdminApi>().IsPlayerAdmin(@event.Userid))
                {
                    @event.Userid.PrintToChat($"{_apiProvider.Get<IWestAdminApi>().GetChatPrefix()}Вы являетесь администратором!");
                }
            }
            return HookResult.Continue;
        }
    }
}
