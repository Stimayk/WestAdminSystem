using CounterStrikeSharp.API.Core;

namespace WestAdminApi
{
    /// <summary>
    /// Интерфейс для API администрирования WestAdmin.
    /// </summary>
    public interface IWestAdminApi
    {
        /// <summary>
        /// Проверяет, загружено ли ядро WestAdmin.
        /// </summary>
        bool IsCoreLoaded();

        /// <summary>
        /// Возвращает префикс чата WestAdmin.
        /// </summary>
        string GetChatPrefix();

        /// <summary>
        /// Проверяет, является ли игрок администратором.
        /// </summary>
        bool IsPlayerAdmin(CCSPlayerController player);

        /// <summary>
        /// Получает самое высокое разрешение администратора для игрока.
        /// </summary>
        char GetHighestAdminPermission(CCSPlayerController player);

        /// <summary>
        /// Получает уровень иммунитета администратора игрока.
        /// </summary>
        int GetAdminImmunity(CCSPlayerController player);

        /// <summary>
        /// Получает SteamID администратора игрока.
        /// </summary>
        string GetAdminSteamID(CCSPlayerController player);

        /// <summary>
        /// Получает имя администратора игрока.
        /// </summary>
        string GetAdminName(CCSPlayerController player);

        /// <summary>
        /// Получает время окончания полномочий администратора.
        /// </summary>
        long GetAdminEndTime(CCSPlayerController player);

        /// <summary>
        /// Получает комментарий администратора игрока.
        /// </summary>
        string GetAdminComment(CCSPlayerController player);

        /// <summary>
        /// Перезагружает список администраторов.
        /// </summary>
        void ReloadAdmins(CCSPlayerController? player);

        /// <summary>
        /// Проверяет, забанен ли игрок по его SteamID.
        /// </summary>
        bool IsPlayerBanned(string sid);

        /// <summary>
        /// Проверяет, заглушен ли игрок (гаг) по его SteamID.
        /// </summary>
        bool IsPlayerGagged(string sid);
    }
}
