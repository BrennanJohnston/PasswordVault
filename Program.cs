using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TextCopy;
using SecurityDriven.Inferno;

namespace PasswordVault {
    class Program {
        static Dictionary<String, Dictionary<String, String>> AccountDictionary = new Dictionary<String, Dictionary<String, String>>();
        const String AccountFileName = "vault";
        static readonly String CurrentDir = Directory.GetCurrentDirectory();
        static readonly String FileFullPath = CurrentDir + "//" + AccountFileName;
        static bool running = true;
        static bool key_set = false;
        const int PW_GEN_LEN = 18;
        static readonly Random random = new Random();
        static byte[] encryption_key;

        static void Main(string[] args) {
            IOManager.SetPrefix("pwvault");

            IOManager.WriteLineNoPrefix("Welcome to Password Vault.");

            IOManager.WriteLineNoPrefix("Please type your encryption key now. If no keys has ever been set, make a new one. Do not forget this key.");
            SetNewEncryptionKey();

            //IOManager.WriteLineNoPrefix("Encryption key set successfully.");
            IOManager.WriteLineNoPrefix("Use command 'help' for command list.");
            ListAccounts();

            while(running) {
                IOManager.IOMessage _input = IOManager.GetInput();
                String _flag = _input.GetFlag();
                String[] _args = _input.GetArguments();
                switch(_flag.ToLower()) {
                    case "exit":
                        running = false;
                        break;

                    case "enterkey":
                        /*
                        if (_args.Length > 0) {
                            encryption_key = System.Text.Encoding.ASCII.GetBytes(_args[0]);
                            key_set = true;
                            IOManager.WriteLineNoPrefix("Encryption key has been set. Ready to get and set passwords.");
                        } else IOManager.WriteLineNoPrefix("No key provided.");
                        */
                        IOManager.WriteLineNoPrefix("Please type the encryption key for your vault.");
                        SetNewEncryptionKey();
                        break;

                    case "setkey":
                        //IOManager.WriteLineNoPrefix("Please enter a new encryption key for your vault.");
                        ChangeVaultEncryptionKey();
                        break;

                    case "help":
                        IOManager.WriteLineNoPrefix("exit - Closes the program.");
                        IOManager.WriteLineNoPrefix("enterkey - Enter your encryption key.\n" +
                                                    "           Required prior to setting or getting passwords.");
                        IOManager.WriteLineNoPrefix("setkey - Walks you through setting a new encryption key for your vault.");
                        //IOManager.WriteLineNoPrefix("addservice [servicename] - Adds a service to your service list.\n" +
                        //                            "                           Examples: addservice google, addservice yahoo, addservice battle.net");
                        IOManager.WriteLineNoPrefix("add [servicename] [username] [password] - Adds an account to your vault.\n" +
                                                    "               Also use this command to set a new password for an existing account.\n" +
                                                    "               Type 'random' as the password for a randomly generated password.\n" +
                                                    "               Make sure to update the associated account with the new password. (Copies to clipboard when generated)\n" +
                                                    "               Example: addaccount google myemail@gmail.com random");
                        IOManager.WriteLineNoPrefix("remove [servicename] [username] - Removes an account from your vault.");
                        IOManager.WriteLineNoPrefix("get - Walks you through retrieving a password and copies it to your clipboard.");
                        IOManager.WriteLineNoPrefix("list - Lists all registered accounts.");
                        IOManager.WriteLineNoPrefix("reset - Deletes your vault file and generates a new one.\n" +
                                                    "        Prompts to set a new encryption key for your new vault.");
                        break;

                    case "add":
                        bool _add_success = AddAccount(_args);
                        if(_add_success) { //service, username, and password were provided

                            IOManager.WriteLineNoPrefix("Account added successfully. Password copied to clipboard.");
                        } else {
                            IOManager.WriteLineNoPrefix("Please provide service, username, and password.");
                        }
                        break;

                    case "remove":
                        bool _remv_success = RemoveAccount(_args);
                        if(_remv_success) {
                            IOManager.WriteLineNoPrefix("Account removed successfully.");
                        } else {
                            IOManager.WriteLineNoPrefix("Account removal failed. Provide both service name and username of the account, and check spelling.");
                        }
                        break;

                    case "get":
                        GetPasswordUI();
                        break;
                    case "list":
                        ListAccounts();
                        break;
                    case "reset":
                        IOManager.WriteLineNoPrefix("Vault has been deleted.");
                        ResetVault();
                        break;
                }
            }
        }

