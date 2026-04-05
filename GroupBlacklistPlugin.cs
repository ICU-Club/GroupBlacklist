using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace GroupBlacklistPlugin
{
    [ApiVersion(2, 1)]
    public class GroupBlacklistPlugin : TerrariaPlugin
    {
        public override string Name => "GroupBlacklist";
        public override string Author => "星梦";
        public override string Description => "禁止指定用户组进入服务器并在在线时踢出";
        public override Version Version => new Version(1, 0, 0, 0);

        private static BlacklistConfig Config;
        private DateTime _lastCheck = DateTime.MinValue;

        public GroupBlacklistPlugin(Main game) : base(game)
        {
            Order = 1;
        }

        public override void Initialize()
        {
            Config = BlacklistConfig.Load();

            PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
            
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
            
            GeneralHooks.ReloadEvent += OnReload;

            TShock.Log.Info("[GroupBlacklist] 插件已加载");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnPlayerPostLogin(PlayerPostLoginEventArgs e)
        {
            var player = e.Player;
            if (player == null || !player.IsLoggedIn) return;

            if (Config.ExemptPlayers.Contains(player.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (Config.Settings.LogActions)
                    TShock.Log.Info($"[GroupBlacklist] 豁免玩家 {player.Name} 已跳过检查");
                return;
            }

            if (IsBlacklistedGroup(player.Group.Name))
            {
                player.Disconnect(Config.Settings.KickMessage);
                
                if (Config.Settings.LogActions)
                    TShock.Log.Warn($"[GroupBlacklist] 已阻止黑名单组玩家 {player.Name} (组: {player.Group.Name}) 进入服务器");
            }
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (!Config.Settings.KickOnlineBlacklist) return;
            
            if ((DateTime.Now - _lastCheck).TotalSeconds >= Config.Settings.CheckInterval)
            {
                _lastCheck = DateTime.Now;
                CheckOnlinePlayers();
            }
        }

        private void CheckOnlinePlayers()
        {
            foreach (var player in TShock.Players)
            {
                if (player == null || !player.Active || !player.IsLoggedIn) continue;

                // 豁免检查
                if (Config.ExemptPlayers.Contains(player.Name, StringComparer.OrdinalIgnoreCase))
                    continue;

                // 黑名单检查
                if (IsBlacklistedGroup(player.Group.Name))
                {
                    player.Kick(Config.Settings.InGameKickMessage, true, true);
                    
                    if (Config.Settings.LogActions)
                        TShock.Log.Warn($"[GroupBlacklist] 已踢出在线黑名单组玩家 {player.Name} (组: {player.Group.Name})");
                }
            }
        }
        
        private void OnReload(ReloadEventArgs args)
        {
            Config = BlacklistConfig.Reload();
            TShock.Log.Info("[GroupBlacklist] 配置已重载");
            args.Player?.SendSuccessMessage("[GroupBlacklist] 黑名单配置已重载");
        }

        private bool IsBlacklistedGroup(string groupName)
        {
            return Config.BlacklistedGroups.Contains(groupName, StringComparer.OrdinalIgnoreCase);
        }

        public static BlacklistConfig GetConfig() => Config;
    }
}
