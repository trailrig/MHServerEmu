﻿using System.Text.RegularExpressions;
using Gazillion;
using MHServerEmu.Core.Config;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.DatabaseAccess;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.PlayerManagement.Configs;

namespace MHServerEmu.PlayerManagement
{
    /// <summary>
    /// Provides <see cref="DBAccount"/> management functions.
    /// </summary>
    public static class AccountManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        private static readonly string DefaultAccountFilePath = Path.Combine(FileHelper.DataDirectory, "DefaultPlayer.json");

        public static IDBManager DBManager { get; private set; }
        public static DBAccount DefaultAccount { get; private set; }

        /// <summary>
        /// Initializes <see cref="AccountManager"/>.
        /// </summary>
        public static bool Initialize(IDBManager dbManager)
        {
            // Initialize default account if BypassAuth is enabled
            if (ConfigManager.Instance.GetConfig<PlayerManagerConfig>().BypassAuth)
            {
                bool defaultAccountLoaded = false;

                if (File.Exists(DefaultAccountFilePath))
                {
                    try
                    {
                        var defaultAccount = FileHelper.DeserializeJson<DBAccount>(DefaultAccountFilePath);
                        DefaultAccount = defaultAccount;
                        defaultAccountLoaded = true;
                    }
                    catch
                    {
                        Logger.Warn($"Incompatible default player data, resetting");
                    }
                }

                if (defaultAccountLoaded == false)
                {
                    // Initialize default account from config
                    var config = ConfigManager.Instance.GetConfig<DefaultPlayerDataConfig>();
                    DefaultAccount = config.InitializeDefaultAccount();
                }
            }

            DBManager = dbManager;
            return DBManager.Initialize();
        }

        /// <summary>
        /// Saves the default <see cref="DBAccount"/> to a JSON file.
        /// </summary>
        public static void SaveDefaultAccount()
        {
            FileHelper.SerializeJson(DefaultAccountFilePath, DefaultAccount);
        }

        /// <summary>
        /// Queries a <see cref="DBAccount"/> using the provided <see cref="LoginDataPB"/> instance.
        /// <see cref="AuthStatusCode"/> indicates the outcome of the query.
        /// </summary>
        public static AuthStatusCode TryGetAccountByLoginDataPB(LoginDataPB loginDataPB, out DBAccount account)
        {
            account = null;

            // Try to query an account to check
            string email = loginDataPB.EmailAddress.ToLower();
            if (DBManager.TryQueryAccountByEmail(email, out DBAccount accountToCheck) == false)
                return AuthStatusCode.IncorrectUsernameOrPassword403;

            // Check the account we queried
            if (CryptographyHelper.VerifyPassword(loginDataPB.Password, accountToCheck.PasswordHash, accountToCheck.Salt) == false)
                return AuthStatusCode.IncorrectUsernameOrPassword403;

            if (accountToCheck.IsBanned) return AuthStatusCode.AccountBanned;
            if (accountToCheck.IsArchived) return AuthStatusCode.AccountArchived;
            if (accountToCheck.IsPasswordExpired) return AuthStatusCode.PasswordExpired;

            // Output the account and return success if everything is okay
            account = accountToCheck;
            return AuthStatusCode.Success;
        }

        /// <summary>
        /// Queries a <see cref="DBAccount"/> using the provided email. Returns <see langword="true"/> if successful.
        /// </summary>
        public static bool TryGetAccountByEmail(string email, out DBAccount account) => DBManager.TryQueryAccountByEmail(email, out account);

        /// <summary>
        /// Creates a new <see cref="DBAccount"/> and inserts it into the database. Returns <see langword="true"/> if successful.
        /// </summary>
        public static (bool, string) CreateAccount(string email, string playerName, string password)
        {
            // Validate input before doing database queries
            if (ValidateEmail(email) == false)
                return (false, "Failed to create account: email must not be longer than 320 characters.");

            if (ValidatePlayerName(playerName) == false)
                return (false, "Failed to create account: names may contain only up to 16 alphanumeric characters.");

            if (ValidatePassword(password) == false)
                return (false, "Failed to create account: password must between 3 and 64 characters long.");

            if (DBManager.TryQueryAccountByEmail(email, out _))
                return (false, $"Failed to create account: email {email} is already used by another account.");

            if (DBManager.QueryIsPlayerNameTaken(playerName))
                return (false, $"Failed to create account: name {playerName} is already used by another account.");

            // Create a new account and insert it into the database
            DBAccount account = new(email, playerName, password);

            if (DBManager.InsertAccount(account) == false)
                return (false, "Failed to create account: database error.");

            return (true, $"Created a new account: {email} ({playerName}).");
        }