        public static void ResetVault() {
            File.Delete(FileFullPath);
            IOManager.WriteLineNoPrefix("Please type an encryption key for your new vault. Do not lose this key.");
            SetNewEncryptionKey();
        }

        public static void ChangeVaultEncryptionKey() {
            bool _new_key_valid = false;
            String _new_key = "";
            IOManager.WriteLineNoPrefix("Please provide new encryption key for your vault. Do not forget this key. ('cancel' to abort)");
            while(!_new_key_valid) {
                IOManager.IOMessage _msg = IOManager.GetInputHidden();
                String _tmp_key = _msg.GetFlag();
                if(_tmp_key.ToLower().Equals("cancel")) {
                    IOManager.WriteLineNoPrefix("Change key aborted. No changes made.");
                    break;
                }
                if(_tmp_key.Length > 0) {
                    _new_key = _tmp_key;
                    _new_key_valid = true;
                } else {
                    IOManager.WriteLineNoPrefix("Key cannot be blank.");
                }
            }

            if (_new_key_valid) {
                LoadAccountData();
                encryption_key = StringToBytes(_new_key);
                SaveAccountData();

                IOManager.WriteLineNoPrefix("Vault encryption key has been changed.\n" +
                                            "Previous encryption key will no longer decrypt your vault.");
            }
        }

        public static void SetNewEncryptionKey() {
            key_set = false;
            IOManager.WriteLineNoPrefix("Type 'cancel' to abort encryption key set.");
            while (!key_set) {
                IOManager.IOMessage _msg = IOManager.GetInputHidden();//IOManager.GetInput();
                if (_msg.GetFlag().Length > 0) {
                    if(_msg.GetFlag().ToLower().Equals("cancel")) {
                        IOManager.WriteLineNoPrefix("Set encryption key aborted.");
                        encryption_key = new byte[0];
                        break;
                    }

                    encryption_key = StringToBytes(_msg.GetFlag());

                    bool successful = LoadAccountData();
                    if (successful) {
                        key_set = true;
                        //ListAccounts();
                        ClearDictionary();
                        IOManager.WriteLineNoPrefix("Encryption key set successfully.");
                        /*
                        IOManager.WriteLineNoPrefix("The following information was decrypted, is it readable? (y/n)");
                        ListAccounts();
                        _msg = IOManager.GetInput();
                        if (_msg.GetFlag().ToLower().Equals("y")) {
                            key_set = true;
                        } else {
                            IOManager.WriteLineNoPrefix("Make sure to check your encryption key and try again.");
                        }
                        */
                    }
                    //clear key from console
                } else IOManager.WriteLineNoPrefix("No input provided.");
            }
        }

        public static void ListAccounts() {
            LoadAccountData();
            IOManager.WriteLineNoPrefix("Registered Accounts - ");
            int _service_counter = 1;
            foreach(String _service in AccountDictionary.Keys) {
                IOManager.WriteLineNoPrefix("   " + _service_counter + ". " + _service + ":");
                int _user_counter = 1;
                foreach(String _user in AccountDictionary[_service].Keys) {
                    IOManager.WriteLineNoPrefix("      -" + _user);
                        _user_counter++;
                }
                _service_counter++;
            }

            ClearDictionary();
        }

