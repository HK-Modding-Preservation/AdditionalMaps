﻿using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using System.IO;
using UnityEngine;

namespace AdditionalMaps.Consts
{
    public class LanguageStrings
    {
        #region Language Strings
        // Places
        public const string PathOfPain_Key = "PathOfPainArea";
        public const string Workshop_Key = "WorkshopArea";
        public const string Credits_Key = "CreditsArea";
        #endregion

        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> jsonDict;

        public LanguageStrings()
        {
            Assembly _asm = Assembly.GetExecutingAssembly();
            using (Stream s = _asm.GetManifestResourceStream("AdditionalMaps.Resources.Language.json"))
            {
                if (s != null)
                {
                    byte[] buffer = new byte[s.Length];
                    s.Read(buffer, 0, buffer.Length);
                    s.Dispose();

                    string json = System.Text.Encoding.Default.GetString(buffer);

                    jsonDict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(json);
                    Modding.Logger.Log("[AdditionalMaps.Consts.LanguageStrings] - Loaded Language");
                }
            }
        }

        public string Get(string key, string sheet)
        {
            GlobalEnums.SupportedLanguages lang = GameManager.instance.gameSettings.gameLanguage;
            try
            {
                return jsonDict[lang.ToString()][sheet][key].Replace("<br>", "\n");
            }
            catch
            {
                return jsonDict[GlobalEnums.SupportedLanguages.EN.ToString()][sheet][key].Replace("<br>", "\n");
            }
        }

        public bool ContainsKey(string key, string sheet)
        {
            try
            {
                GlobalEnums.SupportedLanguages lang = GameManager.instance.gameSettings.gameLanguage;
                try
                {
                    return jsonDict[lang.ToString()][sheet].ContainsKey(key);
                }
                catch
                {
                    try
                    {
                        return jsonDict[GlobalEnums.SupportedLanguages.EN.ToString()][sheet].ContainsKey(key);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
