using Humanizer;
using NHunspell;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SpellMaster
{
    internal class Speller : IDisposable
    {
        private static readonly Lazy<Speller> _default = new Lazy<Speller>(() => new Speller());
        private static readonly Regex SPLIT_REGEX = new Regex("([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))", RegexOptions.Compiled);
        private static string[] EMPTY_STRING_ARRAY = new string[0];
        private readonly Hunspell _s;

        private Speller()
        {
            Hunspell.NativeDllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _s = new Hunspell(Resources.en_US_aff, Encoding.Default.GetBytes(Resources.en_US_dic));
        }

        internal static Speller Default => _default.Value;

        public void Dispose()
        {
            if (!_s.IsDisposed)
                _s.Dispose();
        }

        internal string GetReplacement(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;

            var result = string.Empty;

            foreach (var w in SplitString(word))
            {
                result += GetSuggestion(w);
            }

            if (char.IsUpper(word[0]))
                return result.Pascalize();
            else
                return result.Camelize();
        }

        internal bool HasMisspelling(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            if (word.Length < 4) return false;

            foreach (var w in SplitString(word))
            {
                if (!_s.Spell(w)) return true;
            }
            return false;
        }

        private static string[] SplitString(string word)
        {
            if (string.IsNullOrEmpty(word))
                return EMPTY_STRING_ARRAY;

            return SPLIT_REGEX.Replace(word, "$1 ").Trim().Split(' ');
        }

        private string GetSuggestion(string w)
        {
            var suggestions = _s.Suggest(w);
            var bestPick = suggestions.Where(s => !s.Contains(' ') && !s.Contains('-')).FirstOrDefault();
            return bestPick;
        }
    }
}