        // TODO (bool, string) ChangeAccountEmail(string oldEmail, string newEmail)

        /// <summary>
        /// Changes the player name of the <see cref="DBAccount"/> with the specified email. Returns <see langword="true"/> if successful.
        /// </summary>
        public static (bool, string) ChangeAccountPlayerName(string email, string playerName)
        {
            // Validate input before doing database queries
            if (ValidatePlayerName(playerName) == false)
                return (false, "Failed to change player name: names may contain only up to 16 alphanumeric characters.");

            if (DBManager.TryQueryAccountByEmail(email, out DBAccount account) == false)
                return (false, $"Failed to change player name: account {email} not found.");

            if (DBManager.QueryIsPlayerNameTaken(playerName))
                return (false, $"Failed to change player name: name {playerName} is already used by another account.");

            // Write the new name to the database
            account.PlayerName = playerName;
            DBManager.UpdateAccount(account);
            return (true, $"Successfully changed player name for account {email} to {playerName}.");
        }

        /// <summary>
        /// Changes the password of the <see cref="DBAccount"/> with the specified email. Returns <see langword="true"/> if successful.
        /// </summary>
        public static (bool, string) ChangeAccountPassword(string email, string newPassword)
        {
            // Validate input before doing database queries
            if (ValidatePassword(newPassword) == false)
                return (false, "Failed to change password: password must between 3 and 64 characters long.");

            if (DBManager.TryQueryAccountByEmail(email, out DBAccount account) == false)
                return (false, $"Failed to change password: account {email} not found.");

            // Update the password and write the new hash/salt to the database
            account.PasswordHash = CryptographyHelper.HashPassword(newPassword, out byte[] salt);
            account.Salt = salt;
            account.IsPasswordExpired = false;
            DBManager.UpdateAccount(account);
            return (true, $"Successfully changed password for account {email}.");
        }

        /// <summary>
        /// Changes the <see cref="AccountUserLevel"/> of the <see cref="DBAccount"/> with the specified email. Returns <see langword="true"/> if successful.
        /// </summary>
        public static (bool, string) SetAccountUserLevel(string email, AccountUserLevel userLevel)
        {
            // Make sure the specified account exists
            if (DBManager.TryQueryAccountByEmail(email, out DBAccount account) == false)
                return (false, $"Failed to set user level: account {email} not found.");

            // Write the new user level to the database
            account.UserLevel = userLevel;
            DBManager.UpdateAccount(account);
            return (true, $"Successfully set user level for account {email} to {userLevel}.");
        }

        // Ban and unban are separate methods to make sure we don't accidentally ban or unban someone we didn't intend to.

        /// <summary>
        /// Bans the <see cref="DBAccount"/> with the specified email.
        /// </summary>
        public static (bool, string) BanAccount(string email)
        {
            // Checks to make sure we can ban the specified account
            if (DBManager.TryQueryAccountByEmail(email, out DBAccount account) == false)
                return (false, $"Failed to ban: account {email} not found.");

            if (account.IsBanned)
                return (false, $"Failed to ban: account {email} is already banned.");

            // Write the ban to the database
            account.IsBanned = true;
            DBManager.UpdateAccount(account);
            return (true, $"Successfully banned account {email}.");
        }

        /// <summary>
        /// Unbans the <see cref="DBAccount"/> with the specified email.
        /// </summary>
        public static (bool, string) UnbanAccount(string email)
        {
            // Checks to make sure we can ban the specified account
            if (DBManager.TryQueryAccountByEmail(email, out DBAccount account) == false)
                return (false, $"Failed to unban: account {email} not found.");

            if (account.IsBanned == false)
                return (false, $"Failed to unban: account {email} is not banned.");

            // Write the unban to the database
            account.IsBanned = false;
            DBManager.UpdateAccount(account);
            return (true, $"Successfully unbanned account {email}.");
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided email <see cref="string"/> is valid.
        /// </summary>
        private static bool ValidateEmail(string email)
        {
            return email.Length.IsWithin(1, 320);  // todo: add regex for email
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided player name <see cref="string"/> is valid.
        /// </summary>
        private static bool ValidatePlayerName(string playerName)
        {
            return Regex.Match(playerName, "^[a-zA-Z0-9]{1,16}$").Success;    // 1-16 alphanumeric characters
        }
        
        /// <summary>
        /// Returns <see langword="true"/> if the provided password <see cref="string"/> is valid.
        /// </summary>
        private static bool ValidatePassword(string password)
        {
            return password.Length.IsWithin(3, 64);
        }
    }
}
