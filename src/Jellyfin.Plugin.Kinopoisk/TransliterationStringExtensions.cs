// Copyright: https://calmsen.ru/transliteraciya-na-c
// Copyright: http://usanov.net/748-transliteraciya-rus-2-lat-na-c

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Kinopoisk
{
    public static class TransliterationStringExtensions
    {
        //ISO 9-95
        private static Dictionary<string, string> _replaceDictionary = new()
        {
                { "Є", "Ye" },
                { "І", "I" },
                { "Ѓ", "G" },
                { "і", "i" },
                { "№", "#" },
                { "є", "ye" },
                { "ѓ", "g" },
                { "А", "A" },
                { "Б", "B" },
                { "В", "V" },
                { "Г", "G" },
                { "Д", "D" },
                { "Е", "E" },
                { "Ё", "Yo" },
                { "Ж", "Zh" },
                { "З", "Z" },
                { "И", "I" },
                { "Й", "J" },
                { "К", "K" },
                { "Л", "L" },
                { "М", "M" },
                { "Н", "N" },
                { "О", "O" },
                { "П", "P" },
                { "Р", "R" },
                { "С", "S" },
                { "Т", "T" },
                { "У", "U" },
                { "Ф", "F" },
                { "Х", "X" },
                { "Ц", "C" },
                { "Ч", "Ch" },
                { "Ш", "Sh" },
                { "Щ", "Shh" },
                { "Ъ", "'" },
                { "Ы", "Y" },
                { "Ь", "" },
                { "Э", "E" },
                { "Ю", "Yu" },
                { "Я", "Ya" },
                { "а", "a" },
                { "б", "b" },
                { "в", "v" },
                { "г", "g" },
                { "д", "d" },
                { "е", "e" },
                { "ё", "yo" },
                { "ж", "zh" },
                { "з", "z" },
                { "и", "i" },
                { "й", "j" },
                { "к", "k" },
                { "л", "l" },
                { "м", "m" },
                { "н", "n" },
                { "о", "o" },
                { "п", "p" },
                { "р", "r" },
                { "с", "s" },
                { "т", "t" },
                { "у", "u" },
                { "ф", "f" },
                { "х", "x" },
                { "ц", "c" },
                { "ч", "ch" },
                { "ш", "sh" },
                { "щ", "shh" },
                { "ъ", "" },
                { "ы", "y" },
                { "ь", "" },
                { "э", "e" },
                { "ю", "yu" },
                { "я", "ya" },
            };

        public static string TransliterateToLatin(this string text)
        {
            string output = text;

            output = Regex.Replace(output, @"\s|\.|\(", " ");
            output = Regex.Replace(output, @"\s+", " ");
            output = Regex.Replace(output, @"[^\s\w\d-]", "");
            output = output.Trim();

            foreach (KeyValuePair<string, string> key in _replaceDictionary)
            {
                output = output.Replace(key.Key, key.Value);
            }
            return output;
        }

        public static string TransliterateToCyrillic(this string text)
        {
            string output = text;

            foreach (KeyValuePair<string, string> key in _replaceDictionary)
            {
                output = output.Replace(key.Value, key.Key);
            }
            return output;
        }

        // Latin -> Cyrillic for the "scene" transliteration of Russian titles
        // (Deti.peremen, Holostyak, Militsioner.s.rublevki, Pervaya.raketka):
        // longest match first, no ISO 9 / GOST strictness
        private static readonly (string Latin, string Cyrillic)[] _latinToCyrillic =
        {
            ("shch", "щ"), ("sch", "щ"),
            ("zh", "ж"), ("kh", "х"), ("ts", "ц"), ("tz", "ц"), ("ch", "ч"), ("sh", "ш"),
            ("yo", "ё"), ("jo", "ё"), ("yu", "ю"), ("ju", "ю"), ("ya", "я"), ("ja", "я"),
            ("ye", "е"), ("je", "е"),
            ("a", "а"), ("b", "б"), ("v", "в"), ("w", "в"), ("g", "г"), ("d", "д"),
            ("e", "е"), ("z", "з"), ("i", "и"), ("j", "й"), ("k", "к"), ("q", "к"),
            ("l", "л"), ("m", "м"), ("n", "н"), ("o", "о"), ("p", "п"), ("r", "р"),
            ("s", "с"), ("t", "т"), ("u", "у"), ("f", "ф"), ("h", "х"), ("c", "ц"),
            ("x", "кс"), ("'", "ь"),
        };

        private const string LatinVowels = "aeiouy";

        // Word endings -iy / -yy / -oy: adjectives (Siniy, Krasnyy, Bolshoy)
        private static readonly Regex _adjectiveEnding = new(@"(?<=\p{L})(iy|yy|ij|yj)\b", RegexOptions.Compiled);

        public static bool HasCyrillic(this string text)
            => !string.IsNullOrEmpty(text) && text.Any(c => c is >= '\u0400' and <= '\u04FF');

        public static bool HasLatin(this string text)
            => !string.IsNullOrEmpty(text) && text.Any(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z');

        /// <summary>
        /// Russian title written in Latin letters back to Cyrillic, lower case:
        /// "Militsioner s rublevki" -> "милиционер с рублевки". Approximate
        /// (ш / щ, е / э, the soft sign), good enough for the search.
        /// </summary>
        public static string TransliterateLatinToCyrillic(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var s = _adjectiveEnding.Replace(text.ToLowerInvariant(), m => m.Value[0] == 'i' ? "ий" : "ый");
            var sb = new StringBuilder(s.Length);
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                var wordStart = i == 0 || !char.IsLetter(s[i - 1]);

                // y: й after a vowel (moy), ы otherwise (rybinske); ya / yu / ... below
                if (c == 'y' && !(i + 1 < s.Length && "aoue".Contains(s[i + 1])))
                {
                    sb.Append(i > 0 && LatinVowels.Contains(s[i - 1]) ? 'й' : 'ы');
                    i++;
                    continue;
                }
                // e at the start of a word: э (eto, ekipazh)
                if (c == 'e' && wordStart && !(i + 1 < s.Length && s[i + 1] == 'y'))
                {
                    sb.Append('э');
                    i++;
                    continue;
                }

                var matched = false;
                foreach (var (latin, cyrillic) in _latinToCyrillic)
                {
                    if (string.CompareOrdinal(s, i, latin, 0, latin.Length) == 0)
                    {
                        sb.Append(cyrillic);
                        i += latin.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Loose comparison key of a title: lower case, letters / digits only,
        /// single spaces, the letters a transliteration can't tell apart folded
        /// (ё/е/э, й/и/ы, щ/ш, ь/ъ dropped).
        /// </summary>
        public static string ToTitleKey(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (var ch in text.ToLowerInvariant())
            {
                switch (ch)
                {
                    case 'ё': case 'э': sb.Append('е'); break;
                    case 'й': case 'ы': sb.Append('и'); break;
                    case 'щ': sb.Append('ш'); break;
                    case 'ь': case 'ъ': break;
                    default:
                        sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
                        break;
                }
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }
    }
}
