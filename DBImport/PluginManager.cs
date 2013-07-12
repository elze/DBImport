using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DBImport
{
    public class PluginManager
    {
        private static int nNumberOfMethods = 4;
        protected static Hashtable mapNameToMethod;
        protected static int CompactifierSegmentSize; 
        protected static string[] searchTermRegexKeys;
        protected static string[] imageExtrRegexKeys;
        protected static string[] searchTermRegexes;
        protected static string[] imageExtrRegexes;

        public static void readAppSettings()
        {
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            string[] keys = appSettings.AllKeys;
            searchTermRegexKeys = Array.FindAll(keys, k => k.Contains("search_term_regex"));

            Dictionary<string, string> dict1 = searchTermRegexKeys.ToDictionary(k => k.Substring(k.IndexOf(".") + 1), k => appSettings[k]);
            SortedDictionary<string, string> sDict1 = new SortedDictionary<string, string>(dict1);
            searchTermRegexes = new string[sDict1.Count];
            sDict1.Values.CopyTo(searchTermRegexes, 0);

            imageExtrRegexKeys = Array.FindAll(keys, k => k.Contains("image_extraction_regex"));
            Dictionary<string, string> dict2 = imageExtrRegexKeys.ToDictionary(k => k.Substring(k.IndexOf(".") + 1), k => appSettings[k]);
            SortedDictionary<string, string> sDict2 = new SortedDictionary<string, string>(dict2);

            imageExtrRegexes = new string[sDict2.Count];
            sDict2.Values.CopyTo(imageExtrRegexes, 0);

            System.Int32.TryParse(appSettings["CompactifierSegmentSize"], out CompactifierSegmentSize);
        }

        public static int getCompactifierSegmentSize()
        {
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            int compactifierSegmentSize;
            System.Int32.TryParse(appSettings["CompactifierSegmentSize"], out compactifierSegmentSize);
            Console.WriteLine("PluginManager.getCompactifierSegmentSize: compactifierSegmentSize = " + compactifierSegmentSize + " appSettings[CompactifierSegmentSize] = " + appSettings["CompactifierSegmentSize"]);
            return compactifierSegmentSize;
        }

        public static void setAppSettings(string[] searchTermRgxes, string[] imageExtrRgxes) { 
            searchTermRegexes = searchTermRgxes;
            imageExtrRegexes = imageExtrRgxes;
            //foreach (string searchTermRegex in searchTermRegexes)
              //  Console.WriteLine("setAppSettings: searchTermRegex = " + searchTermRegex);
        }

        public static void setCompactifierSegmentSize(int compactifierSegmentSize)
        {
            CompactifierSegmentSize = compactifierSegmentSize;
        }

        public static DelStringTransformer getDefaultTransformMethod(string fieldNameToTransform)
        {
            if (fieldNameToTransform == "came_from")
                return compactifyReferringURL;
            if (fieldNameToTransform == "page_url")
                return insertLineBreaksIntoURL;
            throw (new ApplicationException("No transform method specified for field " + fieldNameToTransform + ", and no default method exists."));
        }

        public static Hashtable getMapNameToMethod()
        {
            if (mapNameToMethod == null)
            {
                mapNameToMethod = new Hashtable(nNumberOfMethods);
                DelStringTransformer m1 = compactifyReferringURL;
                DelStringTransformer m2 = insertLineBreaksIntoURL;
                DelStringTransformer m3 = compactifyReferringURLAndInsertBreaks;
                DelFieldExtractor m4 = extractQuoteEnclosedDoubleQuoteEscapedFields;
                mapNameToMethod["PluginManager.compactifyReferringURL"] = m1;
                mapNameToMethod["PluginManager.insertLineBreaksIntoURL"] = m2;
                mapNameToMethod["PluginManager.compactifyReferringURLAndInsertBreaks"] = m3;
                mapNameToMethod["PluginManager.extractQuoteEnclosedDoubleQuoteEscapedFields"] = m4;
            }
            return mapNameToMethod;
        }

        public static string compactifyReferringURL(string strToExtractFromPossiblyEncoded)
        {
            string strCompact = "";
            string strToExtractFrom = "";
            Console.WriteLine("compactifyReferringURL begins: strToExtractFromPossiblyEncoded = " + strToExtractFromPossiblyEncoded);
            Console.WriteLine("compactifyReferringURL: CurrentDirectory = " + Directory.GetCurrentDirectory());

            NameValueCollection appSettings;
            try
            {
                appSettings = ConfigurationManager.AppSettings;
            }
            catch (ConfigurationErrorsException e)
            {
                Console.WriteLine("Could not read AppSettings: {0}", e.ToString());
                return strToExtractFromPossiblyEncoded;
            }

            string stringEncoded1;
            string stringEncoded2 = strToExtractFromPossiblyEncoded;
            do
            {
                stringEncoded1 = stringEncoded2;
                stringEncoded2 = HttpUtility.UrlDecode(stringEncoded1);
            }
            while (stringEncoded2 != stringEncoded1);
            strToExtractFrom = stringEncoded2;

            string[] paramValuePairs = strToExtractFrom.Split('&');

            if ((searchTermRegexes == null) && (imageExtrRegexes == null))
                readAppSettings();

            IEnumerable<Regex> regexQuery1 = from searchTermRegex in searchTermRegexes
                                             select new Regex(searchTermRegex);

            Regex[] regexes1 = regexQuery1.ToArray();

            foreach (string paramValuePair in paramValuePairs)
            {
                foreach (Regex regex in regexes1)
                {
                    foreach (Match match in regex.Matches(paramValuePair))
                    {
                        if (match.Success)
                        {
                            strCompact += "... " + match.Groups[1].Value + " ";
                        }
                    }
                }
            }

            IEnumerable<Regex> regexQuery2 = from imageExtrRegex in imageExtrRegexes
                                             select new Regex(imageExtrRegex);

            Regex[] regexes2 = regexQuery2.ToArray();

            string strCompactImages = "";

            foreach (Regex regex in regexes2)
            {
                foreach (string paramValuePair in paramValuePairs)
                {
                    foreach (Match match in regex.Matches(paramValuePair))
                    {
                        if (match.Success)
                        {
                            strCompactImages += "... " + match.Value + " ";
                        }
                    }
                }
                if (!string.IsNullOrEmpty(strCompactImages))
                    break;
            }

            strCompact += strCompactImages;

            if (string.IsNullOrEmpty(strCompact))
                strCompact = strToExtractFrom;

            return strCompact;
        }

        public static string insertLineBreaksIntoURL(string strURL)
        {
            if (CompactifierSegmentSize == 0)
                readAppSettings();
            string strToBreakUp = HttpUtility.UrlDecode(strURL);
            int indOfDelimiter = 0;
            while ((indOfDelimiter != -1) && (strToBreakUp.Length - indOfDelimiter > CompactifierSegmentSize))
            {
                string target = "/&";
                char[] anyOf = target.ToCharArray();
                indOfDelimiter = strToBreakUp.IndexOfAny(anyOf, indOfDelimiter + CompactifierSegmentSize);
                if (indOfDelimiter != -1)
                {
                    strToBreakUp = strToBreakUp.Insert(indOfDelimiter, "<br/>");
                }
            }

            //Console.WriteLine("insertLineBreaksIntoURL after the loop: strToBreakUp = " + strToBreakUp);
            
            Regex regex = new Regex("(?=\\s\\.\\.\\.\\s\\w+)");

            string[] segments = regex.Split(strToBreakUp);
            strToBreakUp = string.Join("<br/>", segments);
            //Console.WriteLine("insertLineBreaksIntoURL after the join: strToBreakUp = " + strToBreakUp);

            return strToBreakUp;
        }

        public static string compactifyReferringURLAndInsertBreaks(string strToExtractFromPossiblyEncoded)
        {
            string strCompactified = compactifyReferringURL(strToExtractFromPossiblyEncoded);
            string strWithLineBreaks = insertLineBreaksIntoURL(strCompactified);
            return strWithLineBreaks;
        }    

	public static string[][] extractQuoteEnclosedDoubleQuoteEscapedFields(string[] allLines)
        {
            string[] toSplitOn = { "\",\"" };
            string[][] fields = new string[allLines.Length][];

            int lineNum = 0;
            foreach (string inputLine in allLines)
            {
                //Console.WriteLine(inputLine);

                fields[lineNum] = inputLine.Split(toSplitOn, StringSplitOptions.None);
                for (int i = 0; i < fields[lineNum].Length; i++)
                {
                    if (fields[lineNum][i].StartsWith("\""))
                    {
                        fields[lineNum][i] = fields[lineNum][i].Remove(0, 1);
                    }
                    if (fields[lineNum][i].EndsWith("\""))
                    {
                        fields[lineNum][i] = fields[lineNum][i].Remove(fields[lineNum][i].Length - 1, 1);
                    }
                }
                lineNum++;
            }
            return fields;
        }

    }
}