        private static void GetPasswordUI() {
            LoadAccountData();
            bool got_service = false;
            bool got_user = false;
            IOManager.WriteLineNoPrefix("Please select the service associated with the account. ('cancel' at any time to cancel)");
            int counter = 1;
            Dictionary<int, String> _selection_dictionary = new Dictionary<int, String>();
            foreach(String _service in AccountDictionary.Keys) {
                IOManager.WriteLineNoPrefix("   " + counter + ". " + _service);
                _selection_dictionary.Add(counter, _service);
                counter++;
            }
            while (!got_service) {
                IOManager.IOMessage _input = IOManager.GetInput();
                try {
                    if(_input.GetFlag().ToLower().Equals("cancel")) {
                        IOManager.WriteLineNoPrefix("Aborting.");
                        got_service = true;
                        break;
                    }
                    int _selection_int = int.Parse(_input.GetFlag());
                    if (_selection_dictionary.ContainsKey(_selection_int)) {
                        String _selected_service = _selection_dictionary[_selection_int];
                        got_service = true;
                        counter = 1;
                        Dictionary<String, String> _user_dictionary = AccountDictionary[_selection_dictionary[_selection_int]];
                        _selection_dictionary.Clear();
                        IOManager.WriteLineNoPrefix("Please select username to get associated password.");
                        foreach (String _username in _user_dictionary.Keys) {
                            IOManager.WriteLineNoPrefix("   " + counter + ". " + _username);
                            _selection_dictionary.Add(counter, _username);
                            counter++;
                        }
                        while (!got_user) {
                            _input = IOManager.GetInput();
                            if(_input.GetFlag().ToLower().Equals("cancel")) {
                                IOManager.WriteLineNoPrefix("Aborting.");
                                got_user = true;
                                break;
                            }
                            try {
                                _selection_int = int.Parse(_input.GetFlag());
                                if (_selection_dictionary.ContainsKey(_selection_int)) {
                                    got_user = true;
                                    String _pw = _user_dictionary[_selection_dictionary[_selection_int]];
                                    //IOManager.WriteLineNoPrefix("Password for " + _selection_dictionary[_selection_int] + " is " + _pw);
                                    IOManager.WriteLineNoPrefix("Password for " + _selected_service + " account " + _selection_dictionary[_selection_int] + " copied to clipboard.");
                                    ClipboardService.SetText(_pw);
                                } else {
                                    IOManager.WriteLineNoPrefix("Selection invalid.");
                                }
                            } catch (FormatException _e1) {
                                IOManager.WriteLineNoPrefix("Please type the number associated.");
                            }
                        }

                    } else {
                        IOManager.WriteLineNoPrefix("Selection invalid.");
                    }
                } catch (FormatException _e2) {
                    IOManager.WriteLineNoPrefix("Please type the number associated.");
                }
            }

            ClearDictionary();
        }

        private static bool RemoveAccount(String[] _args) {
            bool successful = false;
            if(_args.Length == 2) {
                LoadAccountData();
                String _service = _args[0];
                String _username = _args[1];
                if(AccountDictionary.ContainsKey(_service)) {
                    Dictionary<String, String> _service_dictionary = AccountDictionary[_service];
                    if(_service_dictionary.ContainsKey(_username)) {
                        _service_dictionary.Remove(_username);
                        if(_service_dictionary.Count <= 0) {
                            AccountDictionary.Remove(_service);
                        }

                        SaveAccountData();
                        successful = true;
                    }
                }
            }

            ClearDictionary();
            return successful;
        }

        private static bool AddAccount(String[] _args) {
            bool successful = false;
            if (_args.Length == 3) {
                LoadAccountData();
                String _service = _args[0];
                String _username = _args[1];
                String _pw = _args[2];
                if (_pw.ToLower().Equals("random")) {
                    _pw = GeneratePassword();
                    ClipboardService.SetText(_pw);
                }
                if (!AccountDictionary.ContainsKey(_service)) {
                    AccountDictionary.Add(_service, new Dictionary<String, String>());
                }
                if (AccountDictionary[_service].ContainsKey(_username)) {
                    AccountDictionary[_service][_username] = _pw;
                } else {
                    AccountDictionary[_service].Add(_username, _pw);
                }
                SaveAccountData();
                successful = true;
            }

            return successful;
        }

        private static bool LoadAccountData() {
            byte[] bytes_read;
            bool successful = false;

            try {
                AccountDictionary.Clear();
                //FileStream _fs = File.OpenRead(FileFullPath);
                bytes_read = File.ReadAllBytes(FileFullPath);
                byte[] bytes_read_decrypted = SuiteB.Decrypt(encryption_key, bytes_read, null);
                if(bytes_read.Length > 0) {
                    AccountDictionary = JsonSerializer.Deserialize<Dictionary<String, Dictionary<String, String>>>(bytes_read_decrypted);
                }

                successful = true;
                //IOManager.WriteLineNoPrefix("LoadAccountData: String in file - " + BytesToString(bytes_read));

                //String _str = BytesToString(bytes_read);
                //if(_str.Length > 0)
                //AccountDictionary = JsonSerializer.Deserialize<Dictionary<String, Dictionary<String, String>>>(_str);

            } catch (FileNotFoundException _e1) {
                File.WriteAllBytes(FileFullPath, StringToBytes(""));
                IOManager.WriteLineNoPrefix("No vault file found, created one.");
                successful = true;
            } catch (JsonException _e2) {
                IOManager.WriteLineNoPrefix("Decryption failed, please check your encryption key.");
            }

            return successful;
        }

