using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonsNShops
{
    internal sealed class Localization
    {
        [Serializable] private sealed class Table { public Entry[] entries = Array.Empty<Entry>(); }
        [Serializable] private sealed class Entry { public string key = "", ru = "", en = ""; }

        private readonly Dictionary<string, Entry> entries = new();
        public bool Russian { get; private set; } = PlayerPrefs.GetInt("prototype.ru", 1) == 1;

        public Localization()
        {
            TextAsset asset = Resources.Load<TextAsset>("PrototypeStrings");
            foreach (Entry entry in JsonUtility.FromJson<Table>(asset.text).entries)
                entries.Add(entry.key, entry);
        }

        public string Text(string key, params object[] values)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string value = entries.TryGetValue(key, out Entry entry) ? Russian ? entry.ru : entry.en : key;
            return values.Length == 0 ? value : string.Format(value, values);
        }

        public string ItemName(int item) => Text(Catalog.Valid(item) ? "item." + item : "hud.empty");

        public void Toggle()
        {
            Russian = !Russian;
            PlayerPrefs.SetInt("prototype.ru", Russian ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
