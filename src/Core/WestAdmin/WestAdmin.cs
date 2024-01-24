using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;
using Modularity;
using WestAdminApi;
using CounterStrikeSharp.API.Modules.Timers;
using cssAdminManager = CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;

namespace WestAdmin
{
    public class WestAdmin : BasePlugin, ICorePlugin
    {
        public override string ModuleName => "WestAdmin";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0";

        private static string? connectionString;
        private static readonly List<ConnectedAdmin> connectedAdmins = new();
        private readonly DateTime[] _playerPlayTime = new DateTime[65];

        public static readonly string ChatPrefix = $" {ChatColors.Red}WEST ADMIN | ";

        public static readonly char ADMFLAG_RESERVATION = 'a';
        public static readonly char ADMFLAG_GENERIC = 'b';
        public static readonly char ADMFLAG_KICK = 'c';
        public static readonly char ADMFLAG_BAN = 'd';
        public static readonly char ADMFLAG_UNBAN = 'e';
        public static readonly char ADMFLAG_SLAY = 'f';
        public static readonly char ADMFLAG_CHANGEMAP = 'g';
        public static readonly char ADMFLAG_CONVARS = 'h';
        public static readonly char ADMFLAG_CONFIG = 'i';
        public static readonly char ADMFLAG_CHAT = 'j';
        public static readonly char ADMFLAG_VOTE = 'k';
        public static readonly char ADMFLAG_PASSWORD = 'l';
        public static readonly char ADMFLAG_RCON = 'm';
        public static readonly char ADMFLAG_CHEATS = 'n';
        public static readonly char ADMFLAG_CUSTOM1 = 'o';
        public static readonly char ADMFLAG_CUSTOM2 = 'p';
        public static readonly char ADMFLAG_CUSTOM3 = 'q';
        public static readonly char ADMFLAG_CUSTOM4 = 'r';
        public static readonly char ADMFLAG_CUSTOM5 = 's';
        public static readonly char ADMFLAG_CUSTOM6 = 't';
        public static readonly char ADMFLAG_CUSTOM7 = 'u';
        public static readonly char ADMFLAG_CUSTOM8 = 'v';
        public static readonly char ADMFLAG_CUSTOM9 = 'w';
        public static readonly char ADMFLAG_CUSTOM10 = 'x';
        public static readonly char ADMFLAG_CUSTOM11 = 'y';
        public static readonly char ADMFLAG_ROOT = 'z';

        public static List<Admin>? Admins { get; private set; }

        public WestAdminApi? AdminApi { get; private set; }
        public static bool CoreLoaded { get; private set; }

        private List<string> GaggedSids = new();

        public override void Load(bool hotReload)
        {
            LoadCore(new PluginApis());
            RegisterListeners();
        }

        private void RegisterListeners()
        {
            RegisterListener<Listeners.OnClientDisconnectPost>(slot => _playerPlayTime[slot + 1] = DateTime.MinValue);
            AddCommandListener("say", OnSay);
            AddCommandListener("say_team", OnSay);
        }

        public void LoadCore(IApiRegisterer apiRegisterer)
        {
            AdminApi = new WestAdminApi();
            apiRegisterer.Register<IWestAdminApi>(AdminApi);

            ConnectToDatabase();
            InitializeDatabaseTables();
            Admins = LoadAdminsFromDatabase();
            CreateMenu();
            SetupTimers();
            CoreLoaded = true;
            Console.WriteLine($"{ChatPrefix}Ядро успешно загружено.");
        }

        private void ConnectToDatabase()
        {
            Config dbConfig = LoadConfig();
            connectionString = new MySqlConnectionStringBuilder
            {
                Database = dbConfig.WestAdminDB.Database,
                UserID = dbConfig.WestAdminDB.User,
                Password = dbConfig.WestAdminDB.Password,
                Server = dbConfig.WestAdminDB.Host,
                Port = dbConfig.WestAdminDB.Port,
            }.ToString();

            Console.WriteLine($"{ChatPrefix} Соединение с базой данных установлено");
        }

        private void InitializeDatabaseTables()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var tableQueries = new List<string>
                {
                    "CREATE TABLE IF NOT EXISTS `as_admins`(`id` INTEGER NOT NULL PRIMARY KEY AUTO_INCREMENT, `steamid` VARCHAR(32) NOT NULL, `name` VARCHAR(64) NOT NULL, `flags` VARCHAR(64) NOT NULL, `immunity` INTEGER NOT NULL, `end` INTEGER NOT NULL, `comment` VARCHAR(64) NOT NULL);",
                    "CREATE TABLE IF NOT EXISTS `as_bans`(`id` INTEGER NOT NULL PRIMARY KEY AUTO_INCREMENT, `admin_steamid` VARCHAR(32) NOT NULL, `steamid` VARCHAR(32) NOT NULL, `name` VARCHAR(64) NOT NULL, `admin_name` VARCHAR(64) NOT NULL, `created` INTEGER NOT NULL, `duration` INTEGER NOT NULL, `end` INTEGER NOT NULL, `reason` VARCHAR(64) NOT NULL); ",
                    "CREATE TABLE IF NOT EXISTS `as_gags`(`id` INTEGER NOT NULL PRIMARY KEY AUTO_INCREMENT, `admin_steamid` VARCHAR(32) NOT NULL, `steamid` VARCHAR(32) NOT NULL, `name` VARCHAR(64) NOT NULL, `admin_name` VARCHAR(64) NOT NULL, `created` INTEGER NOT NULL, `duration` INTEGER NOT NULL, `end` INTEGER NOT NULL, `reason` VARCHAR(64) NOT NULL);",
                    "CREATE TABLE IF NOT EXISTS `as_mutes`(`id` INTEGER NOT NULL PRIMARY KEY AUTO_INCREMENT,`admin_steamid` VARCHAR(32) NOT NULL, `steamid` VARCHAR(32) NOT NULL, `name` VARCHAR(64) NOT NULL, `admin_name` VARCHAR(64) NOT NULL, `created` INTEGER NOT NULL, `duration` INTEGER NOT NULL, `end` INTEGER NOT NULL, `reason` VARCHAR(64) NOT NULL); "
                };