        private static void SaveAccountData() {
            String _json_str = JsonSerializer.Serialize(AccountDictionary);
            byte[] _json_bytes = StringToBytes(_json_str);
            //FileStream _fs = File.OpenWrite(FileFullPath);
            byte[] _json_bytes_encrypted = SuiteB.Encrypt(encryption_key, _json_bytes, null);

            File.WriteAllBytes(FileFullPath, _json_bytes_encrypted);
            ClearDictionary();
        }

        private static void ClearDictionary() {
            AccountDictionary.Clear();
        }

        public static String BytesToString(byte[] _bytes) {
            return System.Text.Encoding.UTF8.GetString(_bytes);
        }

        public static byte[] StringToBytes(String _s) {
            return System.Text.Encoding.UTF8.GetBytes(_s);
        }

        public static String GeneratePassword() {
            String _pw = "";
            for(int i = 0; i < PW_GEN_LEN; i++) {
                int _char_num_numbers = (int)((random.NextDouble() * 10) + 48);
                int _char_num_capitals = (int)((random.NextDouble() * 26) + 65);
                int _char_num_lowers = (int)((random.NextDouble() * 26) + 97);
                int _actual_char_num = 0;
                double _dice = random.NextDouble();
                if (_dice < 0.15625) //mAgIc nUmBeRs!!!1!
                    _actual_char_num = _char_num_numbers;
                else if (_dice < 0.578125) //MaGiC NuMbErS!1!!!
                    _actual_char_num = _char_num_capitals;
                else
                    _actual_char_num = _char_num_lowers;
                char _c = (char)_actual_char_num;

                _pw += _c;
            }

            //ClipboardService.SetText(_pw);

            return _pw;
        }
    }

    static class IOManager {
        private static String prefix = "";

        public static void WriteLineNoPrefix(String _s) {
            Console.WriteLine(_s);
        }

        public static void WriteLine(String _s) {
            StartLine();
            Console.WriteLine(_s);
        }

        public static IOMessage GetInputHidden() {
            StartLine();
            String _total_input = "";
            while(true) {
                ConsoleKeyInfo _key = Console.ReadKey(true);
                if(_key.Key == ConsoleKey.Enter) {
                    break;
                }
                _total_input += _key.KeyChar;
            }

            IOMessage _msg = new IOMessage(_total_input);
            Console.WriteLine();
            return _msg;
        }

        public static IOMessage GetInput() {
            StartLine();
            IOMessage _msg = new IOMessage(Console.ReadLine());
            return _msg;
        }
        private static String GetPrefix() {
            return prefix + "> ";
        }

        public static void SetPrefix(String _p) {
            prefix = _p;
        }

        private static void StartLine() {
            Console.Write(GetPrefix());
        }

        public class IOMessage {
            private String _flag;
            private String[] _args;
            public IOMessage(String _input) {
                String[] _input_split = _input.Split(" ");
                if (_input_split.Length > 0) {
                    _flag = _input_split[0];
                } else _flag = "";
                if (_input_split.Length > 1) {
                    _args = new String[_input_split.Length-1];
                    Array.Copy(_input_split, 1, _args, 0, _input_split.Length - 1);
                } else _args = new String[0];
            }

            public String GetFlag() {
                return _flag;
            }

            public String[] GetArguments() {
                return _args;
            }

            public int GetArgCount() {
                return _args.Length;
            }

            public void PrintMessageInfo() {
                Console.WriteLine("MESSAGE INFO");
                Console.WriteLine("Flag: " + _flag);
                Console.Write("Arguments: ");
                foreach(String _s in _args) {
                    Console.Write(_s + " ");
                }
                Console.WriteLine();
            }
        }
    }
}