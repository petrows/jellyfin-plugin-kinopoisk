using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Cleans a release-style file / folder name into a searchable title, the
    /// way Kodi does (CUtil::CleanString, advancedsettings cleandatetime /
    /// cleanstrings): '_' and '.' become spaces, [tags] are dropped, the year
    /// is split off and everything from the first release tag (S01, 1080p,
    /// WEB-DL, x264, ...) on is cut.
    /// "Deti.peremen.S01.2024.WEB-DL.1080p.ExKinoRay" -> ("Deti peremen", 2024).
    /// </summary>
    public static class TitleCleaner
    {
        // Separators around years / tags (Kodi: [ _\,\.\(\)\[\]\-])
        private const string Sep = @"[\s,()\[\]\-]";

        private static readonly Regex Extension = new(
            @"\.(mkv|avi|mp4|m4v|ts|m2ts|wmv|mov|mpg|mpeg|webm|divx|iso)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // [tracker tags], {tags}
        private static readonly Regex Brackets = new(@"\[[^\]]*\]|\{[^}]*\}", RegexOptions.Compiled);

        // Kodi cleandatetime: <title><sep><year><sep or end>, the last year
        // ("Blade Runner 2049 2017" -> "Blade Runner 2049", 2017)
        private static readonly Regex Year = new(
            $@"^(?<title>.*[^\s,()\[\]\-]){Sep}+(?<year>(?:19|20)\d{{2}})(?={Sep}|$)",
            RegexOptions.Compiled);

        // Kodi cleanstrings + the usual release tags: cut from the first one on
        private static readonly Regex Tags = new(
            $@"^(?<title>.+?){Sep}+(?:"
            + @"s\d{1,2}(?:e\d{1,3})?|se\d{1,2}|season|сезон|\d{1,2}\s*сезон"
            + @"|(?:480|576|720|1080|2160)[pi]?|\d{3,4}p|4k|uhd|hdr|10bit"
            + @"|web-?(?:dl|rip)(?:rip)?|hdtv(?:rip)?|bd-?rip|br-?rip|blu-?ray|bdremux|remux"
            + @"|dvd(?:rip|scr|screener|5|9)?|hdrip|satrip|tvrip|camrip|vhsrip|iptvrip"
            + @"|[xh]\s?26[45]|hevc|avc|xvid|divx5?|aac|ac3|dts|ddp?\s?5|mp3"
            + @"|amzn|nf|hmax|atvp|dsnp|pcok"
            + @"|extended|remastered|unrated|limited|proper|repack|rerip|internal"
            + @"|multisubs|custom|dc|dsr|dsrip|ntsc|pal|pdtv|svcd|ws|cd[1-9]"
            + $@")(?={Sep}|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Cleaned title (the input as is when nothing is left) and the year
        /// found in the name, if any.
        /// </summary>
        public static (string Title, int? Year) Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (name, null);

            var s = Extension.Replace(name.Trim(), string.Empty);
            s = Brackets.Replace(s, " ");
            // Kodi: '_' always, '.' only without spaces in the name - here
            // always: "Babylon Berlin.S01" is common
            s = s.Replace('_', ' ').Replace('.', ' ');
            s = Spaces.Replace(s, " ").Trim();

            int? year = null;
            var m = Year.Match(s);
            if (m.Success)
            {
                year = int.Parse(m.Groups["year"].Value);
                s = m.Groups["title"].Value;
            }

            m = Tags.Match(s);
            if (m.Success)
                s = m.Groups["title"].Value;

            s = s.Replace("()", " ");
            s = Spaces.Replace(s, " ").Trim(' ', ',', '-', '(', '[');

            return (s.Length == 0 ? name.Trim() : s, year);
        }
    }
}