                foreach (var query in tableQueries)
                {
                    using MySqlCommand command = new(query, connection);
                    command.ExecuteNonQuery();
                }
            }
            Console.WriteLine($"{ChatPrefix} Работа с таблицами базы данных завершена");
        }

        private void SetupTimers()
        {
            AddTimer(3, () =>
            {
                List<string> sids = GetListSids();
                Task.Run(() =>
                {
                    SetGaggedPlayers(sids);
                });
            }, TimerFlags.REPEAT);
            Console.WriteLine($" {ChatPrefix}Таймер успешно запущен");
        }

        public static List<Admin> LoadAdminsFromDatabase()
        {
            var admins = new List<Admin>();

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    var query = "SELECT * FROM `as_admins`";
                    using MySqlCommand command = new(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var admin = CreateAdminFromReader(reader);
                        admins.Add(admin);
                    }
                }

                LogAdminList(admins);
                CSSAdminCompatibility(admins);
            }
            catch (MySqlException ex)
            {
                Console.WriteLine($"{ChatPrefix} Ошибка при загрузке администраторов из базы данных: {ex.Message}");
            }

            return admins;
        }

        private static Admin CreateAdminFromReader(MySqlDataReader reader)
        {
            return new Admin
            {
                SteamID = reader.GetString("steamid"),
                Name = reader.GetString("name"),
                Flags = reader.GetString("flags"),
                Immunity = reader.GetInt32("immunity"),
                EndTime = reader.GetInt64("end"),
                Comment = reader.GetString("comment")
            };
        }

        private static void LogAdminList(List<Admin> admins)
        {
            if (admins.Any())
            {
                Console.WriteLine($"{ChatPrefix} Список полученных администраторов:");
                foreach (var admin in admins)
                {
                    Console.WriteLine($"SteamID={admin.SteamID}, Ник={admin.Name}, Флаги={admin.Flags}, Иммунитет={admin.Immunity}, Время окончания={admin.EndTime}, Комментарий={admin.Comment}");
                }
            }
            else
            {
                Console.WriteLine($"{ChatPrefix} В базе данных не найдено ни одного администратора.");
            }
        }

        public static void CSSAdminCompatibility(List<Admin> admins)
        {
            foreach (var admin in admins)
            {
                if (admin.Flags == null || admin.SteamID == null)
                {
                    continue;
                }

                var permissions = new List<string>();

                foreach (char flag in admin.Flags)
                {
                    switch (flag)
                    {
                        case 'a':
                            permissions.Add("@css/reservation");
                            break;
                        case 'b':
                            permissions.Add("@css/generic");
                            break;
                        case 'c':
                            permissions.Add("@css/kick");
                            break;
                        case 'd':
                            permissions.Add("@css/ban");
                            break;
                        case 'e':
                            permissions.Add("@css/unban");
                            break;
                        case 'f':
                            permissions.Add("@css/slay");
                            break;
                        case 'g':
                            permissions.Add("@css/changemap");
                            break;
                        case 'h':
                            permissions.Add("@css/cvar");
                            break;
                        case 'i':
                            permissions.Add("@css/config");
                            break;
                        case 'j':
                            permissions.Add("@css/chat");
                            break;
                        case 'k':
                            permissions.Add("@css/vote");
                            break;
                        case 'l':
                            permissions.Add("@css/password");
                            break;
                        case 'm':
                            permissions.Add("@css/rcon");
                            break;
                        case 'n':
                            permissions.Add("@css/cheats");
                            break;
                        case 'z':
                            permissions.Add("@css/root");
                            break;
                    }
                }

                SteamID steamID = new SteamID(UInt64.Parse(admin.SteamID));

                cssAdminManager.AdminManager.AddPlayerPermissions(steamID, permissions.ToArray());
                cssAdminManager.AdminManager.SetPlayerImmunity(steamID, (uint)admin.Immunity);

                Console.WriteLine($"{ChatPrefix} Выданы права: {string.Join(", ", permissions)} и установлен иммунитет {admin.Immunity} для администратора {admin.Name} (SteamID: {admin.SteamID})");
            }
        }

        private void CreateMenu()
        {
            AddCommand("css_admin", "admin menu", (player, info) =>
            {
                if (player == null) return;

                var adminInfo = GetAdminInfo(player);
                if (adminInfo == null || (!adminInfo.HasFlag(ADMFLAG_GENERIC) && !adminInfo.HasFlag(ADMFLAG_ROOT)))
                {
                    player.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                    return;
                }

                var adminMenu = new CenterHtmlMenu("Меню Администратора");
                AddPlayerControlOptions(adminMenu, adminInfo, player);
                AddServerControlOptions(adminMenu, adminInfo, player);
                AddVoteControlOptions(adminMenu, player);
                AddBlockControlOptions(adminMenu, adminInfo, player);

                MenuManager.OpenCenterHtmlMenu(this, player, adminMenu);
            });
        }

        private void AddPlayerControlOptions(CenterHtmlMenu menu, Admin adminInfo, CCSPlayerController player)
        {
            var playerControlMenu = new CenterHtmlMenu("Управление игроками");
            if (adminInfo.HasFlag(ADMFLAG_SLAY) || adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                playerControlMenu.AddMenuOption("Убить игрока", (p, o) =>
                {
                    if (adminInfo.HasFlag(ADMFLAG_SLAY) || adminInfo.HasFlag(ADMFLAG_ROOT))
                    {
                        var slayplayer = new CenterHtmlMenu("Убить игрока");
                        CreateSlayMenu(slayplayer, adminInfo);
                        MenuManager.OpenCenterHtmlMenu(this, player, slayplayer);
                    }
                    else
                    {
                        player.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }

            if (adminInfo.HasFlag(ADMFLAG_KICK) || adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                playerControlMenu.AddMenuOption("Кикнуть игрока", (p, o) =>
                {
                    if (adminInfo.HasFlag(ADMFLAG_KICK) || adminInfo.HasFlag(ADMFLAG_ROOT))
                    {
                        var kickplayer = new CenterHtmlMenu("Кикнуть игрока");
                        CreateKickMenu(kickplayer, adminInfo);
                        MenuManager.OpenCenterHtmlMenu(this, player, kickplayer);
                    }
                    else
                    {
                        player.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }
            if (adminInfo.HasFlag(ADMFLAG_GENERIC) || adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                playerControlMenu.AddMenuOption("Инфо об игроках", (player, option) =>
                {
                    if (player == null) return;

                    if (adminInfo != null && (adminInfo.HasFlag(ADMFLAG_GENERIC) || adminInfo.HasFlag(ADMFLAG_ROOT)))
                    {
                        var whoplayer = new CenterHtmlMenu("Информация об игроках");
                        CreateWhoMenu(whoplayer);
                        MenuManager.OpenCenterHtmlMenu(this, player, whoplayer);
                    }
                    else
                    {
                        player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }

            if (adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                playerControlMenu.AddMenuOption("Включить NoClip", (player, option) =>
                {
                    if (player == null) return;

                    if (adminInfo != null && adminInfo.HasFlag(ADMFLAG_ROOT))
                    {
                        var noclipplayer = new CenterHtmlMenu("Включить NoClip для");
                        CreateNoClipMenu(noclipplayer);
                        MenuManager.OpenCenterHtmlMenu(this, player, noclipplayer);
                    }
                    else
                    {
                        player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }
            menu.AddMenuOption("Управление игроками", (p, o) => MenuManager.OpenCenterHtmlMenu(this, player, playerControlMenu));
        }

        private void AddServerControlOptions(CenterHtmlMenu menu, Admin adminInfo, CCSPlayerController player)
        {
            var serverControlMenu = new CenterHtmlMenu("Управление сервером");
            if (adminInfo.HasFlag(ADMFLAG_CHANGEMAP) || adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                serverControlMenu.AddMenuOption("Смена карты", (player, option) =>
                {
                    if (player == null) return;

                    if (adminInfo != null && (adminInfo.HasFlag(ADMFLAG_CHANGEMAP) || adminInfo.HasFlag(ADMFLAG_ROOT)))
                    {
                        var changeMapMenu = new CenterHtmlMenu("Смена карты");

                        CreateChangeMapMenu(changeMapMenu);
                        MenuManager.OpenCenterHtmlMenu(this, player, changeMapMenu);
                    }
                    else
                    {
                        player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }

            if (adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                serverControlMenu.AddMenuOption("Обновить список администраторов", (player, option) =>
                {
                    if (player == null) return;

                    if (adminInfo != null && adminInfo.HasFlag(ADMFLAG_ROOT))
                    {
                        LoadAdminsFromDatabase();
                        player.PrintToChat("Кэш администраторов был обновлен");
                    }
                    else
                    {
                        player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                    }
                });
            }
            menu.AddMenuOption("Управление сервером", (p, o) => MenuManager.OpenCenterHtmlMenu(this, player, serverControlMenu));
        }

        private void AddVoteControlOptions(CenterHtmlMenu menu, CCSPlayerController player)
        {
            var voteControlMenu = new CenterHtmlMenu("Управление голосованиями");

            menu.AddMenuOption("Управление голосованиями", (p, o) => MenuManager.OpenCenterHtmlMenu(this, player, voteControlMenu));
        }

        private void AddBlockControlOptions(CenterHtmlMenu menu, Admin adminInfo, CCSPlayerController player)
        {
            var blockControlMenu = new CenterHtmlMenu("Управление блокировками");
            if (adminInfo.HasFlag(ADMFLAG_BAN) || adminInfo.HasFlag(ADMFLAG_CHAT) || adminInfo.HasFlag(ADMFLAG_ROOT))
            {
                if (adminInfo.HasFlag(ADMFLAG_BAN) || adminInfo.HasFlag(ADMFLAG_ROOT))
                {
                    blockControlMenu.AddMenuOption("Забанить игрока", (player, option) =>
                    {
                        if (player == null) return;

                        if (adminInfo != null && (adminInfo.HasFlag(ADMFLAG_BAN) || adminInfo.HasFlag(ADMFLAG_ROOT)))
                        {
                            var banplayer = new CenterHtmlMenu("Забанить игрока");
                            CreateBanPlayerMenu(banplayer, adminInfo);
                            MenuManager.OpenCenterHtmlMenu(this, player, banplayer);
                        }
                        else
                        {
                            player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                        }
                    });
                }

                if (adminInfo.HasFlag(ADMFLAG_CHAT) || adminInfo.HasFlag(ADMFLAG_ROOT))
                {
                    blockControlMenu.AddMenuOption("Отключить текстовый чат", (player, option) =>
                    {
                        if (player == null) return;

                        if (adminInfo != null && (adminInfo.HasFlag(ADMFLAG_BAN) || adminInfo.HasFlag(ADMFLAG_ROOT)))
                        {
                            var gagplayer = new CenterHtmlMenu("Отключить текстовый чат игроку");
                            CreateGagPlayerMenu(gagplayer, adminInfo);
                            MenuManager.OpenCenterHtmlMenu(this, player, gagplayer);
                        }
                        else
                        {
                            player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                        }
                    });
                }

                if (adminInfo.HasFlag(ADMFLAG_CHAT) || adminInfo.HasFlag(ADMFLAG_ROOT))
                {
                    blockControlMenu.AddMenuOption("Отключить голосовой чат", (player, option) =>
                    {
                        if (player == null) return;

                        if (adminInfo != null && (adminInfo.HasFlag(ADMFLAG_BAN) || adminInfo.HasFlag(ADMFLAG_ROOT)))
                        {
                            var muteplayer = new CenterHtmlMenu("Отключить голосовой чат игроку");
                            CreateMutePlayerMenu(muteplayer, adminInfo);
                            MenuManager.OpenCenterHtmlMenu(this, player, muteplayer);
                        }
                        else
                        {
                            player.PrintToChat($" {ChatPrefix}У вас нет доступа к этой команде.");
                        }
                    });
                }
                menu.AddMenuOption("Управление блокировками", (p, o) => MenuManager.OpenCenterHtmlMenu(this, player, blockControlMenu));
            }
        }

        private Admin? GetAdminInfo(CCSPlayerController player)
        {
            var steamID = player.SteamID.ToString();
            return Admins?.Find(a => a.SteamID == steamID);
        }

        private void CreateSlayMenu(CenterHtmlMenu slayplayer, Admin adminInfo)
        {
            var playerEntities = Utilities.GetPlayers();
            var hasValidPlayers = playerEntities.Any(player => CanAdminSlayPlayer(adminInfo, player));

            if (!hasValidPlayers)
            {
                slayplayer.AddMenuOption("Нет доступных игроков", (controller, option) => { });
                return;
            }

            foreach (var player in playerEntities)
            {
                if (!CanAdminSlayPlayer(adminInfo, player))
                {
                    continue;
                }

                var optionText = FormatPlayerOption(player);
                slayplayer.AddMenuOption(optionText, (controller, option) => SlayPlayer(controller, player));
            }
        }

        private static bool CanAdminSlayPlayer(Admin admin, CCSPlayerController targetPlayer)
        {
            if (!targetPlayer.PawnIsAlive)
            {
                return false;
            }

            if (targetPlayer.IsHLTV || !targetPlayer.Pawn.IsValid) return false;

            var targetAdmin = connectedAdmins.FirstOrDefault(a => a.SteamID == targetPlayer.SteamID.ToString());
            return targetAdmin == null || admin.Immunity >= targetAdmin.Immunity;
        }

        private void SlayPlayer(CCSPlayerController adminController, CCSPlayerController targetPlayer)
        {
            if (!targetPlayer.PawnIsAlive)
            {
                adminController.PrintToChat($"{ChatPrefix}Игрок уже мертв");
                return;
            }

            targetPlayer.PlayerPawn?.Value?.CommitSuicide(true, true);
            Server.PrintToChatAll($"{ChatPrefix}Администратор {ChatColors.Lime}{adminController.PlayerName}{ChatColors.Red} убил игрока {ChatColors.Lime}{targetPlayer.PlayerName}");
        }

        private void CreateKickMenu(CenterHtmlMenu kickplayer, Admin adminInfo)
        {
            var playerEntities = Utilities.GetPlayers();
            var hasValidPlayers = playerEntities.Any(player => CanAdminKickPlayer(adminInfo, player));

            if (!hasValidPlayers)
            {
                kickplayer.AddMenuOption("Нет доступных игроков", (controller, option) => { });
                return;
            }

            foreach (var player in playerEntities)
            {
                if (!CanAdminKickPlayer(adminInfo, player))
                {
                    continue;
                }

                var optionText = FormatPlayerOption(player);
                kickplayer.AddMenuOption(optionText, (controller, option) => KickPlayer(controller, player));
            }
        }

        private static bool CanAdminKickPlayer(Admin admin, CCSPlayerController targetPlayer)
        {
            if (targetPlayer.IsBot || targetPlayer.IsHLTV || !targetPlayer.Pawn.IsValid) return false;

            if (admin.SteamID == targetPlayer.SteamID.ToString()) return false;

            var targetAdmin = connectedAdmins.FirstOrDefault(a => a.SteamID == targetPlayer.SteamID.ToString());
            return targetAdmin == null || admin.Immunity >= targetAdmin.Immunity;
        }

        private void KickPlayer(CCSPlayerController adminController, CCSPlayerController targetPlayer)
        {
            var userId = NativeAPI.GetUseridFromIndex((int)targetPlayer.Index);
            Server.ExecuteCommand($"kickid {userId}");
            Server.PrintToChatAll($"{ChatPrefix}Администратор {ChatColors.Lime}{adminController.PlayerName}{ChatColors.Red} кикнул игрока {ChatColors.Lime}{targetPlayer.PlayerName}");
        }

        private void CreateWhoMenu(CenterHtmlMenu whoplayer)
        {
            var playerEntities = Utilities.GetPlayers();
            whoplayer.MenuOptions.Clear();

            foreach (var player in playerEntities)
            {
                var playerName = GetPlayerName(player);
                var adminStatus = GetAdminStatus(player);
                var playTime = GetPlayTime(player);

                whoplayer.AddMenuOption(playerName, (controller, option) =>
                {
                    var formattedOutput = FormatPlayerInfo(playerName, adminStatus, playTime);
                    controller.PrintToChat(formattedOutput);
                });
            }
        }

        private string GetPlayerName(CCSPlayerController player)
        {
            return !string.IsNullOrWhiteSpace(player.PlayerName) ? player.PlayerName : "Неизвестно";
        }

        private string GetAdminStatus(CCSPlayerController player)
        {
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == player.SteamID.ToString());
            return adminInfo != null ? "Админ" : "Игрок";
        }

        private TimeSpan GetPlayTime(CCSPlayerController player)
        {
            return DateTime.Now - _playerPlayTime[player.Index];
        }

        private string FormatPlayerInfo(string playerName, string adminStatus, TimeSpan playTime)
        {
            return $" {ChatPrefix}Ник: {ChatColors.Lime}{playerName} | {ChatColors.Red}Права: {ChatColors.Lime}{adminStatus} | {ChatColors.Red}Время на сервере: {ChatColors.Lime}{playTime.Hours:D2} часов, {playTime.Minutes:D2} минут, {playTime.Seconds:D2} секунд";
        }

        private void CreateNoClipMenu(CenterHtmlMenu noclipplayer)
        {
            var playerEntities = Utilities.GetPlayers();
            noclipplayer.MenuOptions.Clear();

            foreach (var player in playerEntities)
            {
                var playerName = GetPlayerName(player);
                noclipplayer.AddMenuOption(playerName, (controller, option) => ToggleNoClip(player));
            }
        }

        private void ToggleNoClip(CCSPlayerController player)
        {
            if (player?.PlayerPawn?.Value == null) return;

            var playerPawn = player.PlayerPawn.Value;
            playerPawn.MoveType = playerPawn.MoveType == MoveType_t.MOVETYPE_NOCLIP ? MoveType_t.MOVETYPE_WALK : MoveType_t.MOVETYPE_NOCLIP;

            Server.PrintToChatAll($"{ChatPrefix}Для игрока {ChatColors.Lime}{player.PlayerName}{ChatColors.Red} изменено значение NoClip");
        }

        private void CreateChangeMapMenu(CenterHtmlMenu changeMapMenu)
        {
            var maps = LoadMaps();
            if (!maps.Maplist.Any())
            {
                changeMapMenu.AddMenuOption("Нет доступных карт", (player, option) => player.PrintToChat($"{ChatPrefix}Список карт пуст."));
                return;
            }

            foreach (var mapEntry in maps.Maplist)
            {
                AddMapOptionToMenu(changeMapMenu, mapEntry);
            }
        }

        private void AddMapOptionToMenu(CenterHtmlMenu menu, MapEntry mapEntry)
        {
            var displayName = mapEntry.DisplayName ?? "Неизвестная карта";
            var command = GetChangeMapCommand(mapEntry);

            menu.AddMenuOption(displayName, (player, option) =>
            {
                Server.PrintToChatAll($"{ChatPrefix}Смена карты на {ChatColors.Lime}{displayName}");
                AddTimer(3.0f, () => Server.ExecuteCommand(command));
            });
        }

        private string GetChangeMapCommand(MapEntry mapEntry)
        {
            return mapEntry.IsWorkshop ? $"host_workshop_map {mapEntry.Name}" : $"map {mapEntry.Name}";
        }

        private void CreateBanPlayerMenu(CenterHtmlMenu banplayer, Admin adminInfo)
        {
            var playerEntities = Utilities.GetPlayers();
            var hasValidPlayers = playerEntities.Any(player => CanAdminBanPlayer(adminInfo, player));

            if (!hasValidPlayers)
            {
                banplayer.AddMenuOption("Нет доступных игроков", (controller, option) => { });
                return;
            }

            foreach (var player in playerEntities)
            {
                if (!CanAdminBanPlayer(adminInfo, player))
                {
                    continue;
                }

                var optionText = FormatPlayerOption(player);
                banplayer.AddMenuOption(optionText, (controller, option) => OpenBanReasonMenu(controller, (int)player.Index));
            }
        }

        private static bool CanAdminBanPlayer(Admin admin, CCSPlayerController targetPlayer)
        {
            if (targetPlayer.IsBot || targetPlayer.IsHLTV || !targetPlayer.Pawn.IsValid) return false;

            if (admin.SteamID == targetPlayer.SteamID.ToString()) return false;

            var targetAdmin = connectedAdmins.FirstOrDefault(a => a.SteamID == targetPlayer.SteamID.ToString());
            return targetAdmin == null || admin.Immunity >= targetAdmin.Immunity;
        }

        private string FormatPlayerOption(CCSPlayerController player)
        {
            return $"{player.PlayerName}";
        }

        private void OpenBanReasonMenu(CCSPlayerController controller, int targetIndex)
        {
            var banReasonMenu = new CenterHtmlMenu("Выберите причину бана");
            banReasonMenu.AddMenuOption("Читы", (ctrl, opt) => OpenBanDurationMenu(ctrl, targetIndex, "Читы"));
            banReasonMenu.AddMenuOption("Оскорбления", (ctrl, opt) => OpenBanDurationMenu(ctrl, targetIndex, "Оскорбления"));

            MenuManager.OpenCenterHtmlMenu(this, controller, banReasonMenu);
        }

        private void OpenBanDurationMenu(CCSPlayerController controller, int targetIndex, string reason)
        {
            var banDurationMenu = new CenterHtmlMenu("Выберите срок бана");
            banDurationMenu.AddMenuOption("5 минут", (ctrl, opt) => BanPlayer(ctrl, targetIndex, reason, 5 * 60));
            banDurationMenu.AddMenuOption("30 минут", (ctrl, opt) => BanPlayer(ctrl, targetIndex, reason, 30 * 60));

            MenuManager.OpenCenterHtmlMenu(this, controller, banDurationMenu);
        }

        private void BanPlayer(CCSPlayerController controller, int targetIndex, string reason, int duration)
        {
            var target = Utilities.GetPlayers().FirstOrDefault(player => player.Index == targetIndex);
            if (target == null)
            {
                Console.WriteLine("Целевой игрок не найден.");
                return;
            }

            try
            {
                AddBanToDatabase(controller, target, reason, duration);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Ошибка при добавлении бана в базу данных: {e}");
            }

            KickPlayer(target);
            AnnounceBan(controller, target, duration, reason);
        }

        private static void AddBanToDatabase(CCSPlayerController controller, CCSPlayerController target, string reason, int duration)
        {
            var startBanTimeUnix = DateTime.UtcNow.GetUnixEpoch();
            var endBanTimeUnix = duration == 0 ? 0 : DateTime.UtcNow.AddSeconds(duration).GetUnixEpoch();

            using var connection = new MySqlConnection(connectionString);
            var query = @"
        INSERT INTO `as_bans` (`admin_steamid`, `steamid`, `admin_name`, `name`, `created`, `duration`, `end`, `reason`)
        VALUES (@AdminSteamId, @SteamId, @Admname, @Username, @StartBanTime, @Length, @EndBanTime, @Reason);
    ";
            connection.Execute(query, new
            {
                AdminSteamId = controller?.SteamID.ToString() ?? "Консоль",
                Admname = controller?.PlayerName,
                Username = target.PlayerName,
                SteamId = target.SteamID.ToString(),
                Reason = reason,
                StartBanTime = startBanTimeUnix,
                EndBanTime = endBanTimeUnix,
                Length = duration,
            });
        }

        private void KickPlayer(CCSPlayerController target)
        {
            var userId = NativeAPI.GetUseridFromIndex((int)target.Index);
            Server.ExecuteCommand($"kickid {userId}");
        }

        private static void AnnounceBan(CCSPlayerController controller, CCSPlayerController target, int duration, string reason)
        {
            var banDurationInMinutes = duration / 60;
            Server.PrintToChatAll($"{ChatPrefix}Администратор {ChatColors.Lime}{controller?.PlayerName}{ChatColors.Red} забанил {ChatColors.Lime}{target.PlayerName}{ChatColors.Red} на {ChatColors.Lime}{banDurationInMinutes} минут {ChatColors.Red}по причине: {ChatColors.Lime}{reason}");
        }

        private void CreateGagPlayerMenu(CenterHtmlMenu gagplayer, Admin adminInfo)
        {
            var playerEntities = Utilities.GetPlayers();
            var hasValidPlayers = playerEntities.Any(player => ShouldAddGagOption(adminInfo, player));

            if (!hasValidPlayers)
            {
                gagplayer.AddMenuOption("Нет доступных игроков", (controller, option) => { });
                return;
            }

            foreach (var player in playerEntities)
            {
                if (!ShouldAddGagOption(adminInfo, player))
                {
                    continue;
                }

                var optionText = FormatPlayerOption(player);
                gagplayer.AddMenuOption(optionText, (controller, option) => OpenGagReasonMenu(controller, (int)player.Index));
            }
        }

        private bool ShouldAddGagOption(Admin admin, CCSPlayerController targetPlayer)
        {
            if (targetPlayer.IsBot || targetPlayer.IsHLTV || !targetPlayer.Pawn.IsValid) return false;

            if (admin.SteamID == targetPlayer.SteamID.ToString()) return false;

            var targetAdmin = connectedAdmins.FirstOrDefault(a => a.SteamID == targetPlayer.SteamID.ToString());
            return targetAdmin == null || admin.Immunity >= targetAdmin.Immunity;
        }

        private void OpenGagReasonMenu(CCSPlayerController controller, int targetIndex)
        {
            var gagReasonMenu = new CenterHtmlMenu("Выберите причину отключения текстового чата");
            gagReasonMenu.AddMenuOption("Оскорбления", (ctrl, opt) => OpenGagDurationMenu(ctrl, targetIndex, "Оскорбления"));

            MenuManager.OpenCenterHtmlMenu(this, controller, gagReasonMenu);
        }

        private void OpenGagDurationMenu(CCSPlayerController controller, int targetIndex, string reason)
        {
            var gagDurationMenu = new CenterHtmlMenu("Выберите срок отключения текстового чата");
            gagDurationMenu.AddMenuOption("5 минут", (ctrl, opt) => GagPlayer(ctrl, targetIndex, reason, 5 * 60));
            gagDurationMenu.AddMenuOption("30 минут", (ctrl, opt) => GagPlayer(ctrl, targetIndex, reason, 30 * 60));

            MenuManager.OpenCenterHtmlMenu(this, controller, gagDurationMenu);
        }

        private void GagPlayer(CCSPlayerController controller, int targetIndex, string reason, int duration)
        {
            var target = Utilities.GetPlayers().FirstOrDefault(player => player.Index == targetIndex);
            if (target == null)
            {
                Console.WriteLine("Целевой игрок не найден.");
                return;
            }

            try
            {
                AddGagToDatabase(controller, target, reason, duration);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Ошибка при добавлении гага в базу данных: {e}");
            }

            AnnounceGag(controller, target, duration, reason);
        }

        private static void AddGagToDatabase(CCSPlayerController controller, CCSPlayerController target, string reason, int duration)
        {
            var startGagTimeUnix = DateTime.UtcNow.GetUnixEpoch();
            var endGagTimeUnix = duration == 0 ? 0 : DateTime.UtcNow.AddSeconds(duration).GetUnixEpoch();

            using var connection = new MySqlConnection(connectionString);
            var query = @"
        INSERT INTO `as_gags` (`admin_steamid`, `steamid`, `admin_name`, `name`, `created`, `duration`, `end`, `reason`)
        VALUES (@AdminSteamId, @SteamId, @Admname, @Username, @StartGagTime, @Length, @EndGagTime, @Reason);
    ";
            connection.Execute(query, new
            {
                AdminSteamId = controller?.SteamID.ToString() ?? "Консоль",
                Admname = controller?.PlayerName,
                Username = target.PlayerName,
                SteamId = target.SteamID.ToString(),
                Reason = reason,
                StartGagTime = startGagTimeUnix,
                EndGagTime = endGagTimeUnix,
                Length = duration,
            });
        }

        private static void AnnounceGag(CCSPlayerController controller, CCSPlayerController target, int duration, string reason)
        {
            var gagDurationInMinutes = duration / 60;
            Server.PrintToChatAll($"{ChatPrefix}{ChatColors.Lime}{controller?.PlayerName}{ChatColors.Red} отключил текстовый чат {ChatColors.Lime}{target.PlayerName}{ChatColors.Red} на {ChatColors.Lime}{gagDurationInMinutes} минут {ChatColors.Red}по причине: {ChatColors.Lime}{reason}");
        }

        private void CreateMutePlayerMenu(CenterHtmlMenu muteplayer, Admin adminInfo)
        {
            var playerEntities = Utilities.GetPlayers();
            var hasValidPlayers = playerEntities.Any(player => ShouldAddMuteOption(adminInfo, player));

            if (!hasValidPlayers)
            {
                muteplayer.AddMenuOption("Нет доступных игроков", (controller, option) => { });
                return;
            }

            foreach (var player in playerEntities)
            {
                if (!ShouldAddMuteOption(adminInfo, player))
                {
                    continue;
                }

                var optionText = FormatPlayerOption(player);
                muteplayer.AddMenuOption(optionText, (controller, option) => OpenMuteReasonMenu(controller, (int)player.Index));
            }
        }

        private bool ShouldAddMuteOption(Admin admin, CCSPlayerController targetPlayer)
        {
            if (targetPlayer.IsBot || targetPlayer.IsHLTV || !targetPlayer.Pawn.IsValid) return false;

            if (admin.SteamID == targetPlayer.SteamID.ToString()) return false;

            var targetAdmin = connectedAdmins.FirstOrDefault(a => a.SteamID == targetPlayer.SteamID.ToString());
            return targetAdmin == null || admin.Immunity >= targetAdmin.Immunity;
        }

        private void OpenMuteReasonMenu(CCSPlayerController controller, int targetIndex)
        {
            var gagReasonMenu = new CenterHtmlMenu("Выберите причину отключения голосового чата");
            gagReasonMenu.AddMenuOption("Оскорбления", (ctrl, opt) => OpenMuteDurationMenu(ctrl, targetIndex, "Оскорбления"));

            MenuManager.OpenCenterHtmlMenu(this, controller, gagReasonMenu);
        }

        private void OpenMuteDurationMenu(CCSPlayerController controller, int targetIndex, string reason)
        {
            var gagDurationMenu = new CenterHtmlMenu("Выберите срок отключения голосового чата");
            gagDurationMenu.AddMenuOption("5 минут", (ctrl, opt) => MutePlayer(ctrl, targetIndex, reason, 5 * 60));
            gagDurationMenu.AddMenuOption("30 минут", (ctrl, opt) => MutePlayer(ctrl, targetIndex, reason, 30 * 60));

            MenuManager.OpenCenterHtmlMenu(this, controller, gagDurationMenu);
        }

        private void MutePlayer(CCSPlayerController controller, int targetIndex, string reason, int duration)
        {
            var target = Utilities.GetPlayers().FirstOrDefault(player => player.Index == targetIndex);
            if (target == null)
            {
                Console.WriteLine("Целевой игрок не найден.");
                return;
            }

            try
            {
                AddMuteToDatabase(controller, target, reason, duration);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Ошибка при добавлении мута в базу данных: {e}");
            }

            AnnounceMute(controller, target, duration, reason);
        }

        private static void AddMuteToDatabase(CCSPlayerController controller, CCSPlayerController target, string reason, int duration)
        {
            var startMuteTimeUnix = DateTime.UtcNow.GetUnixEpoch();
            var endMuteTimeUnix = duration == 0 ? 0 : DateTime.UtcNow.AddSeconds(duration).GetUnixEpoch();

            using var connection = new MySqlConnection(connectionString);
            var query = @"
        INSERT INTO `as_mutes` (`admin_steamid`, `steamid`, `admin_name`, `name`, `created`, `duration`, `end`, `reason`)
        VALUES (@AdminSteamId, @SteamId, @Admname, @Username, @StartMuteTime, @Length, @EndMuteTime, @Reason);
    ";
            connection.Execute(query, new
            {
                AdminSteamId = controller?.SteamID.ToString() ?? "Консоль",
                Admname = controller?.PlayerName,
                Username = target.PlayerName,
                SteamId = target.SteamID.ToString(),
                Reason = reason,
                StartMuteTime = startMuteTimeUnix,
                EndMuteTime = endMuteTimeUnix,
                Length = duration,
            });
        }

        private static void AnnounceMute(CCSPlayerController controller, CCSPlayerController target, int duration, string reason)
        {
            var gagDurationInMinutes = duration / 60;
            Server.PrintToChatAll($"{ChatPrefix}{ChatColors.Lime}{controller?.PlayerName}{ChatColors.Red} отключил голосовой чат {ChatColors.Lime}{target.PlayerName}{ChatColors.Red} на {ChatColors.Lime}{gagDurationInMinutes} минут {ChatColors.Red}по причине: {ChatColors.Lime}{reason}");
        }

        [GameEventHandler]
        public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || !@event.Userid.IsValid)
            {
                return HookResult.Continue;
            }

            CheckAndKickBannedPlayer(@event.Userid);
            return HookResult.Continue;
        }

        private void CheckAndKickBannedPlayer(CCSPlayerController player)
        {
            Task.Run(async () =>
            {
                if (await PlayerIsBanned(player.SteamID.ToString()))
                {
                    Server.NextFrame(() => KickPlayer(player.UserId));
                }
            });
        }

        private void KickPlayer(int? userId)
        {
            if (userId.HasValue)
            {
                NativeAPI.IssueServerCommand($"kickid {userId.Value}");
            }
        }


        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            UpdateGaggedPlayersStatus();
            return HookResult.Continue;
        }

        private void UpdateGaggedPlayersStatus()
        {
            var sids = GetListSids();
            SetGaggedPlayers(sids);
        }

        public async Task<bool> PlayerIsBanned(string sid)
        {
            try
            {
                using MySqlConnection connection = new(connectionString);
                await connection.OpenAsync();
                string sql = "SELECT COUNT(1) FROM as_bans WHERE steamid = @sid AND (end > @currentTime OR duration = 0)";
                var comm = new MySqlCommand(sql, connection);
                comm.Parameters.AddWithValue("@sid", sid);
                comm.Parameters.AddWithValue("@currentTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                var result = await comm.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (MySqlException ex)
            {
                LogDatabaseError(ex);
                return false;
            }
        }

        private void LogDatabaseError(Exception ex)
        {
            Console.WriteLine($" {ChatPrefix}Ошибка базы данных: {ex.Message}");
        }

        public bool PlayerIsGaged(string sid)
        {
            try
            {
                using MySqlConnection connection = new(connectionString);
                connection.Open();
                string sql = "SELECT COUNT(1) FROM as_gags WHERE steamid = @sid AND (end > @currentTime OR duration = 0)";
                var comm = new MySqlCommand(sql, connection);
                comm.Parameters.AddWithValue("@sid", sid);
                comm.Parameters.AddWithValue("@currentTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                var result = comm.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
            catch (MySqlException ex)
            {
                LogDatabaseError(ex);
                return false;
            }
        }

        [ConsoleCommand("css_reloadadmins")]
        public void ReloadAdmins(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null)
            {
                if (IsRootAdmin(player))
                {
                    Admins = LoadAdminsFromDatabase();
                    CSSAdminCompatibility(Admins);
                    player.PrintToChat("Кэш администраторов был обновлен");
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                }
            }
        }

        public static bool IsRootAdmin(CCSPlayerController player)
        {
            var steamID = player.SteamID.ToString();
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == steamID);
            return adminInfo != null && adminInfo.HasFlag(ADMFLAG_ROOT);
        }

        [ConsoleCommand("css_hsay")]
        public void Hsay(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithChatPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string message = ExtractMessageFromCommand(command);
            if (!string.IsNullOrWhiteSpace(message))
            {
                VirtualFunctions.ClientPrintAll(HudDestination.Alert, message, 0, 0, 0, 0);
            }
        }

        private bool IsAdminWithChatPermission(CCSPlayerController player)
        {
            var steamID = player.SteamID.ToString();
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == steamID);
            return adminInfo != null && (adminInfo.HasFlag(ADMFLAG_CHAT) || adminInfo.HasFlag(ADMFLAG_ROOT));
        }

        private static string ExtractMessageFromCommand(CommandInfo command)
        {
            int spaceIndex = command.GetCommandString.IndexOf(' ');
            return spaceIndex != -1 ? command.GetCommandString[spaceIndex..].Trim() : string.Empty;
        }

        [ConsoleCommand("css_csay")]
        public void Csay(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithChatPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string message = ExtractMessageFromCommand(command);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Utilities.GetPlayers().ForEach(controller => controller.PrintToCenter(message));
            }
        }

        [ConsoleCommand("css_chat")]
        public void Chat(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithChatPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string message = ExtractMessageFromCommand(command);
            if (!string.IsNullOrWhiteSpace(message))
            {
                BroadcastAdminChat(player, message);
            }
        }

        private static void BroadcastAdminChat(CCSPlayerController sender, string message)
        {
            foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV))
            {
                p.PrintToChat($"{ChatPrefix}Админ-чат | {sender.PlayerName}: {message}");
            }
        }

        [ConsoleCommand("css_rcon")]
        public void Rcon(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithRconPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string rconCommand = ExtractArgumentFromCommand(command);
            if (!string.IsNullOrWhiteSpace(rconCommand))
            {
                ExecuteRconCommand(rconCommand);
            }
        }

        private bool IsAdminWithRconPermission(CCSPlayerController player)
        {
            var steamID = player.SteamID.ToString();
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == steamID);
            return adminInfo != null && (adminInfo.HasFlag(ADMFLAG_RCON) || adminInfo.HasFlag(ADMFLAG_ROOT));
        }

        private static string ExtractArgumentFromCommand(CommandInfo command)
        {
            int spaceIndex = command.GetCommandString.IndexOf(' ');
            return spaceIndex != -1 ? command.GetCommandString[spaceIndex..].Trim() : string.Empty;
        }

        private static void ExecuteRconCommand(string rconCommand)
        {
            Server.ExecuteCommand(rconCommand);
        }

        [ConsoleCommand("css_cvar")]
        public void Cvar(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithRconPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string cvarName = command.GetArg(1);
            var cvar = FindConVar(cvarName);
            if (cvar == null)
            {
                command.ReplyToCommand($"{ChatPrefix}Cvar \"{cvarName}\" не найден.");
                return;
            }

            string value = command.GetArg(2);
            if (!string.IsNullOrEmpty(value))
            {
                SetConVarValue(player, cvar, value);
            }
            else
            {
                DisplayConVarValue(player, cvar);
            }
        }

        private static ConVar? FindConVar(string name)
        {
            return ConVar.Find(name);
        }

        private void SetConVarValue(CCSPlayerController player, ConVar cvar, string value)
        {
            if (cvar.Name.Equals("sv_cheats") && !HasCheatsPermission(player))
            {
                player.PrintToChat($"{ChatPrefix}У вас нет доступа к изменению \"{cvar.Name}\".");
                return;
            }

            Server.ExecuteCommand($"{cvar.Name} {value}");
            player.PrintToChat($"{ChatPrefix}{player.PlayerName} изменил {cvar.Name} на {value}.");
        }

        private bool HasCheatsPermission(CCSPlayerController player)
        {
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == player.SteamID.ToString());
            return adminInfo?.HasFlag(ADMFLAG_CHEATS) ?? false;
        }

        private static void DisplayConVarValue(CCSPlayerController player, ConVar cvar)
        {
            string conVarValue = cvar.Type switch
            {
                ConVarType.Bool => cvar.GetPrimitiveValue<bool>().ToString(),
                ConVarType.Float32 or ConVarType.Float64 => cvar.GetPrimitiveValue<float>().ToString(),
                ConVarType.UInt16 => cvar.GetPrimitiveValue<ushort>().ToString(),
                ConVarType.Int16 => cvar.GetPrimitiveValue<short>().ToString(),
                ConVarType.UInt32 => cvar.GetPrimitiveValue<uint>().ToString(),
                ConVarType.Int32 => cvar.GetPrimitiveValue<int>().ToString(),
                ConVarType.Int64 => cvar.GetPrimitiveValue<long>().ToString(),
                ConVarType.UInt64 => cvar.GetPrimitiveValue<ulong>().ToString(),
                ConVarType.String => cvar.StringValue,
                _ => "Неизвестный тип",
            };
            player.PrintToChat($"{ChatPrefix}Значение {cvar.Name} = {conVarValue}.");
        }

        [ConsoleCommand("css_exec")]
        public void Exec(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null || !IsAdminWithConfigPermission(player))
            {
                player?.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                return;
            }

            string configName = ExtractArgumentFromCommand(command);
            if (!string.IsNullOrWhiteSpace(configName))
            {
                ExecuteConfigFile(configName);
            }
        }

        private bool IsAdminWithConfigPermission(CCSPlayerController player)
        {
            var steamID = player.SteamID.ToString();
            var adminInfo = Admins?.FirstOrDefault(a => a.SteamID == steamID);
            return adminInfo != null && (adminInfo.HasFlag(ADMFLAG_CONFIG) || adminInfo.HasFlag(ADMFLAG_ROOT));
        }

        private static void ExecuteConfigFile(string configName)
        {
            Server.ExecuteCommand($"exec {configName}");
        }

        private Config LoadConfig()
        {
            string configPath = GetConfigFilePath();
            if (!File.Exists(configPath))
            {
                return CreateConfig(configPath);
            }

            try
            {
                string configFileContent = File.ReadAllText(configPath);
                Config? config = JsonSerializer.Deserialize<Config>(configFileContent);
                return config ?? throw new InvalidOperationException("Не удалось десериализовать конфигурационный файл.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Ошибка при чтении конфигурации: {ex.Message}");
            }
        }

        private string GetConfigFilePath()
        {
            string moduleDirectory = GetModuleDirectory();
            return Path.Combine(moduleDirectory, "configs", "mysql.json");
        }

        private string GetModuleDirectory()
        {
            var moduleDirectoryParent = Directory.GetParent(ModuleDirectory) ?? throw new InvalidOperationException($"{ChatPrefix} Каталог модуля равен null");
            var parentDirectory = moduleDirectoryParent.Parent;
            return parentDirectory == null
                ? throw new InvalidOperationException($"{ChatPrefix} Родительский каталог равен null")
                : parentDirectory.FullName;
        }

        private static Config CreateConfig(string configPath)
        {
            var config = GenerateDefaultConfig();

            try
            {
                SaveConfigToFile(configPath, config);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Ошибка при записи в файл конфигурации: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Нет доступа для записи файла конфигурации: {ex.Message}");
            }

            return config;
        }

        private static Config GenerateDefaultConfig()
        {
            return new Config
            {
                WestAdminDB = new WestAdminDB(
                    host: "",
                    database: "",
                    user: "",
                    password: "",
                    port: 3306
                )
            };
        }

        private static void SaveConfigToFile(string configPath, Config config)
        {
            string configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, configJson);
        }

        public HookResult OnSay(CCSPlayerController? controller, CommandInfo info)
        {
            if (controller == null || string.IsNullOrWhiteSpace(info.GetArg(1)))
            {
                return HookResult.Continue;
            }

            string message = info.GetArg(1);
            if (IsGagged(controller.SteamID.ToString()) && !IsCommand(message))
            {
                return HookResult.Stop;
            }

            return HookResult.Continue;
        }

        private bool IsGagged(string steamID)
        {
            return GaggedSids.Contains(steamID);
        }

        private bool IsCommand(string message)
        {
            return message.StartsWith("!") || message.StartsWith("/");
        }

        public List<string> GetListSids()
        {
            return Utilities.GetPlayers()
                            .Where(p => p.IsValid && !p.IsBot)
                            .Select(p => p.SteamID.ToString())
                            .ToList();
        }

        public void SetGaggedPlayers(List<string> sids)
        {
            GaggedSids = sids.Where(sid => PlayerIsGaged(sid)).ToList();
        }

        public class Config
        {
            public WestAdminDB WestAdminDB { get; set; } = new WestAdminDB();
        }

        public class WestAdminDB
        {
            public WestAdminDB(string host = "", string database = "", string user = "", string password = "", uint port = 3306)
            {
                Host = host;
                Database = database;
                User = user;
                Password = password;
                Port = port;
            }

            public string Host { get; }
            public string Database { get; }
            public string User { get; }
            public string Password { get; }
            public uint Port { get; }
        }

        public interface IAdmin
        {
            bool HasFlag(char requiredFlag);
        }

        public class Admin : IAdmin
        {
            public string? SteamID { get; set; }
            public string? Name { get; set; }
            public string? Flags { get; set; }
            public int Immunity { get; set; }
            public long EndTime { get; set; }
            public string? Comment { get; set; }

            public bool HasFlag(char requiredFlag) => Flags?.IndexOf(requiredFlag) >= 0;
        }

        public class ConnectedAdmin : IAdmin
        {
            public string? SteamID { get; set; }
            public string? Name { get; set; }
            public string? Flags { get; set; }
            public int Immunity { get; set; }
            public long EndTime { get; set; }
            public string? Comment { get; set; }

            public bool HasFlag(char requiredFlag) => Flags?.IndexOf(requiredFlag) >= 0;
        }

        private Maps LoadMaps()
        {
            var maplistPath = GetMaplistPath();
            if (!File.Exists(maplistPath)) return CreateMaplist(maplistPath);

            try
            {
                var mapsJson = File.ReadAllText(maplistPath);
                return JsonSerializer.Deserialize<Maps>(mapsJson) ?? new Maps();
            }
            catch (JsonException)
            {
                return new Maps();
            }
        }

        private string GetMaplistPath()
        {
            var moduleDirectory = GetModuleDirectory();
            return Path.Combine(moduleDirectory, "configs", "maps.json");
        }

        private static Maps CreateMaplist(string maplistPath)
        {
            var maps = new Maps();
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(maplistPath, JsonSerializer.Serialize(maps, options));
            return maps;
        }

        public class Maps
        {
            public List<MapEntry> Maplist { get; set; } = new List<MapEntry>();
        }

        public class MapEntry
        {
            public string? Name { get; set; }
            public string? DisplayName { get; set; }
            public bool IsWorkshop { get; set; }
        }

        public class WestAdminApi : IWestAdminApi
        {
            public bool IsCoreLoaded() => CoreLoaded;

            public string GetChatPrefix() => ChatPrefix;

            private Admin? GetAdminInfo(CCSPlayerController player) =>
                player != null ? Admins?.Find(a => a.SteamID == player.SteamID.ToString()) : null;

            public bool IsPlayerAdmin(CCSPlayerController player) =>
                GetAdminInfo(player) != null;

            public char GetHighestAdminPermission(CCSPlayerController player)
            {
                var adminInfo = GetAdminInfo(player);
                if (adminInfo == null) return '0';

                var flags = new[] { ADMFLAG_RESERVATION, ADMFLAG_GENERIC, ADMFLAG_KICK, ADMFLAG_BAN,
                     ADMFLAG_UNBAN, ADMFLAG_SLAY, ADMFLAG_CHANGEMAP, ADMFLAG_CONVARS,
                     ADMFLAG_CONFIG, ADMFLAG_CHAT, ADMFLAG_VOTE, ADMFLAG_PASSWORD,
                     ADMFLAG_RCON, ADMFLAG_CHEATS, ADMFLAG_ROOT, ADMFLAG_CUSTOM1,
                     ADMFLAG_CUSTOM2, ADMFLAG_CUSTOM3, ADMFLAG_CUSTOM4, ADMFLAG_CUSTOM5,
                     ADMFLAG_CUSTOM6};

                return flags.FirstOrDefault(flag => adminInfo.HasFlag(flag), '0');
            }

            public int GetAdminImmunity(CCSPlayerController player) =>
                GetAdminInfo(player)?.Immunity ?? 0;

            public string GetAdminSteamID(CCSPlayerController player) =>
                GetAdminInfo(player)?.SteamID ?? "";

            public string GetAdminName(CCSPlayerController player) =>
                GetAdminInfo(player)?.Name ?? "";

            public long GetAdminEndTime(CCSPlayerController player) =>
                GetAdminInfo(player)?.EndTime ?? 0;

            public string GetAdminComment(CCSPlayerController player) =>
                GetAdminInfo(player)?.Comment ?? "";

            private bool QueryDatabaseForPlayerStatus(string sid, string table)
            {
                try
                {
                    using var connection = new MySqlConnection(connectionString);
                    connection.Open();
                    string sql = $"SELECT * FROM {table} WHERE steamid='{sid}' AND (end>{DateTimeOffset.UtcNow.ToUnixTimeSeconds()} OR duration=0)";
                    return new MySqlCommand(sql, connection).ExecuteScalarAsync() != null;
                }
                catch (MySqlException ex)
                {
                    Console.WriteLine($" {ChatPrefix}Ошибка базы данных: {ex}");
                    return false;
                }
            }

            public bool IsPlayerBanned(string sid) => QueryDatabaseForPlayerStatus(sid, "as_bans");

            public bool IsPlayerGagged(string sid) => QueryDatabaseForPlayerStatus(sid, "as_gags");

            public void ReloadAdmins(CCSPlayerController? player)
            {
                if (player == null || IsRootAdmin(player))
                {
                    LoadAdminsFromDatabase();
                    player?.PrintToChat("Кэш администраторов был обновлен");
                }
                else
                {
                    player.PrintToChat($"{ChatPrefix}У вас нет доступа к этой команде.");
                }
            }
        }
    }
}
public static class DateTimeExtensions
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}